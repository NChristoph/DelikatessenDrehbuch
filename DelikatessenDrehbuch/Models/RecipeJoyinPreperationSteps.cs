using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class RecipeJoyinPreperationSteps
    {
        public int Id { get; set; }
        public RecipeBaseData Recipe { get; set; }
        [NotMapped]
        public int PreperationStepId { get; set; }
        public RecipePreperationSteps RecipePreperationStep { get; set; }
        public int StepIndex { get; set; }
    }
}
