using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Dixels.Bookings;

[Authorize(DixelsPermissions.Bookings.Default)]
public class BookingsAppService : DixelsAppService, IBookingsAppService
{
    private readonly BookingManager _bookingManager;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly BookingViolationLocalizer _violationLocalizer;

    public BookingsAppService(
        BookingManager bookingManager,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        BookingViolationLocalizer violationLocalizer)
    {
        _bookingManager = bookingManager;
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _violationLocalizer = violationLocalizer;
    }

    // Fully-qualified route, like the other custom actions in this app (see
    // EmployeesAppService.AssignBuildingAsync) — an explicit Http* attribute stops ABP
    // prepending the controller prefix.
    [HttpPost("api/app/bookings/preview")]
    public async Task<BookingPreviewDto> PreviewAsync(BookingRequestDto input)
    {
        var evaluation = await _bookingManager.EvaluateAsync(
            CurrentUser.GetId(), input.SpaceId, input.LocalStart, input.LocalEnd, input.Attendees);

        return new BookingPreviewDto
        {
            IsValid = evaluation.IsValid,
            Violations = evaluation.Violations.Select(_violationLocalizer.ToDto).ToList(),
            StartsAt = evaluation.StartUtc,
            EndsAt = evaluation.EndUtc,
            Timezone = evaluation.Building.Timezone,
        };
    }

    [Authorize(DixelsPermissions.Bookings.Create)]
    public async Task<BookingDto> CreateAsync(CreateBookingDto input)
    {
        try
        {
            var (booking, _) = await _bookingManager.CreateAsync(
                CurrentUser.GetId(),
                input.SpaceId,
                input.LocalStart,
                input.LocalEnd,
                input.Attendees,
                input.Title,
                input.IdempotencyKey);

            return (await MapToDtosAsync(new[] { booking })).Single();
        }
        catch (BookingRejectedException ex) when (ex.Violations[0].Level is { } level)
        {
            // The domain names the level in English ("Space"); swap in the localized word so
            // the {level} placeholder in the error message reads naturally in any language.
            ex.WithData("level", _violationLocalizer.LevelName(level));
            throw;
        }
    }

    /// <summary>
    /// Builds DTOs for a batch of bookings with one query per table (not one per booking) —
    /// shaped for lists, since "my bookings" will reuse it.
    /// </summary>
    private async Task<List<BookingDto>> MapToDtosAsync(IReadOnlyCollection<Booking> bookings)
    {
        var spaceIds = bookings.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id);

        var floorIds = spaces.Values.Select(s => s.FloorId).Distinct().ToList();
        var floors = (await _floorRepository.GetListAsync(f => floorIds.Contains(f.Id))).ToDictionary(f => f.Id);

        var buildingIds = floors.Values.Select(f => f.BuildingId).Distinct().ToList();
        var buildings = (await _buildingRepository.GetListAsync(b => buildingIds.Contains(b.Id))).ToDictionary(b => b.Id);

        return bookings.Select(booking =>
        {
            var space = spaces[booking.SpaceId];
            var floor = floors[space.FloorId];
            var building = buildings[floor.BuildingId];
            var clock = new BuildingClock(building.Timezone);

            var dto = ObjectMapper.Map<Booking, BookingDto>(booking);
            dto.SpaceName = space.Name;
            dto.FloorName = floor.Name;
            dto.BuildingName = building.Name;
            dto.Timezone = building.Timezone;
            dto.LocalStart = clock.ToLocal(booking.StartsAt);
            dto.LocalEnd = clock.ToLocal(booking.EndsAt);
            return dto;
        }).ToList();
    }
}
