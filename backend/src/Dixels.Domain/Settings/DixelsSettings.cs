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

    /// <summary>
    /// Whether guests' invites are real meeting requests (METHOD:REQUEST), so their mail app
    /// shows its own Accept / Decline, and the answers are read back from the rsvp@ mailbox
    /// (<see cref="Emails.Rsvp.RsvpMailboxWorker"/>). Off: invites stay "add to calendar"
    /// (PUBLISH), since an answer sent to a mailbox nobody reads would be lost. On in Development.
    /// </summary>
    public const string RsvpMailboxEnabled = Prefix + ".Emails.RsvpMailbox.Enabled";

    /// <summary>How the rsvp@ mailbox is read (IMAP). The password comes from the environment only.</summary>
    public const string RsvpMailboxImapHost = Prefix + ".Emails.RsvpMailbox.Imap.Host";
    public const string RsvpMailboxImapPort = Prefix + ".Emails.RsvpMailbox.Imap.Port";
    public const string RsvpMailboxImapUseSsl = Prefix + ".Emails.RsvpMailbox.Imap.UseSsl";
    public const string RsvpMailboxImapUserName = Prefix + ".Emails.RsvpMailbox.Imap.UserName";
    public const string RsvpMailboxImapPassword = Prefix + ".Emails.RsvpMailbox.Imap.Password";

    /// <summary>
    /// How many days outside guests' details (email, name) are kept after the booking ends or is
    /// cancelled; then <see cref="Bookings.ExternalGuestCleanup"/> deletes them. 0 or less keeps them.
    /// </summary>
    public const string ExternalGuestRetentionDays = Prefix + ".Bookings.ExternalGuestRetentionDays";
}
