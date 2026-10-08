using Dixels.Localization;
using Volo.Abp.TextTemplating;
using Volo.Abp.TextTemplating.Scriban;

namespace Dixels.Emails;

/// <summary>
/// The emails Dixels writes, as ABP text templates (Scriban). Their texts are inline
/// <c>{{ L "Email:…" }}</c> lookups in DixelsResource, so one template serves every language:
/// it's rendered in the recipient's.
/// </summary>
public static class DixelsEmailTemplates
{
    public const string Layout = "Dixels.Email.Layout";
    public const string BookingConfirmed = "Dixels.Email.BookingConfirmed";
    public const string SeriesConfirmed = "Dixels.Email.SeriesConfirmed";
    public const string BookingCancelled = "Dixels.Email.BookingCancelled";
    public const string BookingReminder = "Dixels.Email.BookingReminder";
    public const string AdminCancelled = "Dixels.Email.AdminCancelled";
    public const string Invite = "Dixels.Email.Invite";
}

public class DixelsEmailTemplateDefinitionProvider : TemplateDefinitionProvider
{
    public override void Define(ITemplateDefinitionContext context)
    {
        context.Add(Template(DixelsEmailTemplates.Layout, "Layout", isLayout: true));
        context.Add(Template(DixelsEmailTemplates.BookingConfirmed, "BookingConfirmed"));
        context.Add(Template(DixelsEmailTemplates.SeriesConfirmed, "SeriesConfirmed"));
        context.Add(Template(DixelsEmailTemplates.BookingCancelled, "BookingCancelled"));
        context.Add(Template(DixelsEmailTemplates.BookingReminder, "BookingReminder"));
        context.Add(Template(DixelsEmailTemplates.AdminCancelled, "AdminCancelled"));
        context.Add(Template(DixelsEmailTemplates.Invite, "Invite"));
    }

    private static TemplateDefinition Template(string name, string file, bool isLayout = false)
    {
        return new TemplateDefinition(
                name,
                localizationResource: typeof(DixelsResource),
                isLayout: isLayout,
                layout: isLayout ? null : DixelsEmailTemplates.Layout)
            .WithVirtualFilePath($"/Emails/Templates/{file}.tpl", isInlineLocalized: true)
            .WithScribanEngine();
    }
}
