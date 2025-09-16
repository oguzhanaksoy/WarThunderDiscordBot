# Clan Rating Tracker

A Discord bot that tracks War Thunder clan rating changes for "Aces of Anatolia" clan members.

## Configuration

Before running the application, you need to configure the following settings in `appsettings.json`:

### Discord Configuration
- `Discord:Token` - Your Discord bot token
- `Discord:ChannelId` - The Discord channel ID where rating changes will be posted
- `Discord:RoleId` - The Discord role ID to manage based on clan membership

### Clan Tracking Configuration
- `ClanTracking:ClanUrl` - The War Thunder clan page URL (default: Aces of Anatolia)
- `ClanTracking:RetryAttempts` - Number of retry attempts for failed operations (default: 3)
- `ClanTracking:RetryDelayMs` - Delay between retry attempts in milliseconds (default: 1000)

### Database Configuration
- `ConnectionStrings:DefaultConnection` - SQLite database connection string (default: "Data Source=clantracking.db")

## Usage

1. Configure your settings in `appsettings.json`
2. Run the application: `dotnet run`

The application will:
1. Extract clan member data from the War Thunder website
2. Store the data in the SQLite database
3. Calculate rating changes from the previous run
4. Post changes to the configured Discord channel
5. Remove Discord roles from users not found in the clan
6. Assign roles to new clan members or return members

You need to run the application periodically (e.g., via a scheduled task or cron job) to keep the data up-to-date.

## Development

- Use `appsettings.Development.json` for development-specific settings
- The application uses structured logging with console and debug output
- Configuration validation ensures all required settings are present