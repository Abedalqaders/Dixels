using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Dixels.Bookings;

/// <summary>
/// The employee-facing read side of the space hierarchy. Employees hold no
/// Buildings/Floors/Spaces permissions (those are admin screens); this service returns only
/// what booking needs, and only for the building the employee may book in.
/// </summary>
[Authorize(DixelsPermissions.Bookings.Default)]
public class AvailabilityAppService : DixelsAppService, IAvailabilityAppService
{
    private readonly BookingAccessChecker _accessChecker;
    private readonly ConstraintResolver _constraintResolver;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly BookingOptions _bookingOptions;
    private readonly BookingManager _bookingManager;
    private readonly BookingViolationLocalizer _violationLocalizer;

    public AvailabilityAppService(
        BookingAccessChecker accessChecker,
        ConstraintResolver constraintResolver,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        IRepository<SpaceType, Guid> spaceTypeRepository,
        IOptions<BookingOptions> bookingOptions,
        BookingManager bookingManager,
        BookingViolationLocalizer violationLocalizer)
    {
        _accessChecker = accessChecker;
        _constraintResolver = constraintResolver;
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _spaceRepository = spaceRepository;
        _spaceTypeRepository = spaceTypeRepository;
        _bookingOptions = bookingOptions.Value;
        _bookingManager = bookingManager;
        _violationLocalizer = violationLocalizer;
    }

    public async Task<BookableBuildingDto?> GetMyBuildingAsync()
    {
        var buildingId = await _accessChecker.FindBookableBuildingIdAsync(CurrentUser.GetId());
        if (buildingId is null)
        {
            return null;
        }

        // Null when the assigned building was soft-deleted — the same "not assigned" answer
        // UsersAppService.GetMyBuildingAsync gives, rather than a broken reference.
        var building = await _buildingRepository.FindAsync(buildingId.Value);
        if (building is null)
        {
            return null;
        }

        var floors = await _floorRepository.GetListAsync(f => f.BuildingId == building.Id);
        var floorIds = floors.Select(f => f.Id).ToList();
        var spaces = await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId));

        // Space types are a short company-wide list, so one unfiltered read is cheaper than
        // an IN query. Deleting a type that's in use is blocked, so every space's type exists.
        var spaceTypes = (await _spaceTypeRepository.GetListAsync()).ToDictionary(t => t.Id);

        var spacesByFloor = spaces.ToLookup(s => s.FloorId);

        return new BookableBuildingDto
        {
            Id = building.Id,
            Name = building.Name,
            Timezone = building.Timezone,
            MaxHorizonDays = building.MaxHorizonDays,
            MinLeadMinutes = building.MinLeadMinutes,
            SlotMinutes = _bookingOptions.SlotMinutes,
            Floors = floors
                .Where(f => spacesByFloor[f.Id].Any())
                .OrderBy(f => f.FloorNumber)
                .ThenBy(f => f.Name)
                .Select(floor => new BookableFloorDto
                {
                    Id = floor.Id,
                    Name = floor.Name,
                    FloorNumber = floor.FloorNumber,
                    Spaces = spacesByFloor[floor.Id]
                        .OrderBy(s => s.Name)
                        .Select(space => ToDto(building, floor, space, spaceTypes[space.SpaceTypeId]))
                        .ToList(),
                })
                .ToList(),
        };
    }

    public async Task<AvailabilitySearchResultDto> SearchAsync(SearchAvailabilityInput input)
    {
        var search = await _bookingManager.SearchAsync(
            CurrentUser.GetId(), input.LocalStart, input.LocalEnd, input.Attendees, input.FloorId, input.SpaceTypeId);

        var spaceTypes = (await _spaceTypeRepository.GetListAsync()).ToDictionary(t => t.Id);
        var dayStartLocal = search.LocalClock.ToLocal(search.Day.Start);

        // Minutes from local midnight, so the client draws the day without timezone math.
        int ToMinute(DateTimeOffset utc) => (int)(search.LocalClock.ToLocal(utc) - dayStartLocal).TotalMinutes;

        List<DayRangeDto> ToRanges(IEnumerable<TimeRange> ranges) =>
            ranges.Select(r => new DayRangeDto { StartMinute = ToMinute(r.Start), EndMinute = ToMinute(r.End) }).ToList();

        string? ToHhMm(DateTimeOffset? utc)
        {
            if (utc is null)
            {
                return null;
            }

            var minute = ToMinute(utc.Value);
            return $"{minute / 60:00}:{minute % 60:00}";
        }

        return new AvailabilitySearchResultDto
        {
            BuildingId = search.Building.Id,
            BuildingName = search.Building.Name,
            Timezone = search.Building.Timezone,
            LocalStart = input.LocalStart,
            LocalEnd = input.LocalEnd,
            Spaces = search.Spaces
                .OrderByDescending(s => s.IsAvailable)
                .ThenBy(s => s.Floor.FloorNumber)
                .ThenBy(s => s.Floor.Name)
                .ThenBy(s => s.Space.Name)
                .Select(s => new SpaceAvailabilityDto
                {
                    Space = ToDto(search.Building, s.Floor, s.Space, spaceTypes[s.Space.SpaceTypeId]),
                    FloorId = s.Floor.Id,
                    FloorName = s.Floor.Name,
                    IsAvailable = s.IsAvailable,
                    Violations = s.Violations.Select(_violationLocalizer.ToDto).ToList(),
                    FreeUntil = ToHhMm(s.FreeUntil),
                    NextFreeStart = ToHhMm(s.NextFreeStart),
                    Open = ToRanges(s.Open),
                    Closed = ToRanges(s.Closed),
                    Busy = s.Busy
                        .Select(b => b.Range.ClipTo(search.Day) is { } r
                            ? new DayRangeDto { StartMinute = ToMinute(r.Start), EndMinute = ToMinute(r.End), IsMine = b.IsMine }
                            : null)
                        .OfType<DayRangeDto>()
                        .ToList(),
                })
                .ToList(),
        };
    }

    private BookableSpaceDto ToDto(Building building, Floor floor, Space space, SpaceType spaceType)
    {
        var rules = _constraintResolver.Resolve(building, floor, space);

        var dto = ObjectMapper.Map<Space, BookableSpaceDto>(space);
        dto.SpaceTypeName = spaceType.Name;
        dto.IconKey = spaceType.IconKey;
        dto.Days = new FieldValueDto<int[]>
        {
            Value = ConstraintDtoConversions.ToDayArray(rules.Days.Value),
            Source = rules.Days.Source.ToString(),
        };
        dto.Hours = new FieldValueDto<OperatingWindowDto>
        {
            Value = ConstraintDtoConversions.ToWindowDto(rules.Hours.Value),
            Source = rules.Hours.Source.ToString(),
        };
        dto.MaxDurationMinutes = new FieldValueDto<int>
        {
            Value = rules.MaxDurationMinutes.Value,
            Source = rules.MaxDurationMinutes.Source.ToString(),
        };
        return dto;
    }
}
