namespace Dixels.Web.RateLimiting;

/// <summary>
/// Bound from the "RateLimiting" configuration section (env vars: RateLimiting__Api__PermitLimit etc.).
/// Every limit is a fixed window: at most PermitLimit requests per WindowSeconds, per partition.
/// </summary>
public class DixelsRateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// /api and /connect (the OIDC token endpoint). Partitioned per signed-in user, or per client
    /// IP when anonymous. Generous: a page load fires several calls, and a whole office may sit
    /// behind one IP address.
    /// </summary>
    public DixelsRateLimitWindow Api { get; set; } = new() { PermitLimit = 600, WindowSeconds = 60 };

    /// <summary>
    /// Form posts under /Account (sign in, register, forgot/reset password), per client IP.
    /// Slows password guessing down; per-account lockout (ABP Identity) handles a single account.
    /// </summary>
    public DixelsRateLimitWindow Account { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>
    /// The public answer page's calls (/api/app/rsvp), per client IP. Anyone may call them without
    /// signing in; a guest answers a handful of times, so this is tight.
    /// </summary>
    public DixelsRateLimitWindow GuestLinks { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
}

public class DixelsRateLimitWindow
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; }
}
