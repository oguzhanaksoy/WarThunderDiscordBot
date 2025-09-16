using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClanRatingTracker.Data;

public static class DatabaseHealthCheck
{
    public static async Task<bool> CheckDatabaseHealthAsync(ClanTrackingContext context, ILogger logger)
    {
        try
        {
            // Test database connection by executing a simple query
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                logger.LogError("Cannot connect to the database");
                return false;
            }

            // Check if the ClanMembers table exists and is accessible
            var memberCount = await context.ClanMembers.CountAsync();
            logger.LogInformation("Database health check passed. Current member records: {Count}", memberCount);
            
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database health check failed");
            return false;
        }
    }
}