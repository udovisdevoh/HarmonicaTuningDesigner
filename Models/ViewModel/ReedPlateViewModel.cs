namespace HarmonicaTuningDesigner
{
    public class ReedPlateViewModel
    {
        public string Key { get; set; }
        public string Tuning { get; set; }
        public string Mode { get; set; }
        public List<HoleViewModel> Holes { get; set; }

        // Detected contiguous chords (blow or draw) on this plate
        public List<ChordMatch> Chords { get; set; } = new();
    }
}
