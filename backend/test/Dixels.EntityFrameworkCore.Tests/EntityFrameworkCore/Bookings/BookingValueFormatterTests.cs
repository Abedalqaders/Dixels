using System;
using System.Collections.Generic;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Volo.Abp.Localization;
using Xunit;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// The values quoted inside a booking message come out in the reader's language — a closed
/// Friday reads "غير مفتوحة يوم الجمعة" in Arabic, not "غير مفتوحة يوم Friday".
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BookingValueFormatterTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private static readonly OperatingDays SunToThu = OperatingDays.FromDayOfWeeks(new[]
    {
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
    });

    private readonly BookingValueFormatter _formatter;
    private readonly BookingViolationLocalizer _violationLocalizer;

    public BookingValueFormatterTests()
    {
        _formatter = GetRequiredService<BookingValueFormatter>();
        _violationLocalizer = GetRequiredService<BookingViolationLocalizer>();
    }

    [Fact]
    public void English_reads_as_before()
    {
        using var _ = CultureHelper.Use("en");

        _formatter.Format(DayOfWeek.Friday).ShouldBe("Friday");
        _formatter.Format(SunToThu).ShouldBe("Sun–Thu");
        _formatter.Format(OperatingDays.FromDayOfWeeks(new[] { DayOfWeek.Sunday, DayOfWeek.Tuesday })).ShouldBe("Sun, Tue");
        _formatter.Format(OperatingDays.Everyday).ShouldBe("every day");
        _formatter.Format(OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0))).ShouldBe("07:00–20:00");
        _formatter.Format(OperatingWindow.FullDay).ShouldBe("24 hours");
        _formatter.Format(TimeSpan.FromMinutes(45)).ShouldBe("45 min");
        _formatter.Format(TimeSpan.FromHours(2)).ShouldBe("2h");
        _formatter.Format(TimeSpan.FromMinutes(150)).ShouldBe("2h 30m");
        _formatter.Format(new DateOnly(2026, 10, 12)).ShouldBe("Mon 12 Oct 2026");
        _formatter.Format(new DateTime(2026, 9, 29, 14, 30, 0)).ShouldBe("Tue 29 Sep 14:30");
        _formatter.Format(ReasonCategory.Holiday).ShouldBe("Holiday");
        _formatter.Format(8).ShouldBe("8");
    }

    [Fact]
    public void Arabic_words_every_value_in_arabic_with_western_digits()
    {
        using var _ = CultureHelper.Use("ar");

        _formatter.Format(DayOfWeek.Sunday).ShouldBe("الأحد");
        _formatter.Format(DayOfWeek.Friday).ShouldBe("الجمعة");
        _formatter.Format(SunToThu).ShouldBe("الأحد إلى الخميس");
        _formatter.Format(OperatingDays.FromDayOfWeeks(new[] { DayOfWeek.Sunday, DayOfWeek.Tuesday })).ShouldBe("الأحد، الثلاثاء");
        _formatter.Format(OperatingDays.Everyday).ShouldBe("كل يوم");
        // Isolated left to right (LRI…PDI), or it would show as "20:00–07:00" in an Arabic sentence.
        _formatter.Format(OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0))).ShouldBe("⁦07:00–20:00⁩");
        _formatter.Format(OperatingWindow.FullDay).ShouldBe("24 ساعة");
        _formatter.Format(new DateOnly(2026, 10, 12)).ShouldBe("الاثنين 12 أكتوبر 2026");
        _formatter.Format(ReasonCategory.Holiday).ShouldBe("عطلة");
    }

    [Theory]
    [InlineData(1, "دقيقة واحدة")]
    [InlineData(2, "دقيقتين")]
    [InlineData(5, "5 دقائق")]
    [InlineData(45, "45 دقيقة")]
    [InlineData(60, "ساعة")]
    [InlineData(120, "ساعتين")]
    [InlineData(180, "3 ساعات")]
    [InlineData(150, "ساعتين و30 دقيقة")]
    public void Arabic_durations_take_the_right_plural_form(int minutes, string expected)
    {
        using var _ = CultureHelper.Use("ar");

        _formatter.Format(TimeSpan.FromMinutes(minutes)).ShouldBe(expected);
    }

    [Fact]
    public void A_closed_day_message_names_the_day_in_the_readers_language()
    {
        var violation = new BookingViolation(
            DixelsDomainErrorCodes.BookingClosedDay,
            ConstraintSource.Floor,
            new Dictionary<string, object> { ["day"] = DayOfWeek.Friday, ["openDays"] = SunToThu });

        using (CultureHelper.Use("ar"))
        {
            var dto = _violationLocalizer.ToDto(violation);
            dto.ShortMessage.ShouldBe("غير مفتوحة يوم الجمعة");
            dto.Message.ShouldBe("هذه المساحة غير مفتوحة يوم الجمعة (قاعدة الطابق، أيام الفتح: الأحد إلى الخميس). اختر يومًا آخر.");
        }

        using (CultureHelper.Use("en"))
        {
            _violationLocalizer.ToDto(violation).ShortMessage.ShouldBe("Not open on Friday");
        }
    }
}
