namespace HarmonicaTuningDesigner
{
    public class Tuning
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int BaseHoles { get; set; }
        public List<HoleDefinition> BaseLayout { get; set; } = new();
    }
}
