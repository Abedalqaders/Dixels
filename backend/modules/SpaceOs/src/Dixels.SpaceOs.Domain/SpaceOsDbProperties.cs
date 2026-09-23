namespace Dixels.SpaceOs;

public static class SpaceOsDbProperties
{
    public static string DbTablePrefix { get; set; } = "SpaceOs";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "SpaceOs";
}
