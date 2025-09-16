using Discord;
using Discord.WebSocket;
using ClanRatingTracker.Configuration;
using ClanRatingTracker.Interfaces;
using ClanRatingTracker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace ClanRatingTracker.Services;

public class DiscordService : IDiscordService, IDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly AppConfiguration _config;
    private readonly ILogger<DiscordService> _logger;
    private bool _isInitialized = false;
    private bool _disposed = false;

    public DiscordService(IOptions<AppConfiguration> config, ILogger<DiscordService> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config.Value;
        _logger = logger;

        var clientConfig = new DiscordSocketConfig
        {
            LogLevel = LogSeverity.Info,
            MessageCacheSize = 100,
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMembers
        };

        _client = new DiscordSocketClient(clientConfig);
        _client.Log += LogAsync;
    }

    private Task LogAsync(LogMessage log)
    {
        var logLevel = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, log.Exception, "[Discord.Net] {Message}", log.Message);
        return Task.CompletedTask;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        if (string.IsNullOrEmpty(_config.Discord.Token))
        {
            throw new InvalidOperationException("Discord token is not configured");
        }

        await _client.LoginAsync(TokenType.Bot, _config.Discord.Token);
        await _client.StartAsync();

        // Wait for the client to be ready
        var readyTaskSource = new TaskCompletionSource<bool>();
        _client.Ready += () =>
        {
            readyTaskSource.SetResult(true);
            return Task.CompletedTask;
        };

        await readyTaskSource.Task;
        _isInitialized = true;

        _logger.LogInformation("Discord client initialized successfully");
    }

    public async Task PublishRatingChangesAsync(List<RatingChange> changes)
    {
        if (changes == null)
        {
            throw new ArgumentNullException(nameof(changes));
        }

        await EnsureInitializedAsync();

        var message = FormatRatingChangesMessage(changes);

        await ExecuteWithRetryAsync(async () =>
        {
            var targetChannel = GetTextChannel();
            await targetChannel.SendMessageAsync(message);
            _logger.LogInformation("Published rating changes to Discord channel {ChannelId}", _config.Discord.ChannelId);
        }, nameof(PublishRatingChangesAsync));
    }

    public async Task PublishInitialMemberDataAsync(List<RatingChange> initialData)
    {
        if (initialData == null)
        {
            throw new ArgumentNullException(nameof(initialData));
        }

        await EnsureInitializedAsync();

        var message = FormatInitialMemberDataMessage(initialData);

        await ExecuteWithRetryAsync(async () =>
        {
            var targetChannel = GetTextChannel();
            await targetChannel.SendMessageAsync(message);
            _logger.LogInformation("Published initial member data to Discord channel {ChannelId}", _config.Discord.ChannelId);
        }, nameof(PublishInitialMemberDataAsync));
    }

    public async Task RemoveRoleFromUserAsync(string username, ulong roleId)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        await EnsureInitializedAsync();

        await ExecuteWithRetryAsync(async () =>
        {
            // Filter guilds if GuildId is specified (non-zero)

            var guild = GetGuild();
            await guild.DownloadUsersAsync();
            var user = guild.Users.FirstOrDefault(u =>
                    u.Username.Contains(username, StringComparison.OrdinalIgnoreCase) ||
                    u.DisplayName.Contains(username, StringComparison.OrdinalIgnoreCase)
            );

            if (user == null)
            {
                _logger.LogWarning("User {Username} not found in guild {GuildName}", username, guild.Name);
                return;
            }
            var role = guild.GetRole(roleId);
            if (role == null)
            {
                _logger.LogWarning("Role with ID {RoleId} not found in guild {GuildName}", roleId, guild.Name);
                return;
            }
            if (!user.Roles.Contains(role))
            {
                _logger.LogInformation("User {Username} does not have role {RoleName} in guild {GuildName}",
                    username, role.Name, guild.Name);
                return;
            }

            await user.RemoveRoleAsync(role);
            _logger.LogInformation("Removed role {RoleName} from user {Username} in guild {GuildName}",
                role.Name, username, guild.Name);
        }, nameof(RemoveRoleFromUserAsync));
    }

    public async Task AssignRoleToUserAsync(string username, ulong roleId)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        await EnsureInitializedAsync();

        await ExecuteWithRetryAsync(async () =>
        {
            var guild = GetGuild();
            await guild.DownloadUsersAsync();
            var user = guild.Users.FirstOrDefault(u =>
                    u.Username.Contains(username, StringComparison.OrdinalIgnoreCase) ||
                    u.DisplayName.Contains(username, StringComparison.OrdinalIgnoreCase)
            );

            if (user == null)
            {
                _logger.LogWarning("User {Username} not found in guild {GuildName}", username, guild.Name);
                return;
            }
            var role = guild.GetRole(roleId);
            if (role == null)
            {
                _logger.LogWarning("Role with ID {RoleId} not found in guild {GuildName}", roleId, guild.Name);
                return;
            }
            if (user.Roles.Contains(role))
            {

                _logger.LogInformation("User {Username} already has role {RoleName} in guild {GuildName}",
                    username, role.Name, guild.Name);
                return;
            }
            await user.AddRoleAsync(role);
            _logger.LogInformation("Assigned role {RoleName} to user {Username} in guild {GuildName}",
                role.Name, username, guild.Name);

            // Audit log for role assignment
            _logger.LogWarning("AUDIT: Successfully assigned role {RoleName} to new clan member: {Username}. " +
                                "Action completed at {Timestamp}", role.Name, username, DateTime.UtcNow);
        }, nameof(AssignRoleToUserAsync));
    }

    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or empty", nameof(message));
        }
        await EnsureInitializedAsync();
        await ExecuteWithRetryAsync(async () =>
        {
            var targetChannel = GetTextChannel();
            await targetChannel.SendMessageAsync(message);
            _logger.LogInformation("Sent message to Discord channel {ChannelId}", _config.Discord.ChannelId);
        }, nameof(SendMessageAsync));
    }

    private string FormatRatingChangesMessage(List<RatingChange> changes)
    {
        if (changes.Count == 0)
        {
            return "📊 **Daily Clan Rating Update**\n\nNo rating changes detected today.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("📊 **Daily Clan Rating Update**");
        sb.AppendLine();

        var increasedRatings = changes.Where(c => c.Change > 0).OrderByDescending(c => c.Change).ToList();
        var decreasedRatings = changes.Where(c => c.Change < 0).OrderBy(c => c.Change).ToList();
        var noChange = changes.Where(c => c.Change == 0).ToList();

        if (increasedRatings.Any())
        {
            sb.AppendLine("📈 **Rating Increases:**");
            foreach (var change in increasedRatings)
            {
                sb.AppendLine($"• **{change.Username}**: {change.OldRating} → {change.NewRating} (+{change.Change})");
            }
            sb.AppendLine();
        }

        if (decreasedRatings.Any())
        {
            sb.AppendLine("📉 **Rating Decreases:**");
            foreach (var change in decreasedRatings)
            {
                sb.AppendLine($"• **{change.Username}**: {change.OldRating} → {change.NewRating} ({change.Change})");
            }
            sb.AppendLine();
        }

        if (noChange.Any())
        {
            sb.AppendLine("➖ **No Change:**");
            foreach (var change in noChange)
            {
                sb.AppendLine($"• **{change.Username}**: {change.NewRating}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"*Updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*");

        return sb.ToString();
    }

    private string FormatInitialMemberDataMessage(List<RatingChange> initialData)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎯 **Initial Clan Member Data**");
        sb.AppendLine($"*Tracking started for {initialData.Count} clan members*");
        sb.AppendLine();

        // Sort members by rating (highest first)
        var sortedMembers = initialData.OrderByDescending(m => m.NewRating).ToList();

        // Group members by rating ranges for better readability
        var highRating = sortedMembers.Where(m => m.NewRating >= 2000).ToList();
        var mediumRating = sortedMembers.Where(m => m.NewRating >= 1000 && m.NewRating < 2000).ToList();
        var lowRating = sortedMembers.Where(m => m.NewRating < 1000).ToList();

        if (highRating.Any())
        {
            sb.AppendLine("🏆 **High Rating Members (2000+):**");
            foreach (var member in highRating.Take(10)) // Limit to top 10 to avoid message length issues
            {
                sb.AppendLine($"• **{member.Username}**: {member.NewRating}");
            }
            if (highRating.Count > 10)
            {
                sb.AppendLine($"• *... and {highRating.Count - 10} more high-rating members*");
            }
            sb.AppendLine();
        }

        if (mediumRating.Any())
        {
            sb.AppendLine("⭐ **Medium Rating Members (1000-1999):**");
            foreach (var member in mediumRating.Take(15)) // Show more medium rating members
            {
                sb.AppendLine($"• **{member.Username}**: {member.NewRating}");
            }
            if (mediumRating.Count > 15)
            {
                sb.AppendLine($"• *... and {mediumRating.Count - 15} more medium-rating members*");
            }
            sb.AppendLine();
        }

        if (lowRating.Any())
        {
            sb.AppendLine("🌟 **Developing Members (<1000):**");
            foreach (var member in lowRating.Take(10))
            {
                sb.AppendLine($"• **{member.Username}**: {member.NewRating}");
            }
            if (lowRating.Count > 10)
            {
                sb.AppendLine($"• *... and {lowRating.Count - 10} more developing members*");
            }
            sb.AppendLine();
        }

        // Add summary statistics
        var avgRating = (int)sortedMembers.Average(m => m.NewRating);
        var maxRating = sortedMembers.Max(m => m.NewRating);
        var minRating = sortedMembers.Min(m => m.NewRating);

        sb.AppendLine("📊 **Clan Statistics:**");
        sb.AppendLine($"• **Total Members**: {initialData.Count}");
        sb.AppendLine($"• **Average Rating**: {avgRating}");
        sb.AppendLine($"• **Highest Rating**: {maxRating} ({sortedMembers.First().Username})");
        sb.AppendLine($"• **Lowest Rating**: {minRating} ({sortedMembers.Last().Username})");
        sb.AppendLine();

        sb.AppendLine($"*Clan tracking initialized: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC*");
        sb.AppendLine("*Future updates will show rating changes from this baseline.*");

        return sb.ToString();
    }

    private async Task ExecuteWithRetryAsync(Func<Task> operation, string operationName)
    {
        var retryAttempts = _config.ClanTracking.RetryAttempts;
        var retryDelay = _config.ClanTracking.RetryDelayMs;

        for (int attempt = 1; attempt <= retryAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < retryAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(retryDelay * Math.Pow(2, attempt - 1));
                _logger.LogWarning(ex, "Discord operation {OperationName} failed on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}ms",
                    operationName, attempt, retryAttempts, delay.TotalMilliseconds);

                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord operation {OperationName} failed after {MaxAttempts} attempts",
                    operationName, retryAttempts);
                throw;
            }
        }
    }

    private SocketTextChannel GetTextChannel()
    {
        SocketGuild guild = GetGuild();
        var channelId = _config.Discord.ChannelId;
        if (channelId == 0)
        {
            throw new InvalidOperationException("ChannelId must be specified in configuration to get a specific channel.");
        }
        var channel = guild.GetTextChannel(channelId);
        if (channel == null)
        {
            var textChannels = guild.TextChannels.Select(c => $"#{c.Name} ({c.Id})").Take(10);
            throw new InvalidOperationException($"Channel with ID {channelId} not found in guild {guild.Name}. Available text channels: {string.Join(", ", textChannels)}");
        }
        else
        {
            _logger.LogDebug("Target channel #{ChannelName} ({ChannelId}) found in guild {GuildName} ({GuildId})",
                channel.Name, channel.Id, channel.Guild.Name, channel.Guild.Id);
        }
        return channel;

    }

    private SocketGuild GetGuild()
    {
        var guildId = _config.Discord.GuildId;
        if (guildId == 0)
        {
            throw new InvalidOperationException("GuildId must be specified in configuration to get a specific channel.");
        }
        var guild = _client.Guilds.FirstOrDefault(g => g.Id == guildId);
        if (guild == null)
        {
            throw new InvalidOperationException($"Guild with ID {guildId} not found.");
        }

        return guild;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _disposed = true;
        }
    }
}