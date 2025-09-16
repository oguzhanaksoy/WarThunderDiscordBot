using Microsoft.Extensions.Logging;

namespace ClanRatingTracker.Configuration;

public static class ConfigurationValidator
{
    public static void ValidateConfiguration(AppConfiguration config, ILogger? logger = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        logger?.LogDebug("Starting configuration validation...");

        // Validate Discord configuration
        ValidateDiscordConfiguration(config.Discord, errors, warnings, logger);
        
        // Validate Clan Tracking configuration
        ValidateClanTrackingConfiguration(config.ClanTracking, errors, warnings, logger);

        // Validate Connection Strings
        ValidateConnectionStrings(config.ConnectionStrings, errors, warnings, logger);

        // Log warnings
        foreach (var warning in warnings)
        {
            logger?.LogWarning("Configuration Warning: {Warning}", warning);
        }

        if (errors.Any())
        {
            var errorMessage = "Configuration validation failed with the following errors:\n" + 
                              string.Join("\n", errors.Select((error, index) => $"  {index + 1}. {error}")) +
                              "\n\nPlease check your appsettings.json file and ensure all required values are provided.";
            
            logger?.LogError("Configuration validation failed with {ErrorCount} errors", errors.Count);
            foreach (var error in errors)
            {
                logger?.LogError("Configuration Error: {Error}", error);
            }
            
            throw new InvalidOperationException(errorMessage);
        }

        logger?.LogInformation("Configuration validation completed successfully with {WarningCount} warnings", warnings.Count);
    }

    private static void ValidateDiscordConfiguration(DiscordConfiguration discord, List<string> errors, List<string> warnings, ILogger? logger)
    {
        logger?.LogDebug("Validating Discord configuration...");

        if (string.IsNullOrWhiteSpace(discord.Token))
        {
            errors.Add("Discord:Token is required. Please provide a valid Discord bot token.");
        }
        else if (discord.Token == "your_bot_token_here")
        {
            errors.Add("Discord:Token must be replaced with your actual Discord bot token. The placeholder value 'your_bot_token_here' is not valid.");
        }
        else if (!discord.Token.StartsWith("Bot ") && discord.Token.Length < 50)
        {
            warnings.Add("Discord:Token appears to be invalid. Discord bot tokens are typically longer than 50 characters.");
        }
        
        if (discord.ChannelId == 0)
        {
            errors.Add("Discord:ChannelId is required and must be a valid Discord channel ID (18-19 digit number).");
        }
        else if (discord.ChannelId == 123456789012345678)
        {
            errors.Add("Discord:ChannelId must be replaced with your actual Discord channel ID. The placeholder value is not valid.");
        }
        
        if (discord.RoleId == 0)
        {
            errors.Add("Discord:RoleId is required and must be a valid Discord role ID (18-19 digit number).");
        }
        else if (discord.RoleId == 987654321098765432)
        {
            errors.Add("Discord:RoleId must be replaced with your actual Discord role ID. The placeholder value is not valid.");
        }
    }

    private static void ValidateClanTrackingConfiguration(ClanTrackingConfiguration clanTracking, List<string> errors, List<string> warnings, ILogger? logger)
    {
        logger?.LogDebug("Validating Clan Tracking configuration...");

        if (string.IsNullOrWhiteSpace(clanTracking.ClanUrl))
        {
            errors.Add("ClanTracking:ClanUrl is required. Please provide a valid War Thunder clan URL.");
        }
        else if (!Uri.TryCreate(clanTracking.ClanUrl, UriKind.Absolute, out var uri) || 
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("ClanTracking:ClanUrl must be a valid HTTP or HTTPS URL.");
        }
        else if (!clanTracking.ClanUrl.Contains("warthunder.com"))
        {
            warnings.Add("ClanTracking:ClanUrl does not appear to be a War Thunder website URL. Ensure it points to a valid clan page.");
        }
        
        if (clanTracking.RetryAttempts < 1)
        {
            errors.Add("ClanTracking:RetryAttempts must be at least 1. Recommended value is 3.");
        }
        else if (clanTracking.RetryAttempts > 10)
        {
            warnings.Add("ClanTracking:RetryAttempts is set to a high value. Consider using a value between 3-5 to avoid excessive retry attempts.");
        }
        
        if (clanTracking.RetryDelayMs < 100)
        {
            errors.Add("ClanTracking:RetryDelayMs must be at least 100ms to avoid overwhelming the server. Recommended value is 1000ms.");
        }
        else if (clanTracking.RetryDelayMs > 30000)
        {
            warnings.Add("ClanTracking:RetryDelayMs is set to a high value. Consider using a value between 1000-5000ms for better performance.");
        }
    }

    private static void ValidateConnectionStrings(ConnectionStrings connectionStrings, List<string> errors, List<string> warnings, ILogger? logger)
    {
        logger?.LogDebug("Validating Connection Strings configuration...");

        if (string.IsNullOrWhiteSpace(connectionStrings.DefaultConnection))
        {
            errors.Add("ConnectionStrings:DefaultConnection is required. Please provide a valid SQLite connection string.");
        }
        else if (!connectionStrings.DefaultConnection.Contains("Data Source="))
        {
            errors.Add("ConnectionStrings:DefaultConnection must be a valid SQLite connection string containing 'Data Source='.");
        }
    }
}