using HarmonicaTuningDesigner;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace HarmonicaTuningDesigner.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ScaleRepository _scaleRepo;
        private readonly TuningRepository _tuningRepo;

        [BindProperty]
        public HarmonicaDesignerViewModel ViewModel { get; set; } = new();

        public IndexModel(IWebHostEnvironment env)
        {
            var dataPath = Path.Combine(env.ContentRootPath, "Data");

            _scaleRepo = new ScaleRepository(Path.Combine(dataPath, "Scales.xml"));
            _tuningRepo = new TuningRepository(Path.Combine(dataPath, "Tunings.xml"));
        }

        public void OnGet()
        {
            InitializeViewModel();
        }

        public IActionResult OnPost()
        {
            // Rebuild lists and selected plates based on posted values so the UI reflects changes.
            InitializeViewModel();

            // Keep current selections from posted ViewModel where appropriate.
            // If Tuning/Mode/Key were posted, respect them; otherwise fall back to defaults.

            // Ensure holes count is respected by trimming or padding the Holes list
            AdjustHoleCounts(ViewModel.Diatonic);
            AdjustHoleCounts(ViewModel.ChromaticUpper);
            AdjustHoleCounts(ViewModel.ChromaticLower);

            return Page();
        }

        private void InitializeViewModel()
        {
            var scales = _scaleRepo.GetAll();
            var tunings = _tuningRepo.GetAll();

            // If lists already exist on posted ViewModel preserve them, otherwise populate
            ViewModel.AvailableKeys = ViewModel.AvailableKeys ?? GetKeys();
            ViewModel.AvailableModes = ViewModel.AvailableModes ?? scales.SelectMany(s => s.Modes)
                                           .Select(m => m.Name)
                                           .Distinct()
                                           .ToList();
            ViewModel.AvailableTunings = ViewModel.AvailableTunings ?? tunings.Select(t => t.Name).ToList();

            // Ensure defaults on first load
            if (string.IsNullOrEmpty(ViewModel.HarmonicaType)) ViewModel.HarmonicaType = "Diatonic";
            if (ViewModel.HoleCount == 0) ViewModel.HoleCount = 10;

            // Ensure reed plates exist
            if (ViewModel.Diatonic == null)
                ViewModel.Diatonic = CreateDefaultReedPlate(tunings.First(), scales.First().Modes.First(), ViewModel.HoleCount);

            if (ViewModel.ChromaticUpper == null)
                ViewModel.ChromaticUpper = CreateDefaultReedPlate(tunings.First(), scales.First().Modes.First(), ViewModel.HoleCount);

            if (ViewModel.ChromaticLower == null)
                ViewModel.ChromaticLower = CreateDefaultReedPlate(tunings.First(), scales.First().Modes.First(), ViewModel.HoleCount);
        }

        private void AdjustHoleCounts(ReedPlateViewModel plate)
        {
            if (plate == null || plate.Holes == null) return;

            var desired = ViewModel.HoleCount;
            if (plate.Holes.Count > desired)
            {
                plate.Holes = plate.Holes.Take(desired).ToList();
            }
            else if (plate.Holes.Count < desired)
            {
                // Pad with empty notes using the same note/octave as last entry
                var last = plate.Holes.Last();
                for (int i = plate.Holes.Count + 1; i <= desired; i++)
                {
                    plate.Holes.Add(new HoleViewModel
                    {
                        Index = i,
                        Blow = new NoteCell { Note = last.Blow.Note, Octave = last.Blow.Octave },
                        Draw = new NoteCell { Note = last.Draw.Note, Octave = last.Draw.Octave }
                    });
                }
            }
        }

        private ReedPlateViewModel CreateDefaultReedPlate(Tuning tuning, Mode mode, int holes)
        {
            var baseLayout = tuning.BaseLayout;
            var list = baseLayout.Take(holes).Select(h => new HoleViewModel
            {
                Index = h.Index,
                Blow = new NoteCell { Note = h.Blow, Octave = 4, IsAltered = h.Blow.Contains("#") || h.Blow.Contains("b") },
                Draw = new NoteCell { Note = h.Draw, Octave = 4, IsAltered = h.Draw.Contains("#") || h.Draw.Contains("b") }
            }).ToList();

            return new ReedPlateViewModel
            {
                Key = "C",
                Tuning = tuning.Name,
                Mode = mode.Name,
                Holes = list
            };
        }

        private static List<string> GetKeys() => new()
        {
            "C","C#/Db","D","D#/Eb","E","F","F#/Gb","G","G#/Ab","A","A#/Bb","B"
        };
    }
}
