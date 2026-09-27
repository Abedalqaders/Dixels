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
    ITransientDependency
{
    [MapperIgnoreSource(nameof(Booking.UserId))]
    [MapperIgnoreSource(nameof(Booking.ResolvedConstraintsJson))]
    [MapperIgnoreSource(nameof(Booking.IdempotencyKey))]
    [MapperIgnoreSource(nameof(Booking.SeriesId))]
    [MapperIgnoreSource(nameof(Booking.CancelledById))]
    [MapperIgnoreSource(nameof(Booking.CancelledAt))]
    [MapperIgnoreSource(nameof(Booking.CancelReason))]
    [MapperIgnoreSource(nameof(Booking.CancelledByAdmin))]
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
    public partial BookingDto Map(Booking source);

    [MapperIgnoreSource(nameof(Booking.UserId))]
    [MapperIgnoreSource(nameof(Booking.ResolvedConstraintsJson))]
    [MapperIgnoreSource(nameof(Booking.IdempotencyKey))]
    [MapperIgnoreSource(nameof(Booking.SeriesId))]
    [MapperIgnoreSource(nameof(Booking.CancelledById))]
    [MapperIgnoreSource(nameof(Booking.CancelledAt))]
    [MapperIgnoreSource(nameof(Booking.CancelReason))]
    [MapperIgnoreSource(nameof(Booking.CancelledByAdmin))]
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
    public partial void Map(Space source, BookableSpaceDto destination);

    public void BeforeMap(Space source)
    {
    }

    public void AfterMap(Space source, BookableSpaceDto destination)
    {
    }
}
