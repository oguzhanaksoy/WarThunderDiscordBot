using ClanRatingTracker.Configuration;
using ClanRatingTracker.Interfaces;
using ClanRatingTracker.Models;

using Microsoft.Extensions.Logging;

namespace ClanRatingTracker.Services;

public class ClanTrackingService : IClanTrackingService
{
    private readonly IWebScraper _webScraper;
    private readonly IClanRepository _clanRepository;
    private readonly IDiscordService _discordService;
    private readonly ILogger<ClanTrackingService> _logger;
    private readonly AppConfiguration _config;
    private readonly string _clanUrl;

    public ClanTrackingService(
        IWebScraper webScraper,
        IClanRepository clanRepository,
        IDiscordService discordService,
        ILogger<ClanTrackingService> logger,
        AppConfiguration configuration)
    {
        _webScraper = webScraper ?? throw new ArgumentNullException(nameof(webScraper));
        _clanRepository = clanRepository ?? throw new ArgumentNullException(nameof(clanRepository));
        _discordService = discordService ?? throw new ArgumentNullException(nameof(discordService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _clanUrl = configuration?.ClanTracking?.ClanUrl ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task ExecuteTrackingCycleAsync()
    {
        var executionId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["ExecutionId"] = executionId,
            ["ClanUrl"] = _clanUrl,
            ["StartTime"] = startTime
        });

        try
        {
            _logger.LogInformation("Starting clan tracking cycle with execution ID {ExecutionId}", executionId);

            // Step 1: Scrape current clan member data
            _logger.LogInformation("Step 1: Extracting clan member data from War Thunder website");
            _logger.LogDebug("Scraping clan data from URL: {ClanUrl}", _clanUrl);
            
            var scrapingStartTime = DateTime.UtcNow;
            var scrapedMembers = await _webScraper.ExtractClanMembersAsync(_clanUrl);
            var scrapingDuration = DateTime.UtcNow - scrapingStartTime;
            
            _logger.LogInformation("Successfully extracted data for {MemberCount} clan members in {Duration}ms", 
                scrapedMembers.Count, scrapingDuration.TotalMilliseconds);
            
            if (scrapedMembers.Count == 0)
            {
                _logger.LogWarning("No clan members found during scraping. This may indicate an issue with the website or parsing logic.");
                return;
            }

            // Step 2: Check if this is the first run
            _logger.LogInformation("Step 2: Checking database status");
            var hasAnyData = await _clanRepository.HasAnyDataAsync();
            
            // Step 3: Process and save member data (this will detect changes and only save when needed)
            _logger.LogInformation("Step 3: Processing and saving member data");
            var processingStartTime = DateTime.UtcNow;
            
            var processingResult = await _clanRepository.ProcessAndSaveMemberDataAsync(scrapedMembers);
            var processingDuration = DateTime.UtcNow - processingStartTime;
            
            _logger.LogInformation("Processed member data in {Duration}ms. Found {ChangeCount} rating changes, {NewMembers} new members, {InactiveMembers} inactive members", 
                processingDuration.TotalMilliseconds, processingResult.RatingChanges.Count, processingResult.NewMembers.Count, processingResult.InactiveMembers.Count);

            // Log detailed rating change statistics
            if (processingResult.RatingChanges.Any())
            {
                var positiveChanges = processingResult.RatingChanges.Count(rc => rc.Change > 0);
                var negativeChanges = processingResult.RatingChanges.Count(rc => rc.Change < 0);
                var maxIncrease = processingResult.RatingChanges.Where(rc => rc.Change > 0).MaxBy(rc => rc.Change);
                var maxDecrease = processingResult.RatingChanges.Where(rc => rc.Change < 0).MinBy(rc => rc.Change);
                
                _logger.LogInformation("Rating change statistics: {PositiveChanges} increases, {NegativeChanges} decreases", 
                    positiveChanges, negativeChanges);
                
                if (maxIncrease != null)
                {
                    _logger.LogInformation("Largest rating increase: {Username} (+{Rating})", 
                        maxIncrease.Username, maxIncrease.Change);
                }
                
                if (maxDecrease != null)
                {
                    _logger.LogInformation("Largest rating decrease: {Username} ({Rating})", 
                        maxDecrease.Username, maxDecrease.Change);
                }
            }

            // Step 4: Publish changes to Discord
            _logger.LogInformation("Step 4: Publishing results to Discord");
            var discordStartTime = DateTime.UtcNow;
            
            if (processingResult.RatingChanges.Any())
            {
                _logger.LogInformation("Publishing {ChangeCount} rating changes to Discord", processingResult.RatingChanges.Count);
                await _discordService.PublishRatingChangesAsync(processingResult.RatingChanges);
                _logger.LogInformation("Successfully published rating changes to Discord");
            }
            else if (!hasAnyData)
            {
                // First run - no previous data exists, publish all current members as initial data
                _logger.LogInformation("First run detected (no previous data). Publishing all {MemberCount} current members as initial clan data", scrapedMembers.Count);
                
                // Convert scraped members to RatingChange format for initial display
                var initialMemberData = scrapedMembers.Select(member => new RatingChange
                {
                    Username = member.Username,
                    OldRating = 0, // No previous rating
                    NewRating = member.PersonalClanRating,
                    Change = member.PersonalClanRating, // Show current rating as the "change"
                    Date = DateTime.Today
                }).ToList();
                
                await _discordService.PublishInitialMemberDataAsync(initialMemberData);
                _logger.LogInformation("Successfully published initial clan member data to Discord");
            }
            else
            {
                _logger.LogInformation("No rating changes detected since last run - no Discord message needed");
                _logger.LogInformation("This prevents spam when running multiple times per day with same ratings");
            }
            
            var discordDuration = DateTime.UtcNow - discordStartTime;
            _logger.LogInformation("Discord publishing completed in {Duration}ms", discordDuration.TotalMilliseconds);

            // Step 5: Assign roles to new members
            _logger.LogInformation("Step 5: Assigning roles to new members");
            var roleAssignmentStartTime = DateTime.UtcNow;
            var successfulRoleAssignments = 0;
            var failedRoleAssignments = 0;

            if (processingResult.NewMembers is { Count: > 0 })
            {
                _logger.LogInformation("New members joined: {NewMembersList}", string.Join(", ", processingResult.NewMembers));
            }
            foreach (var newMember in processingResult.NewMembers)
            {
                try
                {
                    // Audit log for role assignment action
                    _logger.LogWarning("AUDIT: Attempting to assign role to new clan member: {Username}. " +
                                     "Member was added to clan and needs role assignment.", newMember);
                    
                    await _discordService.AssignRoleToUserAsync(newMember, _config.Discord.RoleId);
                    successfulRoleAssignments++;
                    
                    _logger.LogInformation("Successfully assigned role to new member: {Username}", newMember);
                }
                catch (Exception ex)
                {
                    failedRoleAssignments++;
                    
                    // Audit log for failed role assignment
                    _logger.LogError(ex, "AUDIT: Failed to assign role to new clan member: {Username}. " +
                                       "Error: {ErrorMessage}", newMember, ex.Message);
                }
            }
            
            var roleAssignmentDuration = DateTime.UtcNow - roleAssignmentStartTime;
            _logger.LogInformation("Role assignment completed in {Duration}ms. " +
                                 "Successful assignments: {SuccessfulAssignments}, Failed assignments: {FailedAssignments}", 
                                 roleAssignmentDuration.TotalMilliseconds, successfulRoleAssignments, failedRoleAssignments);

            // Step 6: Handle role management for inactive members
            _logger.LogInformation("Step 6: Processing role management for inactive members");
            
           

            if (processingResult.InactiveMembers is { Count: > 0 })
            {
                _logger.LogInformation("Inactive members detected: {InactiveMembersList}", string.Join(", ", processingResult.InactiveMembers));
            }

            var roleManagementStartTime = DateTime.UtcNow;
            var failedRoleRemovals = 0;
            var successfulRoleRemovals = 0;

            foreach (var inactiveMember in processingResult.InactiveMembers)
            {
                try
                {
                    // Audit log for role management action
                    _logger.LogWarning("AUDIT: Attempting to remove role from inactive member: {Username}. " +
                                     "Member was present in previous scan but not found in current scan.", inactiveMember);
                    
                    await _discordService.RemoveRoleFromUserAsync(inactiveMember, _config.Discord.RoleId); // Role ID handled in service
                    successfulRoleRemovals++;
                    
                    // Audit log for successful role removal
                    _logger.LogWarning("AUDIT: Successfully removed role from inactive member: {Username}. " +
                                     "Action completed at {Timestamp}", inactiveMember, DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    failedRoleRemovals++;
                    
                    // Audit log for failed role removal
                    _logger.LogError(ex, "AUDIT: Failed to remove role from inactive member: {Username}. " +
                                       "Error: {ErrorMessage}", inactiveMember, ex.Message);
                }
            }
            
            var roleManagementDuration = DateTime.UtcNow - roleManagementStartTime;
            _logger.LogInformation("Role management completed in {Duration}ms. " +
                                 "Successful removals: {SuccessfulRemovals}, Failed removals: {FailedRemovals}", 
                                 roleManagementDuration.TotalMilliseconds, successfulRoleRemovals, failedRoleRemovals);

            // Final summary with comprehensive statistics
            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Clan tracking cycle completed successfully in {TotalDuration}ms. " +
                                 "Summary: {MemberCount} members processed, {ChangeCount} rating changes, " +
                                 "{NewMembers} new members, {InactiveMembers} inactive members, " +
                                 "{SuccessfulRoleAssignments} successful role assignments, {FailedRoleAssignments} failed role assignments, " +
                                 "{SuccessfulRoleRemovals} successful role removals, {FailedRoleRemovals} failed role removals",
                                 totalDuration.TotalMilliseconds, scrapedMembers.Count, processingResult.RatingChanges.Count, 
                                 processingResult.NewMembers.Count, processingResult.InactiveMembers.Count, 
                                 successfulRoleAssignments, failedRoleAssignments, successfulRoleRemovals, failedRoleRemovals);
        }
        catch (Exception ex)
        {
            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Fatal error during clan tracking cycle after {Duration}ms. " +
                           "ExecutionId: {ExecutionId}, Error: {ErrorMessage}", 
                           totalDuration.TotalMilliseconds, executionId, ex.Message);
            throw;
        }
    }
}