namespace Dixels.Emails;

/// <summary>
/// Install-time email settings, from the <c>"Emails"</c> section of appsettings (or
/// <c>Emails__AppUrl</c> in the environment). Which mail server sends them is ABP's own
/// Abp.Mailing.* settings.
/// </summary>
public class EmailOptions
{
    /// <summary>The web app's address, no trailing slash: the "Open Dixels" button links into it.</summary>
    public string AppUrl { get; set; } = "http://localhost:5173";
}
