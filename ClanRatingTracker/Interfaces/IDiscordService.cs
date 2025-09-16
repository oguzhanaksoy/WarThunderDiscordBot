using ClanRatingTracker.Models;

namespace ClanRatingTracker.Interfaces;

public interface IDiscordService
{
    Task PublishRatingChangesAsync(List<RatingChange> changes);
    Task PublishInitialMemberDataAsync(List<RatingChange> initialData);
    Task RemoveRoleFromUserAsync(string username, ulong roleId);
    Task AssignRoleToUserAsync(string username, ulong roleId);
}