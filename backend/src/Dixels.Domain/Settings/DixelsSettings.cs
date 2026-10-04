namespace Dixels.Settings;

public static class DixelsSettings
{
    private const string Prefix = "Dixels";

    /// <summary>
    /// The language a user last used the app in, saved per user (see UserLanguageManager).
    /// Emails to them are written in it.
    /// </summary>
    public const string Language = Prefix + ".Language";
}
