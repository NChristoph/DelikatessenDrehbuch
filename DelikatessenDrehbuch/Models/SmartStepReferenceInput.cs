namespace DelikatessenDrehbuch.Models
{
    public class SmartStepReferenceInput
    {
        public string MasterStepKey { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";
        public int StepIndex { get; set; }
    }
}
