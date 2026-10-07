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
    /// Whether bookings may invite people from outside by email. Off until the invite email
    /// exists: otherwise an outsider would be saved as a guest but never told.
    /// </summary>
    public const string ExternalGuestsEnabled = Prefix + ".Bookings.ExternalGuestsEnabled";
}
