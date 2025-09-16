namespace ClanRatingTracker.Configuration;

public class AppConfiguration
{
    public DiscordConfiguration Discord { get; set; } = new();
    public ClanTrackingConfiguration ClanTracking { get; set; } = new();
    public ConnectionStrings ConnectionStrings { get; set; } = new();
}

public class DiscordConfiguration
{
    public string Token { get; set; } = string.Empty;
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong RoleId { get; set; }
}

public class ClanTrackingConfiguration
{
    public string ClanUrl { get; set; } = string.Empty;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = string.Empty;
}