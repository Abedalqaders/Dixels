namespace Dixels;

/// <summary>
/// The remote-service name and route area shared by every HTTP API controller, and by the
/// client proxies in Dixels.HttpApi.Client, which look services up by this name.
/// </summary>
public static class DixelsRemoteServiceConsts
{
    public const string RemoteServiceName = "Default";

    /// <summary>The "app" in <c>/api/app/...</c>.</summary>
    public const string ModuleName = "app";
}
