namespace test.Models
{

        public class Story
        {
            public int StoryId { get; set; }
            public string Title { get; set; }
            public string Type { get; set; }
            public DateTime Date { get; set; }

            public virtual IEnumerable<Sentence> Sentences { get; set; } // one to many Story-Sentence
        }
    
}
