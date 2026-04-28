using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces
{
    public interface IWorldAdPreferenceService
    {
        Task<AdPreferenceDashboardViewModel> GetDashboardProfileAsync(string userHash, string preferredLanguage);
        Task UpdateSettingsAsync(string userHash, UpdateAdPreferenceSettingsRequest request, string preferredLanguage);
        Task RebuildProfileAsync(string userHash, string preferredLanguage);
        Task UpdatePreferredLanguageAsync(string userHash, string cultureCode);
    }
}
