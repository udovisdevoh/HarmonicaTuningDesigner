namespace HarmonicaTuningDesigner
{
    public class Scale
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<int> Intervals { get; set; } = new();
        public List<Mode> Modes { get; set; } = new();
    }
}
