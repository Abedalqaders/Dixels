using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.SpaceManagement;

/// <summary>
/// The ids of the rooms in a <see cref="RoomScope"/>, found by a join on floor and building —
/// for when only the ids matter (an event, a count), so no room is loaded as an entity.
/// Removed rooms, floors and buildings are left out by the soft-delete filter, as anywhere.
/// </summary>
public class RoomIdReader : DomainService
{
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;

    public RoomIdReader(IRepository<Space, Guid> spaceRepository, IRepository<Floor, Guid> floorRepository)
    {
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
    }

    /// <summary>The rooms' ids as a query, to use inside another (nothing is read until it runs).</summary>
    public async Task<IQueryable<Guid>> QueryAsync(RoomScope scope)
    {
        var spaces = await _spaceRepository.GetQueryableAsync();
        if (scope.SpaceId is { } spaceId)
        {
            return spaces.Where(s => s.Id == spaceId).Select(s => s.Id);
        }

        var floors = (await _floorRepository.GetQueryableAsync()).Where(f => f.BuildingId == scope.BuildingId);
        if (scope.FloorId is { } floorId)
        {
            floors = floors.Where(f => f.Id == floorId);
        }

        return from s in spaces
               join f in floors on s.FloorId equals f.Id
               select s.Id;
    }

    /// <summary>The rooms' ids, read in one query.</summary>
    public async Task<List<Guid>> GetListAsync(RoomScope scope) =>
        await AsyncExecuter.ToListAsync(await QueryAsync(scope));
}
