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

[Authorize(DixelsPermissions.Floors.Default)]
public class FloorsAppService : DixelsAppService, IFloorsAppService
{
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly SpaceHierarchyManager _spaceHierarchyManager;
    private readonly IDataFilter _dataFilter;

    public FloorsAppService(
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Space, Guid> spaceRepository,
        ConstraintResolver constraintResolver,
        SpaceHierarchyManager spaceHierarchyManager,
        IDataFilter dataFilter)
    {
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _spaceRepository = spaceRepository;
        _constraintResolver = constraintResolver;
        _spaceHierarchyManager = spaceHierarchyManager;
        _dataFilter = dataFilter;
    }

    public async Task<FloorDto> GetAsync(Guid id)
    {
        var floor = await _floorRepository.GetAsync(id);
        return MapToDto(floor);
    }

    public async Task<PagedResultDto<FloorDto>> GetListAsync(GetFloorsInput input)
    {
        // Floor has no EF navigation to Building (separate aggregate roots, FK-only), so
        // BuildingName is resolved with an explicit join — one SQL query, not one lookup per
        // row. Joining unconditionally (even when BuildingId scopes to one building) keeps a
        // single code path instead of branching on whether the caller is the standalone
        // Floors page or the drill-down Floors-of-a-building page.
        async Task<PagedResultDto<FloorDto>> QueryAsync()
        {
            var floorsQueryable = await _floorRepository.GetQueryableAsync();
            var buildingsQueryable = await _buildingRepository.GetQueryableAsync();

            var joined =
                from f in floorsQueryable
                join b in buildingsQueryable on f.BuildingId equals b.Id
                select new { Floor = f, BuildingName = b.Name };

            if (input.BuildingId.HasValue)
            {
                joined = joined.Where(x => x.Floor.BuildingId == input.BuildingId.Value);
            }

            if (!input.Filter.IsNullOrWhiteSpace())
            {
                joined = joined.Where(x => x.Floor.Name.Contains(input.Filter!) || x.BuildingName.Contains(input.Filter!));
            }

            var totalCount = await AsyncExecuter.CountAsync(joined);
            var page = await AsyncExecuter.ToListAsync(
                joined.OrderBy(x => x.BuildingName).ThenBy(x => x.Floor.Name).Skip(input.SkipCount).Take(input.MaxResultCount));

            var items = page.Select(x =>
            {
                var dto = MapToDto(x.Floor);
                dto.BuildingName = x.BuildingName;
                return dto;
            }).ToList();

            return new PagedResultDto<FloorDto>(totalCount, items);
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

    [Authorize(DixelsPermissions.Floors.Create)]
    public async Task<FloorDto> CreateAsync(CreateFloorDto input)
    {
        await EnsureCanManageBuildingAsync(input.BuildingId);

        var floor = new Floor(GuidGenerator.Create(), input.BuildingId, input.Name, input.FloorNumber);
        await _floorRepository.InsertAsync(floor);

        return MapToDto(floor);
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task<FloorDto> UpdateAsync(Guid id, UpdateFloorDto input)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        floor.SetName(input.Name);
        floor.SetFloorNumber(input.FloorNumber);

        await _floorRepository.UpdateAsync(floor);

        return MapToDto(floor);
    }

    // Fully-qualified route: ABP's conventional-controller routing doesn't auto-prepend the
    // "api/app/floors" controller prefix once an action carries its own explicit Http*
    // attribute, so a bare "constraints" would collide with Buildings'/Spaces' own actions
    // of the same name at the application root (confirmed via a real SwaggerGeneratorException
    // before this fix).
    [HttpPut("api/app/floors/{id}/constraints")]
    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateFloorConstraintsDto input)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        floor.ConcurrencyStamp = input.ConcurrencyStamp;

        var proposedDays = ConstraintDtoConversions.ToOperatingDaysOrNull(input.Days);
        var proposedHours = ConstraintDtoConversions.ToOperatingWindowOrNull(input.Hours);

        // Computed before saving — the tightening itself is never blocked, this is purely
        // informational for the admin to go fix the named Spaces afterward.
        var warnings = await FindNarrowingConflictsAsync(id, proposedDays, proposedHours);

        // Validated against the Building's raw values directly — Building has no parent of
        // its own, so its own fields already are the "resolved" value.
        floor.SetOwnOperatingDays(proposedDays, building.Days);
        floor.SetOwnOperatingHours(proposedHours, building.Hours);
        floor.SetOwnMaxDuration(input.MaxDurationMinutes);

        await _floorRepository.UpdateAsync(floor);
        await CurrentUnitOfWork!.SaveChangesAsync();

        return new ConstraintsSaveResultDto
        {
            ConcurrencyStamp = floor.ConcurrencyStamp,
            Warnings = warnings,
        };
    }

    // Fully-qualified route, same reasoning as UpdateConstraintsAsync above.
    [HttpGet("api/app/floors/{id}/resolved-constraints")]
    public async Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id)
    {
        var floor = await _floorRepository.GetAsync(id);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        var resolved = _constraintResolver.Resolve(building, floor);

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
        };
    }

    [Authorize(DixelsPermissions.Floors.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        var spaces = await _spaceRepository.GetListAsync(s => s.FloorId == id);

        var batchId = GuidGenerator.Create();
        _spaceHierarchyManager.MarkForSoftDelete(batchId, floor, spaces);

        // Flushed separately from the soft-deletes below — see BuildingsAppService.DeleteAsync
        // for why: converting Remove -> Modified for the soft-delete appears to reset which
        // properties EF considers changed, silently dropping a same-transaction DeletionBatchId
        // mutation otherwise.
        await _floorRepository.UpdateAsync(floor);
        foreach (var space in spaces)
        {
            await _spaceRepository.UpdateAsync(space);
        }

        await CurrentUnitOfWork!.SaveChangesAsync();

        foreach (var space in spaces)
        {
            await _spaceRepository.DeleteAsync(space);
        }

        await _floorRepository.DeleteAsync(floor);
        await CurrentUnitOfWork!.SaveChangesAsync();
    }

    // Fully-qualified route, same reasoning as UpdateConstraintsAsync above.
    [HttpPost("api/app/floors/{id}/restore")]
    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task RestoreAsync(Guid id)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var floor = await _floorRepository.GetAsync(id);
            await EnsureCanManageBuildingAsync(floor.BuildingId);

            var batchId = floor.DeletionBatchId;

            if (batchId is null)
            {
                floor.IsDeleted = false;
                await _floorRepository.UpdateAsync(floor);
                await CurrentUnitOfWork!.SaveChangesAsync();
                return;
            }

            var candidateSpaces = await _spaceRepository.GetListAsync(s => s.FloorId == id);
            var restoredSpaces = _spaceHierarchyManager.SelectAndClearForRestore(batchId.Value, candidateSpaces);

            floor.ClearDeletionBatch();
            floor.IsDeleted = false;
            await _floorRepository.UpdateAsync(floor);

            foreach (var space in restoredSpaces)
            {
                space.IsDeleted = false;
                await _spaceRepository.UpdateAsync(space);
            }

            await CurrentUnitOfWork!.SaveChangesAsync();
        }
    }

    private async Task<List<string>> FindNarrowingConflictsAsync(Guid floorId, OperatingDays? proposedDays, OperatingWindow? proposedHours)
    {
        var spaces = await _spaceRepository.GetListAsync(s => s.FloorId == floorId);
        var candidates = spaces
            .Where(s => s.Days is not null || s.Hours is not null)
            .Select(s => new NarrowingCandidate(s.Name, s.Days, s.Hours))
            .ToList();

        return _constraintResolver.FindNarrowingConflicts(candidates, proposedDays, proposedHours).ToList();
    }

    private async Task EnsureCanManageBuildingAsync(Guid buildingId)
    {
        // Same extensibility hook as BuildingsAppService's — today just re-checks the flat
        // permission, but is where a future per-building-admin scoping check plugs in,
        // using buildingId rather than the floor's own id.
        _ = buildingId;
        await AuthorizationService.CheckAsync(DixelsPermissions.Floors.Edit);
    }

    private FloorDto MapToDto(Floor floor)
    {
        var dto = ObjectMapper.Map<Floor, FloorDto>(floor);
        dto.Days = ConstraintDtoConversions.ToDayArrayOrNull(floor.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDtoOrNull(floor.Hours);
        dto.HasOverrides = floor.Days is not null || floor.Hours is not null || floor.MaxDurationMinutes is not null;
        dto.IsDeleted = floor.IsDeleted;
        return dto;
    }
}
