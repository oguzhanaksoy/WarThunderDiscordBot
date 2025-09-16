using ClanRatingTracker.Data;
using ClanRatingTracker.Interfaces;
using ClanRatingTracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClanRatingTracker.Data;

public class ClanRepository : IClanRepository
{
    private readonly ClanTrackingContext _context;
    private readonly ILogger<ClanRepository> _logger;

    public ClanRepository(ClanTrackingContext context, ILogger<ClanRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> HasAnyDataAsync()
    {
        try
        {
            return await _context.ClanMembers.AnyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if database has any data");
            throw;
        }
    }

    public async Task<List<ClanMember>> GetLatestMembersAsync()
    {
        try
        {
            var activeMembers = await _context.ClanMembers
                .Where(m => m.IsActive)
                .Include(m => m.Ratings.OrderByDescending(r => r.RecordedAt).Take(1))
                .OrderBy(m => m.Username)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} active clan members", activeMembers.Count);
            return activeMembers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest clan members");
            throw;
        }
    }

    public async Task<MemberProcessingResult> ProcessAndSaveMemberDataAsync(List<ScrapedMemberData> scrapedMembers)
    {
        if (scrapedMembers == null || !scrapedMembers.Any())
        {
            _logger.LogWarning("No scraped members provided to process");
            return new MemberProcessingResult();
        }

        try
        {
            var recordedAt = DateTime.UtcNow;
            var ratingChanges = new List<RatingChange>();
            var newMembers = new List<string>();
            var inactiveMembers = new List<ClanMember>();
            var newRatingsToAdd = new List<ClanRating>();

            _logger.LogInformation("Processing {Count} scraped members", scrapedMembers.Count);

            // Get all existing members
            var existingMembers = await _context.ClanMembers
                .Include(m => m.Ratings.OrderByDescending(r => r.RecordedAt).Take(1))
                .ToDictionaryAsync(m => m.Username, m => m);


            foreach (var scrapedMember in scrapedMembers)
            {
                if (existingMembers.TryGetValue(scrapedMember.Username, out var existingMember))
                {
                    if (existingMember.IsActive == false)
                    {
                        newMembers.Add(scrapedMember.Username);
                    }
                    // Update existing member
                    existingMember.IsActive = true;
                    existingMember.LastSeen = recordedAt;

                    // Get the latest rating for this member
                    var latestRating = existingMember.Ratings.FirstOrDefault();
                    var currentRating = scrapedMember.PersonalClanRating;

                    if (latestRating == null || latestRating.PersonalClanRating != currentRating)
                    {
                        // Rating has changed or this is the first rating - add new rating record
                        var newRating = new ClanRating
                        {
                            ClanMemberId = existingMember.Id,
                            PersonalClanRating = currentRating,
                            RecordedAt = recordedAt
                        };
                        newRatingsToAdd.Add(newRating);

                        // Create rating change record
                        var oldRating = latestRating?.PersonalClanRating ?? 0;
                        var change = currentRating - oldRating;

                        if (latestRating != null) // Only add to changes if there was a previous rating
                        {
                            ratingChanges.Add(new RatingChange
                            {
                                Username = scrapedMember.Username,
                                OldRating = oldRating,
                                NewRating = currentRating,
                                Change = change,
                                Date = recordedAt.Date
                            });
                        }

                        _logger.LogDebug("Rating change detected for {Username}: {OldRating} → {NewRating} ({Change})",
                            scrapedMember.Username, oldRating, currentRating, change);
                    }
                    else
                    {
                        _logger.LogDebug("No rating change for {Username}: {Rating}", 
                            scrapedMember.Username, currentRating);
                    }
                }
                else
                {
                    // New member - create member and first rating
                    var newMember = new ClanMember
                    {
                        Username = scrapedMember.Username,
                        FirstSeen = recordedAt,
                        LastSeen = recordedAt,
                        IsActive = true
                    };

                    _context.ClanMembers.Add(newMember);
                    await _context.SaveChangesAsync(); // Save to get the ID

                    var newRating = new ClanRating
                    {
                        ClanMemberId = newMember.Id,
                        PersonalClanRating = scrapedMember.PersonalClanRating,
                        RecordedAt = recordedAt
                    };
                    newRatingsToAdd.Add(newRating);

                    // Track new member for role assignment
                    newMembers.Add(scrapedMember.Username);

                    _logger.LogInformation("New member added: {Username} with rating {Rating}", 
                        scrapedMember.Username, scrapedMember.PersonalClanRating);
                }
            }

            // Collect inactive members (those who were not in the scraped data)
            var currentUsers = scrapedMembers.Select(x => x.Username).ToHashSet();
            var existingUsernames = existingMembers.Keys.ToHashSet();
            var inactiveUsers = existingUsernames.Except(currentUsers);
            foreach (var inactiveUsername in inactiveUsers)
            {
                if (existingMembers.TryGetValue(inactiveUsername, out var member) == false)
                {
                    continue;
                }
                inactiveMembers.Add(member);
                _logger.LogInformation("Member marked as inactive: {Username}", inactiveUsername);
                _context.ClanMembers.Remove(member);
            }

            // Save all new ratings
            if (newRatingsToAdd.Any())
            {
                await _context.ClanRatings.AddRangeAsync(newRatingsToAdd);
            }

            var savedChanges = await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully processed member data: {SavedChanges} database changes, {RatingChanges} rating changes, {NewMembers} new members, {InactiveMembers} inactive members",
                savedChanges, ratingChanges.Count, newMembers.Count, inactiveMembers.Count);

            return new MemberProcessingResult
            {
                RatingChanges = ratingChanges.OrderByDescending(r => Math.Abs(r.Change)).ToList(),
                NewMembers = newMembers,
                InactiveMembers = inactiveMembers
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing and saving member data");
            throw;
        }
    }

    public async Task<List<RatingChange>> GetRatingChangesAsync(DateTime date)
    {
        try
        {
            // This method is now mainly for historical queries
            // The main logic is in ProcessAndSaveMemberDataAsync
            var ratingChanges = await _context.ClanRatings
                .Where(r => r.RecordedAt.Date == date.Date)
                .Join(_context.ClanMembers, r => r.ClanMemberId, m => m.Id, (r, m) => new { Rating = r, Member = m })
                .GroupBy(x => x.Member.Username)
                .Select(g => g.OrderByDescending(x => x.Rating.RecordedAt).First())
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} rating records for date: {Date}", 
                ratingChanges.Count, date.ToString("yyyy-MM-dd"));

            return new List<RatingChange>(); // For now, return empty as the main logic is in ProcessAndSaveMemberDataAsync
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rating changes for date: {Date}", date.ToString("yyyy-MM-dd"));
            throw;
        }
    }
}