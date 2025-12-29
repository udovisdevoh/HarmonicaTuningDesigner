using HarmonicaTuningDesigner;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class IndexModel : PageModel
{
    private readonly ScaleRepository _scaleRepo;
    private readonly TuningRepository _tuningRepo;

    public HarmonicaDesignerViewModel ViewModel { get; set; } = new();

    public IndexModel(IWebHostEnvironment env)
    {
        var dataPath = Path.Combine(env.ContentRootPath, "Data");

        _scaleRepo = new ScaleRepository(Path.Combine(dataPath, "Scales.xml"));
        _tuningRepo = new TuningRepository(Path.Combine(dataPath, "Tunings.xml"));
    }

    public void OnGet()
    {
        var scales = _scaleRepo.GetAll();
        var tunings = _tuningRepo.GetAll();

        ViewModel = new HarmonicaDesignerViewModel
        {
            HarmonicaType = "Diatonic",
            HoleCount = 10,

            AvailableKeys = GetKeys(),
            AvailableModes = scales.SelectMany(s => s.Modes)
                                   .Select(m => m.Name)
                                   .Distinct()
                                   .ToList(),

            AvailableTunings = tunings.Select(t => t.Name).ToList(),

            Diatonic = CreateDefaultReedPlate(tunings.First(), scales.First().Modes.First()),
            ChromaticUpper = CreateDefaultReedPlate(tunings.First(), scales.First().Modes.First()),
            ChromaticLower = CreateDefaultReedPlate(tunings.First(), scales.First().Modes.First())
        };
    }

    private ReedPlateViewModel CreateDefaultReedPlate(Tuning tuning, Mode mode)
    {
        return new ReedPlateViewModel
        {
            Key = "C",
            Tuning = tuning.Name,
            Mode = mode.Name,
            Holes = tuning.BaseLayout.Select(h => new HoleViewModel
            {
                Index = h.Index,
                Blow = new NoteCell { Note = h.Blow, Octave = 4 },
                Draw = new NoteCell { Note = h.Draw, Octave = 4 }
            }).ToList()
        };
    }

    private static List<string> GetKeys() => new()
    {
        "C","C#/Db","D","D#/Eb","E","F","F#/Gb","G","G#/Ab","A","A#/Bb","B"
    };
}
