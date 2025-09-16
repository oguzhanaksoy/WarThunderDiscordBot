using ClanRatingTracker.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClanRatingTracker.Data;

public class ClanTrackingContextFactory : IDesignTimeDbContextFactory<ClanTrackingContext>
{
    public ClanTrackingContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        ILogger logger = loggerFactory.CreateLogger<ClanTrackingContextFactory>();

        var appConfig = configuration.GetSection("AppConfiguration").Get<AppConfiguration>();
        if (appConfig == null)
        {
            throw new InvalidOperationException("Failed to bind AppConfiguration section from configuration. Please ensure the section exists and is properly formatted.");
        }
        ConfigurationValidator.ValidateConfiguration(appConfig, logger);

        var optionsBuilder = new DbContextOptionsBuilder<ClanTrackingContext>();
        optionsBuilder.UseSqlite(appConfig.ConnectionStrings.DefaultConnection);

        return new ClanTrackingContext(optionsBuilder.Options);
    }
}