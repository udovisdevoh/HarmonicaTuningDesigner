namespace HarmonicaTuningDesigner
{
    public class NoteCell
    {
        public string Note { get; set; }      // e.g. C#, Eb
        public int Octave { get; set; }
        public bool IsAltered { get; set; }
    }
}
