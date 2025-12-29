namespace HarmonicaTuningDesigner
{
    public class HarmonicaDesignerViewModel
    {
        public string HarmonicaType { get; set; } // "Diatonic" | "Chromatic"
        public int HoleCount { get; set; }

        public List<string> AvailableKeys { get; set; }
        public List<string> AvailableTunings { get; set; }
        public List<string> AvailableModes { get; set; }

        public ReedPlateViewModel Diatonic { get; set; }
        public ReedPlateViewModel ChromaticUpper { get; set; }
        public ReedPlateViewModel ChromaticLower { get; set; }
    }
}
