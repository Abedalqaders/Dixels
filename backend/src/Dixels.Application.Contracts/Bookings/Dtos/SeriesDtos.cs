using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Timing;

namespace Dixels.Bookings;

/// <summary>How a booking repeats — the same shape Teams' "Custom" dialog asks for.</summary>
public class RecurrenceDto
{
    public RecurrenceFrequency Frequency { get; set; }

    [Range(1, 99)]
    public int Interval { get; set; } = 1;

    /// <summary>Weekly only: days to repeat on, 0 = Sunday … 6 = Saturday.</summary>
    public int[] Weekdays { get; set; } = Array.Empty<int>();

    public MonthlyRepeat MonthlyRepeat { get; set; }

    /// <summary>The last date an occurrence may fall on (building-local, inclusive).</summary>
    public DateOnly EndDate { get; set; }
}

/// <summary>A recurring booking request: the first occurrence (as for a single booking) and how it repeats.</summary>
public class SeriesRequestDto : BookingRequestDto
{
    [Required]
    public RecurrenceDto Recurrence { get; set; } = new();
}

public class CreateSeriesDto : SeriesRequestDto
{
    /// <summary>Dates from the preview the person unticked — they're not booked.</summary>
    public List<DateOnly> SkipDates { get; set; } = new();

    [Required]
    [StringLength(BookingConsts.MaxSeriesIdempotencyKeyLength)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

/// <summary>One date of a previewed series.</summary>
public class OccurrencePreviewDto
{
    public DateOnly Date { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }

    public bool IsValid { get; set; }
    public List<BookingViolationDto> Violations { get; set; } = new();
    public List<BookingViolationDto> Warnings { get; set; } = new();
}

public class SeriesPreviewDto
{
    /// <summary>Problems that hit every date alike (too many people, too long) — none of it can be booked until they're fixed.</summary>
    public List<BookingViolationDto> SeriesViolations { get; set; } = new();

    public List<OccurrencePreviewDto> Occurrences { get; set; } = new();

    public int BookableCount { get; set; }
    public string Timezone { get; set; } = string.Empty;
}

public class SeriesCreatedDto
{
    public Guid SeriesId { get; set; }
    public List<BookingDto> Bookings { get; set; } = new();
}
