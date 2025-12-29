namespace HarmonicaTuningDesigner
{
    public class ReedPlateViewModel
    {
        public string Key { get; set; }
        public string Tuning { get; set; }
        public string Mode { get; set; }
        public List<HoleViewModel> Holes { get; set; }
    }
}
