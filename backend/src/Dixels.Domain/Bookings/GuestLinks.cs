using System;
using System.Buffers.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Bookings;

/// <summary>
/// Install-time settings for <see cref="GuestLinks"/>, from the <c>"GuestLinks"</c> section
/// (<c>GuestLinks__Key</c> in the environment).
/// </summary>
public class GuestLinkOptions
{
    /// <summary>
    /// The secret every guest link is signed with. Required outside Development (the API host
    /// refuses to start without it). Changing it makes every link already sent stop working.
    /// </summary>
    public string? Key { get; set; }
}

/// <summary>What a guest link opens: the guest's row, on a booking or on a series.</summary>
public sealed record GuestLinkTarget(InviteeRow Row, Booking? Booking, BookingSeries? Series);

/// <summary>
/// A guest's own secret link (the answer buttons in their emails, the public answer page): no
/// sign-in, anyone holding it acts as that guest. Nothing is stored for it. The link is the
/// row's id plus a keyed hash (HMAC-SHA256) of it and the row's secret
/// <see cref="InviteeRow.IcsUid"/>, so every email can carry the same link, the database alone
/// can't make one, and taking the guest off the list (their row goes) ends it.
///
/// Shape: <c>b.&lt;row&gt;.&lt;mac&gt;</c> for a booking's guest, <c>s.…</c> for a series' (both
/// base64url). Expiry isn't in the link: the answer rules close it once the meeting starts.
/// </summary>
public class GuestLinks : DomainService
{
    private const string BookingKind = "b";
    private const string SeriesKind = "s";

    /// <summary>Used when no key is configured, which only Development allows.</summary>
    internal const string DevelopmentKey = "dixels-development-guest-links-key";

    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<BookingSeries, Guid> _seriesRepository;
    private readonly GuestLinkOptions _options;
    private byte[]? _key;
    private static bool _warnedAboutDevelopmentKey;

    public GuestLinks(
        IBookingRepository bookingRepository,
        IRepository<BookingSeries, Guid> seriesRepository,
        IOptions<GuestLinkOptions> options)
    {
        _bookingRepository = bookingRepository;
        _seriesRepository = seriesRepository;
        _options = options.Value;
    }

    /// <summary>The guest's link token, the same every time it's asked for.</summary>
    public string TokenFor(InviteeRow row)
    {
        var kind = row is BookingSeriesAttendee ? SeriesKind : BookingKind;
        return $"{kind}.{Base64Url.EncodeToString(row.Id.ToByteArray())}.{Base64Url.EncodeToString(Mac(kind, row))}";
    }

    /// <summary>
    /// The guest the token belongs to, with their booking or series (and its guests); null for
    /// a malformed or forged token, or a guest no longer on the list.
    /// </summary>
    public async Task<GuestLinkTarget?> ResolveAsync(string? token)
    {
        if (!TryParse(token, out var kind, out var rowId, out var mac))
        {
            return null;
        }

        GuestLinkTarget? target;
        if (kind == BookingKind)
        {
            var booking = await _bookingRepository.FindAsync(b => b.Invitees.Any(i => i.Id == rowId), includeDetails: true);
            target = booking is null ? null : new GuestLinkTarget(booking.Invitees.Single(i => i.Id == rowId), booking, null);
        }
        else
        {
            var series = await _seriesRepository.FindAsync(s => s.Invitees.Any(i => i.Id == rowId), includeDetails: true);
            target = series is null ? null : new GuestLinkTarget(series.Invitees.Single(i => i.Id == rowId), null, series);
        }

        // Fixed-time compare: how long a wrong guess takes says nothing about how close it was.
        return target is not null && CryptographicOperations.FixedTimeEquals(Mac(kind, target.Row), mac) ? target : null;
    }

    private static bool TryParse(string? token, out string kind, out Guid rowId, out byte[] mac)
    {
        kind = string.Empty;
        rowId = Guid.Empty;
        mac = Array.Empty<byte>();

        var parts = token?.Split('.');
        if (parts is not { Length: 3 } || (parts[0] != BookingKind && parts[0] != SeriesKind))
        {
            return false;
        }

        try
        {
            var id = Base64Url.DecodeFromChars(parts[1]);
            mac = Base64Url.DecodeFromChars(parts[2]);
            if (id.Length != 16 || mac.Length != 32)
            {
                return false;
            }

            kind = parts[0];
            rowId = new Guid(id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private byte[] Mac(string kind, InviteeRow row) =>
        HMACSHA256.HashData(Key(), Encoding.UTF8.GetBytes($"{kind}|{row.Id:N}|{row.IcsUid}"));

    private byte[] Key()
    {
        if (_key is not null)
        {
            return _key;
        }

        var secret = _options.Key;
        if (string.IsNullOrWhiteSpace(secret))
        {
            // The API host refuses to start without a key outside Development (DixelsWebModule).
            if (!_warnedAboutDevelopmentKey)
            {
                _warnedAboutDevelopmentKey = true;
                Logger.LogWarning("GuestLinks:Key is not set: guest links are signed with the development key.");
            }

            secret = DevelopmentKey;
        }

        // Any length of secret becomes a full-size HMAC key.
        return _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }
}
