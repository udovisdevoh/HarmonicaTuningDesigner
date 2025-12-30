namespace HarmonicaTuningDesigner
{
    public class HarmonicaDesignerViewModel
    {
        public string HarmonicaType { get; set; } // "Diatonic" | "Chromatic"
        public int HoleCount { get; set; }

        public List<string> AvailableKeys { get; set; }
        public List<string> AvailableTunings { get; set; }
        public List<string> AvailableModes { get; set; }

        // New: list of available note names shown in the UI
        public List<string> AvailableNotes { get; set; }

        // New: list of missing note names (notes not present on the current instrument)
        public List<string> MissingNotes { get; set; }

        public ReedPlateViewModel Diatonic { get; set; }
        public ReedPlateViewModel ChromaticUpper { get; set; }
        public ReedPlateViewModel ChromaticLower { get; set; }
    }
}
