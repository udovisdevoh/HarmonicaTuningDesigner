namespace HarmonicaTuningDesigner
{

    public class Mode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Degree { get; set; }
        public List<int> Intervals { get; set; } = new();
    }
}
