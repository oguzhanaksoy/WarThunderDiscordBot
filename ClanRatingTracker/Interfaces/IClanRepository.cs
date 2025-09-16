using ClanRatingTracker.Models;

namespace ClanRatingTracker.Interfaces;

public interface IClanRepository
{
    Task<List<ClanMember>> GetLatestMembersAsync();
    Task<MemberProcessingResult> ProcessAndSaveMemberDataAsync(List<ScrapedMemberData> scrapedMembers);
    Task<List<RatingChange>> GetRatingChangesAsync(DateTime date);
    Task<bool> HasAnyDataAsync();
}