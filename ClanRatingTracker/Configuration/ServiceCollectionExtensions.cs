using ClanRatingTracker.Data;
using ClanRatingTracker.Interfaces;
using ClanRatingTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ClanRatingTracker.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Create a temporary logger for configuration validation
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AppConfiguration>();
        
        var appConfig = configuration.GetSection("AppConfiguration").Get<AppConfiguration>();

        if (appConfig == null)
        {
            var errorMessage = "Failed to bind AppConfiguration section from configuration. Please ensure the section exists and is properly formatted.";
            logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        services.Configure<AppConfiguration>(options =>
        {
            options.Discord = appConfig.Discord;
            options.ClanTracking = appConfig.ClanTracking;
            options.ConnectionStrings = appConfig.ConnectionStrings;
            
            // Override Discord token from environment variable if available
            var envToken = configuration["DISCORD_BOT_TOKEN"];
            if (!string.IsNullOrEmpty(envToken))
            {
                options.Discord.Token = envToken;
            }
        });

        // Validate configuration
        ConfigurationValidator.ValidateConfiguration(appConfig, logger);
        
        services.AddSingleton(appConfig);
        
        return services;
    }
    
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("Application", "ClanRatingTracker")
            .Enrich.WithProperty("Version", "1.0.0")
            .CreateLogger();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger);
        });
        
        // Add HttpClient for web scraping
        services.AddHttpClient();
        
        // Add Entity Framework with SQLite
        services.AddDbContext<ClanTrackingContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));
        
        // Register repository
        services.AddScoped<IClanRepository, ClanRepository>();
        
        // Register web scraper service
        services.AddScoped<IWebScraper, WarThunderWebScraper>();
        
        // Register Discord service
        services.AddScoped<IDiscordService, DiscordService>();
        
        // Register clan tracking service
        services.AddScoped<IClanTrackingService, ClanTrackingService>();
        
        return services;
    }
}