using Riok.Mapperly.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Mapperly;

namespace Dixels.SpaceManagement;

/// <summary>
/// Kept separate from <see cref="DixelsApplicationMappers"/> (per that class's own comment
/// on splitting mapper classes for organization) so SpaceManagement's mapping stays
/// self-contained. One class can implement <see cref="IAbpMapperlyMapper{TSource,TDestination}"/>
/// for several distinct pairs at once, so this class grows a new Map overload (and a new
/// interface implementation) as Building/Floor/Space/AvailabilityOverride DTOs are added,
/// rather than spawning one class per pair.
///
/// Implementing IAbpMapperlyMapper alone is NOT enough to be discovered: ABP's conventional
/// DI registrar only auto-registers classes that also carry a lifetime marker interface
/// (which is exactly what Volo.Abp.Mapperly.MapperBase&lt;,&gt; bundles in for you — it just
/// can't be inherited more than once per class, which is why this class implements the
/// interfaces directly instead and adds ITransientDependency itself).
/// </summary>
[Mapper]
public partial class SpaceManagementObjectMapping :
    IAbpMapperlyMapper<SpaceType, SpaceTypeDto>,
    IAbpMapperlyMapper<Building, BuildingDto>,
    IAbpMapperlyMapper<Floor, FloorDto>,
    IAbpMapperlyMapper<Space, SpaceDto>,
    IAbpMapperlyMapper<AvailabilityOverride, AvailabilityOverrideDto>,
    ITransientDependency
{
    [MapperIgnoreSource(nameof(SpaceType.ExtraProperties))]
    [MapperIgnoreSource(nameof(SpaceType.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(SpaceType.CreationTime))]
    [MapperIgnoreSource(nameof(SpaceType.CreatorId))]
    [MapperIgnoreSource(nameof(SpaceType.LastModificationTime))]
    [MapperIgnoreSource(nameof(SpaceType.LastModifierId))]
    [MapperIgnoreSource(nameof(SpaceType.IsDeleted))]
    [MapperIgnoreSource(nameof(SpaceType.DeleterId))]
    [MapperIgnoreSource(nameof(SpaceType.DeletionTime))]
    public partial SpaceTypeDto Map(SpaceType source);

    [MapperIgnoreSource(nameof(SpaceType.ExtraProperties))]
    [MapperIgnoreSource(nameof(SpaceType.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(SpaceType.CreationTime))]
    [MapperIgnoreSource(nameof(SpaceType.CreatorId))]
    [MapperIgnoreSource(nameof(SpaceType.LastModificationTime))]
    [MapperIgnoreSource(nameof(SpaceType.LastModifierId))]
    [MapperIgnoreSource(nameof(SpaceType.IsDeleted))]
    [MapperIgnoreSource(nameof(SpaceType.DeleterId))]
    [MapperIgnoreSource(nameof(SpaceType.DeletionTime))]
    public partial void Map(SpaceType source, SpaceTypeDto destination);

    public void BeforeMap(SpaceType source)
    {
    }

    public void AfterMap(SpaceType source, SpaceTypeDto destination)
    {
    }

    // Days/Hours are ignored here and set manually via ConstraintDtoConversions in
    // BuildingsAppService — converting them is a real decision (e.g. OperatingWindow.FullDay
    // vs. the regular constructor), not a dumb property copy Mapperly should own.
    [MapperIgnoreSource(nameof(Building.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Building.ExtraProperties))]
    [MapperIgnoreSource(nameof(Building.CreationTime))]
    [MapperIgnoreSource(nameof(Building.CreatorId))]
    [MapperIgnoreSource(nameof(Building.LastModificationTime))]
    [MapperIgnoreSource(nameof(Building.LastModifierId))]
    [MapperIgnoreSource(nameof(Building.IsDeleted))]
    [MapperIgnoreSource(nameof(Building.DeleterId))]
    [MapperIgnoreSource(nameof(Building.DeletionTime))]
    [MapperIgnoreSource(nameof(Building.Days))]
    [MapperIgnoreSource(nameof(Building.Hours))]
    [MapperIgnoreTarget(nameof(BuildingDto.Days))]
    [MapperIgnoreTarget(nameof(BuildingDto.Hours))]
    [MapperIgnoreTarget(nameof(BuildingDto.IsDeleted))]
    public partial BuildingDto Map(Building source);

    [MapperIgnoreSource(nameof(Building.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Building.ExtraProperties))]
    [MapperIgnoreSource(nameof(Building.CreationTime))]
    [MapperIgnoreSource(nameof(Building.CreatorId))]
    [MapperIgnoreSource(nameof(Building.LastModificationTime))]
    [MapperIgnoreSource(nameof(Building.LastModifierId))]
    [MapperIgnoreSource(nameof(Building.IsDeleted))]
    [MapperIgnoreSource(nameof(Building.DeleterId))]
    [MapperIgnoreSource(nameof(Building.DeletionTime))]
    [MapperIgnoreSource(nameof(Building.Days))]
    [MapperIgnoreSource(nameof(Building.Hours))]
    [MapperIgnoreTarget(nameof(BuildingDto.Days))]
    [MapperIgnoreTarget(nameof(BuildingDto.Hours))]
    [MapperIgnoreTarget(nameof(BuildingDto.IsDeleted))]
    public partial void Map(Building source, BuildingDto destination);

    public void BeforeMap(Building source)
    {
    }

    public void AfterMap(Building source, BuildingDto destination)
    {
    }

    [MapperIgnoreSource(nameof(Floor.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Floor.ExtraProperties))]
    [MapperIgnoreSource(nameof(Floor.CreationTime))]
    [MapperIgnoreSource(nameof(Floor.CreatorId))]
    [MapperIgnoreSource(nameof(Floor.LastModificationTime))]
    [MapperIgnoreSource(nameof(Floor.LastModifierId))]
    [MapperIgnoreSource(nameof(Floor.IsDeleted))]
    [MapperIgnoreSource(nameof(Floor.DeleterId))]
    [MapperIgnoreSource(nameof(Floor.DeletionTime))]
    [MapperIgnoreSource(nameof(Floor.Days))]
    [MapperIgnoreSource(nameof(Floor.Hours))]
    [MapperIgnoreTarget(nameof(FloorDto.Days))]
    [MapperIgnoreTarget(nameof(FloorDto.Hours))]
    [MapperIgnoreTarget(nameof(FloorDto.HasOverrides))]
    [MapperIgnoreTarget(nameof(FloorDto.IsDeleted))]
    [MapperIgnoreTarget(nameof(FloorDto.BuildingName))]
    public partial FloorDto Map(Floor source);

    [MapperIgnoreSource(nameof(Floor.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Floor.ExtraProperties))]
    [MapperIgnoreSource(nameof(Floor.CreationTime))]
    [MapperIgnoreSource(nameof(Floor.CreatorId))]
    [MapperIgnoreSource(nameof(Floor.LastModificationTime))]
    [MapperIgnoreSource(nameof(Floor.LastModifierId))]
    [MapperIgnoreSource(nameof(Floor.IsDeleted))]
    [MapperIgnoreSource(nameof(Floor.DeleterId))]
    [MapperIgnoreSource(nameof(Floor.DeletionTime))]
    [MapperIgnoreSource(nameof(Floor.Days))]
    [MapperIgnoreSource(nameof(Floor.Hours))]
    [MapperIgnoreTarget(nameof(FloorDto.Days))]
    [MapperIgnoreTarget(nameof(FloorDto.Hours))]
    [MapperIgnoreTarget(nameof(FloorDto.HasOverrides))]
    [MapperIgnoreTarget(nameof(FloorDto.IsDeleted))]
    [MapperIgnoreTarget(nameof(FloorDto.BuildingName))]
    public partial void Map(Floor source, FloorDto destination);

    public void BeforeMap(Floor source)
    {
    }

    public void AfterMap(Floor source, FloorDto destination)
    {
    }

    [MapperIgnoreSource(nameof(Space.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Space.ExtraProperties))]
    [MapperIgnoreSource(nameof(Space.CreationTime))]
    [MapperIgnoreSource(nameof(Space.CreatorId))]
    [MapperIgnoreSource(nameof(Space.LastModificationTime))]
    [MapperIgnoreSource(nameof(Space.LastModifierId))]
    [MapperIgnoreSource(nameof(Space.IsDeleted))]
    [MapperIgnoreSource(nameof(Space.DeleterId))]
    [MapperIgnoreSource(nameof(Space.DeletionTime))]
    [MapperIgnoreSource(nameof(Space.Days))]
    [MapperIgnoreSource(nameof(Space.Hours))]
    [MapperIgnoreTarget(nameof(SpaceDto.Days))]
    [MapperIgnoreTarget(nameof(SpaceDto.Hours))]
    [MapperIgnoreTarget(nameof(SpaceDto.HasOverrides))]
    [MapperIgnoreTarget(nameof(SpaceDto.IsDeleted))]
    [MapperIgnoreTarget(nameof(SpaceDto.FloorName))]
    [MapperIgnoreTarget(nameof(SpaceDto.BuildingName))]
    public partial SpaceDto Map(Space source);

    [MapperIgnoreSource(nameof(Space.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Space.ExtraProperties))]
    [MapperIgnoreSource(nameof(Space.CreationTime))]
    [MapperIgnoreSource(nameof(Space.CreatorId))]
    [MapperIgnoreSource(nameof(Space.LastModificationTime))]
    [MapperIgnoreSource(nameof(Space.LastModifierId))]
    [MapperIgnoreSource(nameof(Space.IsDeleted))]
    [MapperIgnoreSource(nameof(Space.DeleterId))]
    [MapperIgnoreSource(nameof(Space.DeletionTime))]
    [MapperIgnoreSource(nameof(Space.Days))]
    [MapperIgnoreSource(nameof(Space.Hours))]
    [MapperIgnoreTarget(nameof(SpaceDto.Days))]
    [MapperIgnoreTarget(nameof(SpaceDto.Hours))]
    [MapperIgnoreTarget(nameof(SpaceDto.HasOverrides))]
    [MapperIgnoreTarget(nameof(SpaceDto.IsDeleted))]
    [MapperIgnoreTarget(nameof(SpaceDto.FloorName))]
    [MapperIgnoreTarget(nameof(SpaceDto.BuildingName))]
    public partial void Map(Space source, SpaceDto destination);

    public void BeforeMap(Space source)
    {
    }

    public void AfterMap(Space source, SpaceDto destination)
    {
    }

    [MapperIgnoreSource(nameof(AvailabilityOverride.ExtraProperties))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.CreationTime))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.CreatorId))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.LastModificationTime))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.LastModifierId))]
    public partial AvailabilityOverrideDto Map(AvailabilityOverride source);

    [MapperIgnoreSource(nameof(AvailabilityOverride.ExtraProperties))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.CreationTime))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.CreatorId))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.LastModificationTime))]
    [MapperIgnoreSource(nameof(AvailabilityOverride.LastModifierId))]
    public partial void Map(AvailabilityOverride source, AvailabilityOverrideDto destination);

    public void BeforeMap(AvailabilityOverride source)
    {
    }

    public void AfterMap(AvailabilityOverride source, AvailabilityOverrideDto destination)
    {
    }
}
