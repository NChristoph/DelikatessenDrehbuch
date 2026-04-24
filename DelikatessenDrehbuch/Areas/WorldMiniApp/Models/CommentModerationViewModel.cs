namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Models
{
    public class CommentModerationViewModel
    {
        public string UserHash { get; set; } = string.Empty;
        public int OpenReportCount { get; set; }
        public List<CommentModerationItemViewModel> Items { get; set; } = new();
    }

    public class CommentModerationItemViewModel
    {
        public int CommentId { get; set; }
        public int PostingId { get; set; }
        public string CommentText { get; set; } = string.Empty;
        public string CommentUserName { get; set; } = string.Empty;
        public string CommentUserHash { get; set; } = string.Empty;
        public string VerificationLevel { get; set; } = string.Empty;
        public DateTime CommentCreatedAtUtc { get; set; }
        public int ReportCount { get; set; }
        public DateTime LatestReportAtUtc { get; set; }
        public List<CommentReportReasonViewModel> Reasons { get; set; } = new();
    }

    public class CommentReportReasonViewModel
    {
        public string Reason { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
