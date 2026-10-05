using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Dixels.Web.Components.AccountTheme;

/*
 * Light/dark for ABP's own sign-in pages (login, register, forgot password: the "Account"
 * layout), the same two themes as the web app. Those pages are on the API's address, not the
 * app's, so they can't read the theme the app saved; the app sends it along instead, as
 * ui_theme on the sign-in request (see frontend features/auth/signIn.ts). Registered as
 * layout hooks in DixelsWebModule; the colours are in wwwroot/global-styles.css.
 */

/// <summary>In the head: picks the theme before the page paints, so it never flashes light first.</summary>
public class AccountThemeHeadViewComponent : AbpViewComponent
{
    public IViewComponentResult Invoke() => View("~/Components/AccountTheme/Head.cshtml");
}

/// <summary>At the end of the body: the light/dark button in the top corner.</summary>
public class AccountThemeToggleViewComponent : AbpViewComponent
{
    public IViewComponentResult Invoke() => View("~/Components/AccountTheme/Toggle.cshtml");
}
