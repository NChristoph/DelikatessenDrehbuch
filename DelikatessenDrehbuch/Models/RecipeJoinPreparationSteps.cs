using System.ComponentModel.DataAnnotations.Schema;

namespace DelikatessenDrehbuch.Models
{
    public class RecipeJoinPreparationSteps
    {
        public int Id { get; set; }
        public RecipeBaseData Recipe { get; set; }
        [NotMapped]
        public int PreparationStepId { get; set; }
        public RecipePreparationSteps RecipePreparationStep { get; set; }
        public int StepIndex { get; set; }
    }
}
