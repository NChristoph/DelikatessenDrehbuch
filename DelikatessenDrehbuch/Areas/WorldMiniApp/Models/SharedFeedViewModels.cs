namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class SharedFeedIndexViewModel
    {
        public string CurrentUserHash { get; set; } = string.Empty;
        public List<SharedFeedListItemViewModel> Feeds { get; set; } = new();
        public SharedFeedDetailViewModel? ActiveFeed { get; set; }
    }

    public class SharedFeedListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int ItemCount { get; set; }
        public DateTime LastActivityAtUtc { get; set; }
        public bool IsOwner { get; set; }
        public int UnreadCount { get; set; }
    }

    public class SharedFeedDetailViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string InviteToken { get; set; } = string.Empty;
        public string InviteUrl { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public string CurrentUserHash { get; set; } = string.Empty;
        public List<SharedFeedMemberViewModel> Members { get; set; } = new();
        public List<SharedFeedContentCardViewModel> Items { get; set; } = new();
        public List<SharedFeedTodoViewModel> Todos { get; set; } = new();
        public int MemberCount => Members.Count;
        public int ItemCount => Items.Count;
        public int UnreadCount { get; set; }
    }

    public class SharedFeedTodoViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string? CompletedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    public class SharedFeedMemberViewModel
    {
        public string UserHash { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAtUtc { get; set; }
    }

    public class SharedFeedContentCardViewModel
    {
        public string ContentType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SourceToken { get; set; } = string.Empty;
        public string OpenUrl { get; set; } = string.Empty;
        public string AddedByName { get; set; } = string.Empty;
        public string CreatorUserHash { get; set; } = string.Empty;
        public bool IsCreator { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int PersonCount { get; set; }
        public int MealCount { get; set; }
        public int RecipeCount { get; set; }
        public string PreviewText { get; set; } = string.Empty;
        public List<RecipeDishPreview> Dishes { get; set; } = new();
        public int DurationDays { get; set; }
    }

    public class RecipeDishPreview
    {
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int PreparationTime { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}
