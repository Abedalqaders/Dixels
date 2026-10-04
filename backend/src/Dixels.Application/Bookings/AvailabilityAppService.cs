using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiLingualObjects;
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
    private readonly IDataFilter _dataFilter;
    private readonly IMultiLingualObjectManager _multiLingualObjectManager;
    private readonly LocalizedNameReader _nameReader;

    public AvailabilityAppService(
        BookingAccessChecker accessChecker,
        ConstraintResolver constraintResolver,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        IRepository<SpaceType, Guid> spaceTypeRepository,
        IOptions<BookingOptions> bookingOptions,
        BookingManager bookingManager,
        BookingViolationLocalizer violationLocalizer,
        IDataFilter dataFilter,
        IMultiLingualObjectManager multiLingualObjectManager,
        LocalizedNameReader nameReader)
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
        _dataFilter = dataFilter;
        _multiLingualObjectManager = multiLingualObjectManager;
        _nameReader = nameReader;
    }

    public async Task<BookableBuildingDto?> GetMyBuildingAsync()
    {
        var buildingId = await _accessChecker.FindBookableBuildingIdAsync(CurrentUser.GetId());
        if (buildingId is null)
        {
            return null;
        }

        // A building an admin deleted comes back marked IsRemoved, with nothing in it: the
        // employee is told their building went (not "you were never assigned"), and their
        // calendar still shows past and cancelled bookings on that building's clock.
        Building? building;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            building = await _buildingRepository.FindAsync(buildingId.Value);
        }

        if (building is null)
        {
            return null;
        }

        if (building.IsDeleted)
        {
            var removed = await MapBuildingAsync(building);
            removed.IsRemoved = true;
            return removed;
        }

        // With details: their names, in the reader's language below.
        var floors = await _floorRepository.GetListAsync(f => f.BuildingId == building.Id, includeDetails: true);
        var floorIds = floors.Select(f => f.Id).ToList();
        var spaces = await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId), includeDetails: true);
        var floorNames = await _nameReader.ShownAsync<Floor, FloorTranslation>(floors);
        var spaceNames = await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces);
        var byName = StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true);

        // Space types are a short company-wide list, so one unfiltered read is cheaper than
        // an IN query. Deleting a type that's in use is blocked, so every space's type exists.
        var spaceTypes = await GetShownSpaceTypesAsync();

        var spacesByFloor = spaces.ToLookup(s => s.FloorId);

        var dto = await MapBuildingAsync(building);
        dto.Floors = floors
            .Where(f => spacesByFloor[f.Id].Any())
            .OrderBy(f => f.FloorNumber)
            .ThenBy(f => floorNames[f.Id], byName)
            .Select(floor =>
            {
                var floorDto = ObjectMapper.Map<Floor, BookableFloorDto>(floor);
                floorDto.Name = floorNames[floor.Id];
                floorDto.Spaces = spacesByFloor[floor.Id]
                    .OrderBy(s => spaceNames[s.Id], byName)
                    .Select(space => ToDto(building, floor, space, spaceNames[space.Id], spaceTypes[space.SpaceTypeId]))
                    .ToList();
                return floorDto;
            })
            .ToList();
        return dto;
    }

    // Days/Hours are value objects, converted here rather than copied by Mapperly (see
    // SpaceManagementObjectMapping's note on the same two fields).
    private async Task<BookableBuildingDto> MapBuildingAsync(Building building)
    {
        var dto = ObjectMapper.Map<Building, BookableBuildingDto>(building);
        dto.Name = await _nameReader.ShownAsync(building);
        dto.SlotMinutes = _bookingOptions.SlotMinutes;
        dto.Days = ConstraintDtoConversions.ToDayArray(building.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDto(building.Hours);
        return dto;
    }

    public async Task<AvailabilitySearchResultDto> SearchAsync(SearchAvailabilityInput input)
    {
        var search = await _bookingManager.SearchAsync(
            CurrentUser.GetId(), input.LocalStart, input.LocalEnd, input.Attendees, input.FloorId, input.SpaceTypeId);

        var spaceTypes = await GetShownSpaceTypesAsync();
        var floorNames = await _nameReader.ShownAsync<Floor, FloorTranslation>(search.Spaces.Select(s => s.Floor));
        var spaceNames = await _nameReader.ShownAsync<Space, SpaceTranslation>(search.Spaces.Select(s => s.Space));
        var byName = StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true);

        int ToMinute(DateTimeOffset utc) => MinuteOf(search.LocalClock, search.Day, utc);
        List<DayRangeDto> ToRanges(IEnumerable<TimeRange> ranges) => ToDayRanges(search.LocalClock, search.Day, ranges);

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
            BuildingName = await _nameReader.ShownAsync(search.Building),
            Timezone = search.Building.Timezone,
            LocalStart = input.LocalStart,
            LocalEnd = input.LocalEnd,
            Warnings = search.Warnings.Select(_violationLocalizer.ToDto).ToList(),
            Spaces = search.Spaces
                .OrderByDescending(s => s.IsAvailable)
                .ThenBy(s => s.Floor.FloorNumber)
                .ThenBy(s => floorNames[s.Floor.Id], byName)
                .ThenBy(s => spaceNames[s.Space.Id], byName)
                .Select(s => new SpaceAvailabilityDto
                {
                    Space = ToDto(search.Building, s.Floor, s.Space, spaceNames[s.Space.Id], spaceTypes[s.Space.SpaceTypeId]),
                    FloorId = s.Floor.Id,
                    FloorName = floorNames[s.Floor.Id],
                    IsAvailable = s.IsAvailable,
                    Violations = s.Violations.Select(_violationLocalizer.ToDto).ToList(),
                    FreeUntil = ToHhMm(s.FreeUntil),
                    NextFreeStart = ToHhMm(s.NextFreeStart),
                    Open = ToRanges(s.Open),
                    Closed = ToRanges(s.Closed),
                    Busy = ToBusyRanges(search.LocalClock, search.Day, s.Busy),
                })
                .ToList(),
        };
    }

    public async Task<SpaceDaysDto> GetSpaceDaysAsync(Guid spaceId, GetSpaceDaysInput input)
    {
        var result = await _bookingManager.GetSpaceDaysAsync(CurrentUser.GetId(), spaceId, input.From, input.To);

        return new SpaceDaysDto
        {
            Days = result.Days
                .Select(d => new SpaceDayDto
                {
                    Date = d.Date,
                    Open = ToDayRanges(result.LocalClock, d.Day, d.Open),
                    Closed = ToDayRanges(result.LocalClock, d.Day, d.Closed),
                    Busy = ToBusyRanges(result.LocalClock, d.Day, d.Busy),
                })
                .ToList(),
        };
    }

    // Minutes from the local midnight that starts the day, so the client draws a day without
    // any timezone math.
    private static int MinuteOf(BuildingClock clock, TimeRange day, DateTimeOffset utc) =>
        (int)(clock.ToLocal(utc) - clock.ToLocal(day.Start)).TotalMinutes;

    private static List<DayRangeDto> ToDayRanges(BuildingClock clock, TimeRange day, IEnumerable<TimeRange> ranges) =>
        ranges.Select(r => new DayRangeDto { StartMinute = MinuteOf(clock, day, r.Start), EndMinute = MinuteOf(clock, day, r.End) }).ToList();

    // A booking may run past the day's edges (overnight): only its part inside the day is drawn.
    private static List<DayRangeDto> ToBusyRanges(BuildingClock clock, TimeRange day, IEnumerable<BusyRange> busy) =>
        busy
            .Select(b => b.Range.ClipTo(day) is { } r
                ? new DayRangeDto { StartMinute = MinuteOf(clock, day, r.Start), EndMinute = MinuteOf(clock, day, r.End), IsMine = b.IsMine }
                : null)
            .OfType<DayRangeDto>()
            .ToList();

    /// <summary>What a result card shows of a space type: its name in the reader's language.</summary>
    private sealed record ShownSpaceType(string Name, IconKey IconKey);

    /// <summary>
    /// Every space type, with the name to show resolved once for all of them (ABP's
    /// IMultiLingualObjectManager: the request's language, else the default language's).
    /// </summary>
    private async Task<Dictionary<Guid, ShownSpaceType>> GetShownSpaceTypesAsync()
    {
        var spaceTypes = await _spaceTypeRepository.GetListAsync(includeDetails: true);
        var named = await _multiLingualObjectManager.GetBulkTranslationsAsync<SpaceType, SpaceTypeTranslation>(spaceTypes);
        return named.ToDictionary(
            pair => pair.entity.Id,
            pair => new ShownSpaceType(pair.translation?.Name ?? string.Empty, pair.entity.IconKey));
    }

    private BookableSpaceDto ToDto(Building building, Floor floor, Space space, string spaceName, ShownSpaceType spaceType)
    {
        var rules = _constraintResolver.Resolve(building, floor, space);

        var dto = ObjectMapper.Map<Space, BookableSpaceDto>(space);
        dto.Name = spaceName;
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
