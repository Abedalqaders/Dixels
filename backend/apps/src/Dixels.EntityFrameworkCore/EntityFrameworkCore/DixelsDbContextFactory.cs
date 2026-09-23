using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Dixels.EntityFrameworkCore;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands) */
public class DixelsDbContextFactory : IDesignTimeDbContextFactory<DixelsDbContext>
{
    public DixelsDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        
        DixelsEfCoreEntityExtensionMappings.Configure();

        var builder = new DbContextOptionsBuilder<DixelsDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));
        
        return new DixelsDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Dixels.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}
