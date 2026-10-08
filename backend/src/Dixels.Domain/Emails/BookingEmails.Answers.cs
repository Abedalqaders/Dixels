using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Microsoft.Extensions.Logging;

namespace Dixels.Emails;

/// <summary>
/// Guests' answers by email: the Accept / Decline links in their invite (their own secret link,
/// see <see cref="GuestLinks"/>, opening the public answer page), and the booker's "X can't make
/// it" email when a guest declines, however the answer came in.
/// </summary>
public partial class BookingEmails
{
    /// <summary>The guest's Accept / Decline links on the public answer page.</summary>
    private void AddAnswerLinks(BookingEmailModel model, InviteeRow guest)
    {
        // Looked up here rather than injected: only guests' emails need it.
        var token = LazyServiceProvider.LazyGetRequiredService<GuestLinks>().TokenFor(guest);
        model.AcceptUrl = AppLink($"/rsvp/{token}?answer=accepted");
        model.DeclineUrl = AppLink($"/rsvp/{token}?answer=declined");
    }

    /// <summary>
    /// "Rana Haddad can't make it" to the booker, in their language: one email for a whole series'
    /// answer, not one per date. Sent when the answer turns to Declined (see GuestAnswerEmailHandler).
    /// </summary>
    public async Task SendGuestDeclinedAsync(BookingInviteeRespondedEvent answer)
    {
        try
        {
            var guestName = answer.Guest.UserId is { } guestId
                ? (await _userRepository.FindAsync(guestId, includeDetails: false)) is { } user ? DisplayName(user) : null
                : string.IsNullOrWhiteSpace(answer.Guest.Name) ? answer.Guest.Email : answer.Guest.Name;
            if (guestName is null)
            {
                return;
            }

            if (answer.BookingId is { } bookingId)
            {
                var booking = await _bookingRepository.GetAsync(bookingId, includeDetails: false);
                await SendAsync(booking.UserId, booking.SpaceId, DixelsEmailTemplates.GuestDeclined, (model, clock) =>
                {
                    Describe(model, clock, booking);
                    return Declined(model, guestName);
                });
                return;
            }

            var series = await _seriesRepository.GetAsync(answer.SeriesId!.Value, includeDetails: false);
            var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
            var seriesId = series.Id;
            var next = (await _bookingRepository.GetListAsync(b => b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed && b.StartsAt > now))
                .MinBy(b => b.StartsAt);
            if (next is null)
            {
                return;
            }

            await SendAsync(series.UserId, series.SpaceId, DixelsEmailTemplates.GuestDeclined, (model, clock) =>
            {
                Describe(model, clock, next);
                model.Title = series.Title;
                model.Rows[0].Date = RepeatText(series.Rule, series.FirstDate);
                model.RepeatsFrom = _localizer["Email:RepeatsFrom", model.Date];
                model.IsSeries = true;
                return Declined(model, guestName);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not write the 'guest declined' email for booking {BookingId} / series {SeriesId}.", answer.BookingId, answer.SeriesId);
        }
    }

    private string Declined(BookingEmailModel model, string guestName)
    {
        model.Status = EmailStatus.Declined;
        model.AnsweredBy = guestName;
        model.Heading = _localizer["Email:GuestDeclined:Heading", guestName, TitleOrRoom(model)];
        return model.IsSeries
            ? _localizer["Email:GuestDeclined:SubjectSeries", guestName, TitleOrRoom(model)]
            : _localizer["Email:GuestDeclined:Subject", guestName, TitleOrRoom(model), model.Date];
    }
}
