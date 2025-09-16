using ClanRatingTracker.Configuration;
using ClanRatingTracker.Data;
using ClanRatingTracker.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var applicationStartTime = DateTime.UtcNow;
var applicationId = Guid.NewGuid();

try
{
    var host = CreateHostBuilder(args).Build();
    
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    
    using var applicationScope = logger.BeginScope(new Dictionary<string, object>
    {
        ["ApplicationId"] = applicationId,
        ["StartTime"] = applicationStartTime,
        ["Version"] = "1.0.0"
    });

    logger.LogInformation("Clan Rating Tracker starting with Application ID {ApplicationId}", applicationId);
    logger.LogInformation("Application version: 1.0.0, Start time: {StartTime}", applicationStartTime);

    #region Database Initialization and Health Check
    using (var scope = host.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ClanTrackingContext>();
        logger.LogInformation("Initializing database and applying migrations...");
        
        var migrationStartTime = DateTime.UtcNow;
        await context.Database.MigrateAsync();
        var migrationDuration = DateTime.UtcNow - migrationStartTime;
        
        logger.LogInformation("Database migrations applied successfully in {Duration}ms", migrationDuration.TotalMilliseconds);
        
        // Perform database health check
        logger.LogInformation("Performing database health check...");
        var healthCheckStartTime = DateTime.UtcNow;
        var isHealthy = await DatabaseHealthCheck.CheckDatabaseHealthAsync(context, logger);
        var healthCheckDuration = DateTime.UtcNow - healthCheckStartTime;
        
        if (!isHealthy)
        {
            logger.LogError("Database health check failed after {Duration}ms. Application cannot continue.", 
                healthCheckDuration.TotalMilliseconds);
            Environment.Exit(2); // Database error exit code
        }
        
        logger.LogInformation("Database health check passed in {Duration}ms", healthCheckDuration.TotalMilliseconds);
    }
    #endregion
    
    using (var scope = host.Services.CreateScope())
    {
        var trackingService = scope.ServiceProvider.GetRequiredService<IClanTrackingService>();
        logger.LogInformation("Starting clan tracking service execution...");
        
        var trackingStartTime = DateTime.UtcNow;
        await trackingService.ExecuteTrackingCycleAsync();
        var trackingDuration = DateTime.UtcNow - trackingStartTime;
        
        logger.LogInformation("Clan tracking service completed successfully in {Duration}ms", trackingDuration.TotalMilliseconds);
    }
    
    var totalDuration = DateTime.UtcNow - applicationStartTime;
    logger.LogInformation("Clan Rating Tracker completed successfully. Total execution time: {TotalDuration}ms", 
        totalDuration.TotalMilliseconds);
    
    // Ensure all logs are flushed before exit
    Log.CloseAndFlush();
    Environment.Exit(ApplicationExitCodes.SUCCESS); // Success exit code
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Configuration validation failed"))
{
    var totalDuration = DateTime.UtcNow - applicationStartTime;
    var errorMessage = $"Configuration Error after {totalDuration.TotalMilliseconds}ms: {ex.Message}";
    
    Console.WriteLine(errorMessage);
    Console.WriteLine("Please check your appsettings.json file and ensure all required configuration values are provided.");
    
    try
    {
        Log.Error(ex, "Application failed due to configuration validation error after {Duration}ms. ApplicationId: {ApplicationId}", 
            totalDuration.TotalMilliseconds, applicationId);
    }
    catch {  }
    
    Log.CloseAndFlush();
    Environment.Exit(ApplicationExitCodes.CONFIGURATION_ERROR); // Configuration error exit code
}
catch (HttpRequestException ex)
{
    var totalDuration = DateTime.UtcNow - applicationStartTime;
    var errorMessage = $"Network Error after {totalDuration.TotalMilliseconds}ms: Failed to connect to War Thunder website - {ex.Message}";
    
    Console.WriteLine(errorMessage);
    Console.WriteLine("Please check your internet connection and try again.");
    
    try
    {
        Log.Error(ex, "Application failed due to network error after {Duration}ms. ApplicationId: {ApplicationId}", 
            totalDuration.TotalMilliseconds, applicationId);
    }
    catch {  }
    
    Log.CloseAndFlush();
    Environment.Exit(ApplicationExitCodes.NETWORK_ERROR); // Network error exit code
}
catch (DbUpdateException ex)
{
    var totalDuration = DateTime.UtcNow - applicationStartTime;
    var errorMessage = $"Database Error after {totalDuration.TotalMilliseconds}ms: Failed to save data - {ex.Message}";
    
    Console.WriteLine(errorMessage);
    Console.WriteLine("Please check database permissions and disk space.");
    
    try
    {
        Log.Error(ex, "Application failed due to database error after {Duration}ms. ApplicationId: {ApplicationId}", 
            totalDuration.TotalMilliseconds, applicationId);
    }
    catch {  }
    
    Log.CloseAndFlush();
    Environment.Exit(ApplicationExitCodes.DATABASE_ERROR); // Database error exit code
}
catch (UnauthorizedAccessException ex)
{
    var totalDuration = DateTime.UtcNow - applicationStartTime;
    var errorMessage = $"Discord Authorization Error after {totalDuration.TotalMilliseconds}ms: {ex.Message}";
    
    Console.WriteLine(errorMessage);
    Console.WriteLine("Please check your Discord bot token and permissions.");
    
    try
    {
        Log.Error(ex, "Application failed due to Discord authorization error after {Duration}ms. ApplicationId: {ApplicationId}", 
            totalDuration.TotalMilliseconds, applicationId);
    }
    catch {  }
    
    Log.CloseAndFlush();
    Environment.Exit(ApplicationExitCodes.DISCORD_AUTHORIZATION_ERROR); // Discord authorization error exit code
}
catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
{
    var totalDuration = DateTime.UtcNow - applicationStartTime;
    var errorMessage = $"Timeout Error after {totalDuration.TotalMilliseconds}ms: Operation timed out - {ex.Message}";
    
    Console.WriteLine(errorMessage);
    Console.WriteLine("The operation took too long to complete. Please try again.");
    
    try
    {
        Log.Error(ex, "Application failed due to timeout after {Duration}ms. ApplicationId: {ApplicationId}", 
            totalDuration.TotalMilliseconds, applicationId);
    }
    catch {  }
    
    Log.CloseAndFlush();
    Environment.Exit(ApplicationExitCodes.TIMEOUT_ERROR); // Timeout error exit code
}
catch (Exception ex)
{
    var totalDuration = DateTime.UtcNow - applicationStartTime;
    var errorMessage = $"Fatal Error after {totalDuration.TotalMilliseconds}ms: An unexpected error occurred - {ex.Message}";
    
    Console.WriteLine(errorMessage);
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    Console.WriteLine("Please contact support if this error persists.");
    Console.WriteLine($"Application ID for support reference: {applicationId}");
    
    try
    {
        Log.Fatal(ex, "Application failed with fatal error after {Duration}ms. ApplicationId: {ApplicationId}, Error: {ErrorMessage}", 
            totalDuration.TotalMilliseconds, applicationId, ex.Message);
    }
    catch {  }
    
    Log.CloseAndFlush();
    Environment.Exit(ApplicationExitCodes.FATAL_ERROR); // General fatal error exit code
}

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((context, config) =>
        {
#if RELEASE
            config.AddJsonFile($@"{AppContext.BaseDirectory}\appsettings.json", optional: false, reloadOnChange: true);
#elif DEBUG
            config.AddJsonFile($@"{AppContext.BaseDirectory}\appsettings.Development.json", optional: false, reloadOnChange: true); 
#endif
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((context, services) =>
        {
            services.AddConfiguration(context.Configuration);
            services.AddApplicationServices(context.Configuration);
        });

static class ApplicationExitCodes
{
    public const int SUCCESS = 0;
    public const int CONFIGURATION_ERROR = 1;
    public const int DATABASE_ERROR = 2;
    public const int NETWORK_ERROR = 3;
    public const int DISCORD_AUTHORIZATION_ERROR = 4;
    public const int TIMEOUT_ERROR = 5;
    public const int FATAL_ERROR = 99;
}