using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Dixels.SpaceManagement.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;

namespace Dixels.SpaceManagement;

[Authorize(DixelsPermissions.Spaces.Default)]
public class SpacesAppService : DixelsAppService, ISpacesAppService
{
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly IDataFilter _dataFilter;

    public SpacesAppService(
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        ConstraintResolver constraintResolver,
        IDataFilter dataFilter)
    {
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _constraintResolver = constraintResolver;
        _dataFilter = dataFilter;
    }

    public async Task<SpaceDto> GetAsync(Guid id)
    {
        var space = await _spaceRepository.GetAsync(id);
        return MapToDto(space);
    }

    public async Task<PagedResultDto<SpaceDto>> GetListAsync(GetSpacesInput input)
    {
        // Space has no EF navigation to Floor/Building (separate aggregate roots, FK-only),
        // so FloorName/BuildingName are resolved with an explicit double join — one SQL
        // query, not one lookup per row. Joined unconditionally for the same reason as
        // FloorsAppService's own GetListAsync: one code path for both the standalone Spaces
        // page and the drill-down Spaces-of-a-floor page.
        async Task<PagedResultDto<SpaceDto>> QueryAsync()
        {
            var spacesQueryable = await _spaceRepository.GetQueryableAsync();
            var floorsQueryable = await _floorRepository.GetQueryableAsync();
            var buildingsQueryable = await _buildingRepository.GetQueryableAsync();

            var joined =
                from s in spacesQueryable
                join f in floorsQueryable on s.FloorId equals f.Id
                join b in buildingsQueryable on f.BuildingId equals b.Id
                select new { Space = s, FloorName = f.Name, BuildingName = b.Name, BuildingId = b.Id };

            if (input.FloorId.HasValue)
            {
                joined = joined.Where(x => x.Space.FloorId == input.FloorId.Value);
            }

            if (input.BuildingId.HasValue)
            {
                joined = joined.Where(x => x.BuildingId == input.BuildingId.Value);
            }

            if (input.SpaceTypeId.HasValue)
            {
                joined = joined.Where(x => x.Space.SpaceTypeId == input.SpaceTypeId.Value);
            }

            if (!input.Filter.IsNullOrWhiteSpace())
            {
                joined = joined.Where(x =>
                    x.Space.Name.Contains(input.Filter!) ||
                    x.FloorName.Contains(input.Filter!) ||
                    x.BuildingName.Contains(input.Filter!));
            }

            var totalCount = await AsyncExecuter.CountAsync(joined);
            var page = await AsyncExecuter.ToListAsync(
                joined.OrderBy(x => x.BuildingName).ThenBy(x => x.FloorName).ThenBy(x => x.Space.Name)
                    .Skip(input.SkipCount).Take(input.MaxResultCount));

            var items = page.Select(x =>
            {
                var dto = MapToDto(x.Space);
                dto.FloorName = x.FloorName;
                dto.BuildingName = x.BuildingName;
                return dto;
            }).ToList();

            return new PagedResultDto<SpaceDto>(totalCount, items);
        }

        if (input.IncludeDeleted)
        {
            using (_dataFilter.Disable<ISoftDelete>())
            {
                return await QueryAsync();
            }
        }

        return await QueryAsync();
    }

    [Authorize(DixelsPermissions.Spaces.Create)]
    public async Task<SpaceDto> CreateAsync(CreateSpaceDto input)
    {
        await EnsureCanManageBuildingAsync(input.FloorId);

        var space = new Space(GuidGenerator.Create(), input.FloorId, input.Name, input.SpaceTypeId, input.Capacity);
        await _spaceRepository.InsertAsync(space);

        return MapToDto(space);
    }

    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task<SpaceDto> UpdateAsync(Guid id, UpdateSpaceDto input)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);

        space.SetName(input.Name);
        space.SetSpaceType(input.SpaceTypeId);
        space.SetCapacity(input.Capacity);

        await _spaceRepository.UpdateAsync(space);

        return MapToDto(space);
    }

    // Fully-qualified route: ABP's conventional-controller routing doesn't auto-prepend the
    // "api/app/spaces" controller prefix once an action carries its own explicit Http*
    // attribute, so a bare "constraints" would collide with Buildings'/Floors' own actions
    // of the same name at the application root (confirmed via a real SwaggerGeneratorException
    // before this fix).
    [HttpPut("api/app/spaces/{id}/constraints")]
    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateSpaceConstraintsDto input)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);

        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        // Narrowed against the *resolved* Floor value (Floor's own override if set, else the
        // Building's) — reusing the same resolver as the read path, never the raw Floor row.
        var resolvedParent = _constraintResolver.Resolve(building, floor);

        space.ConcurrencyStamp = input.ConcurrencyStamp;

        var proposedDays = ConstraintDtoConversions.ToOperatingDaysOrNull(input.Days);
        var proposedHours = ConstraintDtoConversions.ToOperatingWindowOrNull(input.Hours);

        space.SetOwnOperatingDays(proposedDays, resolvedParent.Days.Value);
        space.SetOwnOperatingHours(proposedHours, resolvedParent.Hours.Value);
        space.SetOwnMaxDuration(input.MaxDurationMinutes);
        space.SetMinAttendees(input.MinAttendees);

        await _spaceRepository.UpdateAsync(space);
        await CurrentUnitOfWork!.SaveChangesAsync();

        return new ConstraintsSaveResultDto
        {
            ConcurrencyStamp = space.ConcurrencyStamp,
            // A Space is a leaf — nothing sits below it to ever produce a narrowing warning.
            Warnings = new List<string>(),
        };
    }

    // Fully-qualified route, same reasoning as UpdateConstraintsAsync above.
    [HttpGet("api/app/spaces/{id}/resolved-constraints")]
    public async Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id)
    {
        var space = await _spaceRepository.GetAsync(id);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        var resolved = _constraintResolver.Resolve(building, floor, space);

        return new ResolvedConstraintsDto
        {
            Timezone = resolved.Timezone,
            Days = new FieldValueDto<int[]>
            {
                Value = ConstraintDtoConversions.ToDayArray(resolved.Days.Value),
                Source = resolved.Days.Source.ToString(),
            },
            Hours = new FieldValueDto<OperatingWindowDto>
            {
                Value = ConstraintDtoConversions.ToWindowDto(resolved.Hours.Value),
                Source = resolved.Hours.Source.ToString(),
            },
            MaxDurationMinutes = new FieldValueDto<int>
            {
                Value = resolved.MaxDurationMinutes.Value,
                Source = resolved.MaxDurationMinutes.Source.ToString(),
            },
            MaxHorizonDays = resolved.MaxHorizonDays,
            MinLeadMinutes = resolved.MinLeadMinutes,
            MinAttendees = resolved.MinAttendees,
            Capacity = resolved.Capacity,
            BuildingId = building.Id,
            BuildingName = building.Name,
            FloorId = floor.Id,
            FloorName = floor.Name,
        };
    }

    [Authorize(DixelsPermissions.Spaces.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);

        // No DeletionBatchId stamping here, unlike Building/Floor: batch scoping exists only
        // to disambiguate cascaded siblings on restore, and a Space is a leaf with nothing
        // below it to disambiguate against.
        await _spaceRepository.DeleteAsync(space);
        await CurrentUnitOfWork!.SaveChangesAsync();
    }

    // Fully-qualified route, same reasoning as UpdateConstraintsAsync above.
    [HttpPost("api/app/spaces/{id}/restore")]
    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task RestoreAsync(Guid id)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var space = await _spaceRepository.GetAsync(id);
            await EnsureCanManageBuildingAsync(space.FloorId);

            space.IsDeleted = false;
            await _spaceRepository.UpdateAsync(space);
            await CurrentUnitOfWork!.SaveChangesAsync();
        }
    }

    private async Task EnsureCanManageBuildingAsync(Guid floorId)
    {
        // Same extensibility hook as Buildings/FloorsAppService's.
        _ = floorId;
        await AuthorizationService.CheckAsync(DixelsPermissions.Spaces.Edit);
    }

    private SpaceDto MapToDto(Space space)
    {
        var dto = ObjectMapper.Map<Space, SpaceDto>(space);
        dto.Days = ConstraintDtoConversions.ToDayArrayOrNull(space.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDtoOrNull(space.Hours);
        dto.HasOverrides = space.Days is not null || space.Hours is not null
            || space.MaxDurationMinutes is not null || space.MinAttendees is not null;
        dto.IsDeleted = space.IsDeleted;
        return dto;
    }
}
