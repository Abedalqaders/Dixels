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

[Authorize(DixelsPermissions.Buildings.Default)]
public class BuildingsAppService : DixelsAppService, IBuildingsAppService
{
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly SpaceHierarchyManager _spaceHierarchyManager;
    private readonly IDataFilter _dataFilter;

    public BuildingsAppService(
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        ConstraintResolver constraintResolver,
        SpaceHierarchyManager spaceHierarchyManager,
        IDataFilter dataFilter)
    {
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _spaceRepository = spaceRepository;
        _constraintResolver = constraintResolver;
        _spaceHierarchyManager = spaceHierarchyManager;
        _dataFilter = dataFilter;
    }

    public async Task<BuildingDto> GetAsync(Guid id)
    {
        var building = await _buildingRepository.GetAsync(id);
        return MapToDto(building);
    }

    public async Task<PagedResultDto<BuildingDto>> GetListAsync(GetBuildingsInput input)
    {
        async Task<PagedResultDto<BuildingDto>> QueryAsync()
        {
            var queryable = await _buildingRepository.GetQueryableAsync();

            if (!input.Filter.IsNullOrWhiteSpace())
            {
                queryable = queryable.Where(b => b.Name.Contains(input.Filter!));
            }

            var totalCount = await AsyncExecuter.CountAsync(queryable);
            var buildings = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(b => b.Name).Skip(input.SkipCount).Take(input.MaxResultCount));

            return new PagedResultDto<BuildingDto>(totalCount, buildings.Select(MapToDto).ToList());
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

    [Authorize(DixelsPermissions.Buildings.Create)]
    public async Task<BuildingDto> CreateAsync(CreateBuildingDto input)
    {
        var building = new Building(
            GuidGenerator.Create(),
            input.Name,
            input.BuildingNumber,
            input.Timezone,
            ConstraintDtoConversions.ToOperatingDays(input.Days),
            ConstraintDtoConversions.ToOperatingWindow(input.Hours),
            input.MaxDurationMinutes,
            input.MaxHorizonDays,
            input.MinLeadMinutes);

        await _buildingRepository.InsertAsync(building);

        return MapToDto(building);
    }

    [Authorize(DixelsPermissions.Buildings.Edit)]
    public async Task<BuildingDto> UpdateAsync(Guid id, UpdateBuildingDto input)
    {
        await EnsureCanManageBuildingAsync(id);

        var building = await _buildingRepository.GetAsync(id);
        building.SetName(input.Name);
        building.SetBuildingNumber(input.BuildingNumber);
        building.SetTimezone(input.Timezone);

        await _buildingRepository.UpdateAsync(building);

        return MapToDto(building);
    }

    // Fully-qualified route, same reasoning as GetTreeAsync above.
    [HttpPut("api/app/buildings/{id}/constraints")]
    [Authorize(DixelsPermissions.Buildings.Edit)]
    public async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateBuildingConstraintsDto input)
    {
        await EnsureCanManageBuildingAsync(id);

        var building = await _buildingRepository.GetAsync(id);
        building.ConcurrencyStamp = input.ConcurrencyStamp;

        var proposedDays = ConstraintDtoConversions.ToOperatingDays(input.Days);
        var proposedHours = ConstraintDtoConversions.ToOperatingWindow(input.Hours);

        // Computed before saving: the tightening itself is never blocked by this (matching
        // "tightening never retroactively invalidates"), it's purely informational for the
        // admin to go fix the named descendants afterward.
        var warnings = await FindNarrowingConflictsAsync(id, proposedDays, proposedHours);

        building.SetOperatingDays(proposedDays);
        building.SetOperatingHours(proposedHours);
        building.SetMaxDurationMinutes(input.MaxDurationMinutes);
        building.SetMaxHorizonDays(input.MaxHorizonDays);
        building.SetMinLeadMinutes(input.MinLeadMinutes);

        await _buildingRepository.UpdateAsync(building);

        // The test module disables automatic UoW transactions, so a save isn't guaranteed
        // flushed until the ambient unit of work completes at the very end of this method —
        // but we need the freshly-generated ConcurrencyStamp back *now*, in this same
        // response, so flush explicitly instead of returning whatever's still in memory.
        await CurrentUnitOfWork!.SaveChangesAsync();

        return new ConstraintsSaveResultDto
        {
            ConcurrencyStamp = building.ConcurrencyStamp,
            Warnings = warnings,
        };
    }

    [Authorize(DixelsPermissions.Buildings.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await EnsureCanManageBuildingAsync(id);

        var building = await _buildingRepository.GetAsync(id);
        var floors = await _floorRepository.GetListAsync(f => f.BuildingId == id);
        var floorIds = floors.Select(f => f.Id).ToList();
        var spaces = floorIds.Count == 0
            ? new List<Space>()
            : await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId));

        var batchId = GuidGenerator.Create();
        _spaceHierarchyManager.MarkForSoftDelete(batchId, building, floors, spaces);

        // Flushed separately from the soft-deletes below: converting a repository DeleteAsync
        // call into a soft-delete (Remove -> Modified) appears to reset which properties EF
        // considers actually changed, silently dropping a DeletionBatchId mutation made just
        // beforehand if both land in the same SaveChanges call. Saving the stamp on its own
        // first sidesteps that entirely.
        await _buildingRepository.UpdateAsync(building);
        foreach (var floor in floors)
        {
            await _floorRepository.UpdateAsync(floor);
        }

        foreach (var space in spaces)
        {
            await _spaceRepository.UpdateAsync(space);
        }

        await CurrentUnitOfWork!.SaveChangesAsync();

        // Children first: once the building itself is (soft-)deleted, a plain repository
        // query for its floors would already be filtered out by the parent's own state in
        // some setups, and this ordering matches how a real DB cascade would run anyway.
        foreach (var space in spaces)
        {
            await _spaceRepository.DeleteAsync(space);
        }

        foreach (var floor in floors)
        {
            await _floorRepository.DeleteAsync(floor);
        }

        await _buildingRepository.DeleteAsync(building);
        await CurrentUnitOfWork!.SaveChangesAsync();
    }

    // Fully-qualified route, same reasoning as GetTreeAsync above.
    [HttpPost("api/app/buildings/{id}/restore")]
    [Authorize(DixelsPermissions.Buildings.Edit)]
    public async Task RestoreAsync(Guid id)
    {
        await EnsureCanManageBuildingAsync(id);

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var building = await _buildingRepository.GetAsync(id);
            var batchId = building.DeletionBatchId;

            if (batchId is null)
            {
                building.IsDeleted = false;
                await _buildingRepository.UpdateAsync(building);
                await CurrentUnitOfWork!.SaveChangesAsync();
                return;
            }

            var candidateFloors = await _floorRepository.GetListAsync(f => f.BuildingId == id);
            var restoredFloors = _spaceHierarchyManager.SelectAndClearForRestore(batchId.Value, candidateFloors);

            var floorIds = restoredFloors.Select(f => f.Id).ToList();
            var candidateSpaces = floorIds.Count == 0
                ? new List<Space>()
                : await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId));
            var restoredSpaces = _spaceHierarchyManager.SelectAndClearForRestore(batchId.Value, candidateSpaces);

            building.ClearDeletionBatch();
            building.IsDeleted = false;
            await _buildingRepository.UpdateAsync(building);

            foreach (var floor in restoredFloors)
            {
                floor.IsDeleted = false;
                await _floorRepository.UpdateAsync(floor);
            }

            foreach (var space in restoredSpaces)
            {
                space.IsDeleted = false;
                await _spaceRepository.UpdateAsync(space);
            }

            await CurrentUnitOfWork!.SaveChangesAsync();
        }
    }

    private async Task<List<string>> FindNarrowingConflictsAsync(Guid buildingId, OperatingDays proposedDays, OperatingWindow proposedHours)
    {
        var floors = await _floorRepository.GetListAsync(f => f.BuildingId == buildingId);
        var candidates = floors
            .Where(f => f.Days is not null || f.Hours is not null)
            .Select(f => new NarrowingCandidate(f.Name, f.Days, f.Hours))
            .ToList();

        return _constraintResolver.FindNarrowingConflicts(candidates, proposedDays, proposedHours).ToList();
    }

    private async Task EnsureCanManageBuildingAsync(Guid buildingId)
    {
        // Extensibility hook for future per-building-admin scoping: today this just
        // re-checks the flat permission (already enforced by [Authorize] too, so this is
        // currently redundant). Floor/Space's own equivalent checks — and any future
        // building-scoped permission — plug in here without touching call sites.
        _ = buildingId;
        await AuthorizationService.CheckAsync(DixelsPermissions.Buildings.Edit);
    }

    private BuildingDto MapToDto(Building building)
    {
        var dto = ObjectMapper.Map<Building, BuildingDto>(building);
        dto.Days = ConstraintDtoConversions.ToDayArray(building.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDto(building.Hours);
        dto.IsDeleted = building.IsDeleted;
        return dto;
    }
}
