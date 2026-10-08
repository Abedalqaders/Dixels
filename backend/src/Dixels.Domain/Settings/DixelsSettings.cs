namespace Dixels.Settings;

public static class DixelsSettings
{
    private const string Prefix = "Dixels";

    /// <summary>
    /// The language a user last used the app in, saved per user (see UserLanguageManager).
    /// Emails to them are written in it.
    /// </summary>
    public const string Language = Prefix + ".Language";

    /// <summary>
    /// Whether bookings may invite people from outside by email. On by default now that every
    /// guest gets an invite email; an admin can still turn it off.
    /// </summary>
    public const string ExternalGuestsEnabled = Prefix + ".Bookings.ExternalGuestsEnabled";

    /// <summary>
    /// The mailbox guests' calendar invites come from (their ORGANIZER, shown as "{booker} (via
    /// Dixels)"). Answers to invites will be read from it (E6); the dev default is smtp4dev's.
    /// </summary>
    public const string RsvpMailboxAddress = Prefix + ".Emails.RsvpMailbox.Address";
}
