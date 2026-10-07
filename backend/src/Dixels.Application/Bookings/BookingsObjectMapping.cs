using Dixels.SpaceManagement;
using Riok.Mapperly.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Mapperly;

namespace Dixels.Bookings;

/// <summary>
/// Mapperly mappings for the Bookings vertical, kept separate like
/// <see cref="SpaceManagementObjectMapping"/>. Only the plain property copies live here;
/// values that need another aggregate (space/floor/building names, the building-local
/// times, resolved rules) are ignored and filled in by the app service after mapping.
/// </summary>
[Mapper]
public partial class BookingsObjectMapping :
    IAbpMapperlyMapper<Booking, BookingDto>,
    IAbpMapperlyMapper<Space, BookableSpaceDto>,
    IAbpMapperlyMapper<Building, BookableBuildingDto>,
    IAbpMapperlyMapper<Floor, BookableFloorDto>,
    ITransientDependency
{
    [MapperIgnoreSource(nameof(Booking.UserId))]
    [MapperIgnoreSource(nameof(Booking.ResolvedConstraintsJson))]
    [MapperIgnoreSource(nameof(Booking.IdempotencyKey))]
    [MapperIgnoreSource(nameof(Booking.CancelledById))]
    [MapperIgnoreSource(nameof(Booking.CancelledAt))]
    [MapperIgnoreSource(nameof(Booking.ExtraProperties))]
    [MapperIgnoreSource(nameof(Booking.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Booking.CreationTime))]
    [MapperIgnoreSource(nameof(Booking.CreatorId))]
    [MapperIgnoreSource(nameof(Booking.LastModificationTime))]
    [MapperIgnoreSource(nameof(Booking.LastModifierId))]
    [MapperIgnoreTarget(nameof(BookingDto.SpaceName))]
    [MapperIgnoreTarget(nameof(BookingDto.FloorName))]
    [MapperIgnoreTarget(nameof(BookingDto.BuildingName))]
    [MapperIgnoreTarget(nameof(BookingDto.Timezone))]
    [MapperIgnoreTarget(nameof(BookingDto.LocalStart))]
    [MapperIgnoreTarget(nameof(BookingDto.LocalEnd))]
    [MapperIgnoreTarget(nameof(BookingDto.Recurrence))]
    // The guests need their users' current names, and whether the reader is the owner: the app service fills them.
    [MapperIgnoreSource(nameof(Booking.Invitees))]
    [MapperIgnoreTarget(nameof(BookingDto.Invitees))]
    [MapperIgnoreTarget(nameof(BookingDto.IsOwner))]
    [MapperIgnoreTarget(nameof(BookingDto.OwnerName))]
    public partial BookingDto Map(Booking source);

    [MapperIgnoreSource(nameof(Booking.UserId))]
    [MapperIgnoreSource(nameof(Booking.ResolvedConstraintsJson))]
    [MapperIgnoreSource(nameof(Booking.IdempotencyKey))]
    [MapperIgnoreSource(nameof(Booking.CancelledById))]
    [MapperIgnoreSource(nameof(Booking.CancelledAt))]
    [MapperIgnoreSource(nameof(Booking.ExtraProperties))]
    [MapperIgnoreSource(nameof(Booking.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Booking.CreationTime))]
    [MapperIgnoreSource(nameof(Booking.CreatorId))]
    [MapperIgnoreSource(nameof(Booking.LastModificationTime))]
    [MapperIgnoreSource(nameof(Booking.LastModifierId))]
    [MapperIgnoreTarget(nameof(BookingDto.SpaceName))]
    [MapperIgnoreTarget(nameof(BookingDto.FloorName))]
    [MapperIgnoreTarget(nameof(BookingDto.BuildingName))]
    [MapperIgnoreTarget(nameof(BookingDto.Timezone))]
    [MapperIgnoreTarget(nameof(BookingDto.LocalStart))]
    [MapperIgnoreTarget(nameof(BookingDto.LocalEnd))]
    [MapperIgnoreTarget(nameof(BookingDto.Recurrence))]
    // The guests need their users' current names, and whether the reader is the owner: the app service fills them.
    [MapperIgnoreSource(nameof(Booking.Invitees))]
    [MapperIgnoreTarget(nameof(BookingDto.Invitees))]
    [MapperIgnoreTarget(nameof(BookingDto.IsOwner))]
    [MapperIgnoreTarget(nameof(BookingDto.OwnerName))]
    public partial void Map(Booking source, BookingDto destination);

    public void BeforeMap(Booking source)
    {
    }

    public void AfterMap(Booking source, BookingDto destination)
    {
    }

    // Days/Hours/MaxDurationMinutes on the DTO are the *resolved* values (with the level
    // that set them), not the space's own nullable overrides, so they're filled from
    // ConstraintResolver by the app service rather than copied here.
    [MapperIgnoreSource(nameof(Space.FloorId))]
    [MapperIgnoreSource(nameof(Space.Days))]
    [MapperIgnoreSource(nameof(Space.Hours))]
    [MapperIgnoreSource(nameof(Space.MaxDurationMinutes))]
    [MapperIgnoreSource(nameof(Space.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Space.ExtraProperties))]
    [MapperIgnoreSource(nameof(Space.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Space.CreationTime))]
    [MapperIgnoreSource(nameof(Space.CreatorId))]
    [MapperIgnoreSource(nameof(Space.LastModificationTime))]
    [MapperIgnoreSource(nameof(Space.LastModifierId))]
    [MapperIgnoreSource(nameof(Space.IsDeleted))]
    [MapperIgnoreSource(nameof(Space.DeleterId))]
    [MapperIgnoreSource(nameof(Space.DeletionTime))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.SpaceTypeName))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.IconKey))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.Days))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.Hours))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.MaxDurationMinutes))]
    // The names: which one is shown depends on the reader's language (LocalizedNameReader),
    // so the app service fills Name itself.
    [MapperIgnoreSource(nameof(Space.Translations))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.Name))]
    public partial BookableSpaceDto Map(Space source);

    [MapperIgnoreSource(nameof(Space.FloorId))]
    [MapperIgnoreSource(nameof(Space.Days))]
    [MapperIgnoreSource(nameof(Space.Hours))]
    [MapperIgnoreSource(nameof(Space.MaxDurationMinutes))]
    [MapperIgnoreSource(nameof(Space.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Space.ExtraProperties))]
    [MapperIgnoreSource(nameof(Space.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Space.CreationTime))]
    [MapperIgnoreSource(nameof(Space.CreatorId))]
    [MapperIgnoreSource(nameof(Space.LastModificationTime))]
    [MapperIgnoreSource(nameof(Space.LastModifierId))]
    [MapperIgnoreSource(nameof(Space.IsDeleted))]
    [MapperIgnoreSource(nameof(Space.DeleterId))]
    [MapperIgnoreSource(nameof(Space.DeletionTime))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.SpaceTypeName))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.IconKey))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.Days))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.Hours))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.MaxDurationMinutes))]
    // The names: which one is shown depends on the reader's language (LocalizedNameReader),
    // so the app service fills Name itself.
    [MapperIgnoreSource(nameof(Space.Translations))]
    [MapperIgnoreTarget(nameof(BookableSpaceDto.Name))]
    public partial void Map(Space source, BookableSpaceDto destination);

    public void BeforeMap(Space source)
    {
    }

    public void AfterMap(Space source, BookableSpaceDto destination)
    {
    }

    // Only the building's own booking rules are copied. SlotMinutes is install config
    // (BookingOptions), and Floors is the filtered, ordered list the app service builds.
    [MapperIgnoreSource(nameof(Building.BuildingNumber))]
    [MapperIgnoreSource(nameof(Building.Days))]
    [MapperIgnoreSource(nameof(Building.Hours))]
    [MapperIgnoreSource(nameof(Building.MaxDurationMinutes))]
    [MapperIgnoreSource(nameof(Building.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Building.ExtraProperties))]
    [MapperIgnoreSource(nameof(Building.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Building.CreationTime))]
    [MapperIgnoreSource(nameof(Building.CreatorId))]
    [MapperIgnoreSource(nameof(Building.LastModificationTime))]
    [MapperIgnoreSource(nameof(Building.LastModifierId))]
    [MapperIgnoreSource(nameof(Building.IsDeleted))]
    [MapperIgnoreSource(nameof(Building.DeleterId))]
    [MapperIgnoreSource(nameof(Building.DeletionTime))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.SlotMinutes))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Floors))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.IsRemoved))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Days))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Hours))]
    // The names: which one is shown depends on the reader's language (LocalizedNameReader),
    // so the app service fills Name itself.
    [MapperIgnoreSource(nameof(Building.Translations))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Name))]
    public partial BookableBuildingDto Map(Building source);

    [MapperIgnoreSource(nameof(Building.BuildingNumber))]
    [MapperIgnoreSource(nameof(Building.Days))]
    [MapperIgnoreSource(nameof(Building.Hours))]
    [MapperIgnoreSource(nameof(Building.MaxDurationMinutes))]
    [MapperIgnoreSource(nameof(Building.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Building.ExtraProperties))]
    [MapperIgnoreSource(nameof(Building.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Building.CreationTime))]
    [MapperIgnoreSource(nameof(Building.CreatorId))]
    [MapperIgnoreSource(nameof(Building.LastModificationTime))]
    [MapperIgnoreSource(nameof(Building.LastModifierId))]
    [MapperIgnoreSource(nameof(Building.IsDeleted))]
    [MapperIgnoreSource(nameof(Building.DeleterId))]
    [MapperIgnoreSource(nameof(Building.DeletionTime))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.SlotMinutes))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Floors))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.IsRemoved))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Days))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Hours))]
    // The names: which one is shown depends on the reader's language (LocalizedNameReader),
    // so the app service fills Name itself.
    [MapperIgnoreSource(nameof(Building.Translations))]
    [MapperIgnoreTarget(nameof(BookableBuildingDto.Name))]
    public partial void Map(Building source, BookableBuildingDto destination);

    public void BeforeMap(Building source)
    {
    }

    public void AfterMap(Building source, BookableBuildingDto destination)
    {
    }

    // Spaces is filled by the app service: each one needs its resolved rules.
    [MapperIgnoreSource(nameof(Floor.BuildingId))]
    [MapperIgnoreSource(nameof(Floor.Days))]
    [MapperIgnoreSource(nameof(Floor.Hours))]
    [MapperIgnoreSource(nameof(Floor.MaxDurationMinutes))]
    [MapperIgnoreSource(nameof(Floor.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Floor.ExtraProperties))]
    [MapperIgnoreSource(nameof(Floor.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Floor.CreationTime))]
    [MapperIgnoreSource(nameof(Floor.CreatorId))]
    [MapperIgnoreSource(nameof(Floor.LastModificationTime))]
    [MapperIgnoreSource(nameof(Floor.LastModifierId))]
    [MapperIgnoreSource(nameof(Floor.IsDeleted))]
    [MapperIgnoreSource(nameof(Floor.DeleterId))]
    [MapperIgnoreSource(nameof(Floor.DeletionTime))]
    [MapperIgnoreTarget(nameof(BookableFloorDto.Spaces))]
    // The names: which one is shown depends on the reader's language (LocalizedNameReader),
    // so the app service fills Name itself.
    [MapperIgnoreSource(nameof(Floor.Translations))]
    [MapperIgnoreTarget(nameof(BookableFloorDto.Name))]
    public partial BookableFloorDto Map(Floor source);

    [MapperIgnoreSource(nameof(Floor.BuildingId))]
    [MapperIgnoreSource(nameof(Floor.Days))]
    [MapperIgnoreSource(nameof(Floor.Hours))]
    [MapperIgnoreSource(nameof(Floor.MaxDurationMinutes))]
    [MapperIgnoreSource(nameof(Floor.DeletionBatchId))]
    [MapperIgnoreSource(nameof(Floor.ExtraProperties))]
    [MapperIgnoreSource(nameof(Floor.ConcurrencyStamp))]
    [MapperIgnoreSource(nameof(Floor.CreationTime))]
    [MapperIgnoreSource(nameof(Floor.CreatorId))]
    [MapperIgnoreSource(nameof(Floor.LastModificationTime))]
    [MapperIgnoreSource(nameof(Floor.LastModifierId))]
    [MapperIgnoreSource(nameof(Floor.IsDeleted))]
    [MapperIgnoreSource(nameof(Floor.DeleterId))]
    [MapperIgnoreSource(nameof(Floor.DeletionTime))]
    [MapperIgnoreTarget(nameof(BookableFloorDto.Spaces))]
    // The names: which one is shown depends on the reader's language (LocalizedNameReader),
    // so the app service fills Name itself.
    [MapperIgnoreSource(nameof(Floor.Translations))]
    [MapperIgnoreTarget(nameof(BookableFloorDto.Name))]
    public partial void Map(Floor source, BookableFloorDto destination);

    public void BeforeMap(Floor source)
    {
    }

    public void AfterMap(Floor source, BookableFloorDto destination)
    {
    }
}
