using HarmonicaTuningDesigner;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace HarmonicaTuningDesigner.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ScaleRepository _scaleRepo;
        private readonly TuningRepository _tuning_repo;

        [BindProperty]
        public HarmonicaDesignerViewModel ViewModel { get; set; } = new();

        public IndexModel(IWebHostEnvironment env)
        {
            var dataPath = Path.Combine(env.ContentRootPath, "Data");

            _scaleRepo = new ScaleRepository(Path.Combine(dataPath, "Scales.xml"));
            _tuning_repo = new TuningRepository(Path.Combine(dataPath, "Tunings.xml"));
        }

        public void OnGet()
        {
            InitializeViewModel();
        }

        public IActionResult OnPost()
        {
            // Rebuild lists and selected plates based on posted values so the UI reflects changes.
            InitializeViewModel();

            var scales = _scaleRepo.GetAll();
            var tunings = _tuning_repo.GetAll();

            // Diatonic
            if (ViewModel.Diatonic != null)
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.Diatonic.Tuning) ?? tunings.First();
                var mode = FindModeByName(scales, ViewModel.Diatonic.Mode) ?? scales.First().Modes.First();
                ViewModel.Diatonic.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, ViewModel.Diatonic.Key ?? "C", mode);
            }

            if (ViewModel.ChromaticUpper != null)
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.ChromaticUpper.Tuning) ?? tunings.First();
                var mode = FindModeByName(scales, ViewModel.ChromaticUpper.Mode) ?? scales.First().Modes.First();
                ViewModel.ChromaticUpper.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, ViewModel.ChromaticUpper.Key ?? "C", mode);
            }

            if (ViewModel.ChromaticLower != null)
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.ChromaticLower.Tuning) ?? tunings.First();
                var mode = FindModeByName(scales, ViewModel.ChromaticLower.Mode) ?? scales.First().Modes.First();
                ViewModel.ChromaticLower.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, ViewModel.ChromaticLower.Key ?? "C", mode);
            }

            // Ensure holes count is respected by trimming or padding the Holes list
            AdjustHoleCounts(ViewModel.Diatonic);
            AdjustHoleCounts(ViewModel.ChromaticUpper);
            AdjustHoleCounts(ViewModel.ChromaticLower);

            return Page();
        }

        private void InitializeViewModel()
        {
            var scales = _scaleRepo.GetAll();
            var tunings = _tuning_repo.GetAll();

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

            // Ensure reed plates exist or have holes populated
            // Diatonic
            if (ViewModel.Diatonic == null)
            {
                var t = tunings.First();
                var m = scales.First().Modes.First();
                ViewModel.Diatonic = CreateDefaultReedPlate(t, m, ViewModel.HoleCount);
            }
            else
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.Diatonic.Tuning) ?? tunings.First();
                var key = ViewModel.Diatonic.Key ?? "C";
                var mode = FindModeByName(scales, ViewModel.Diatonic.Mode) ?? scales.First().Modes.First();
                ViewModel.Diatonic.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, key, mode);
            }

            // ChromaticUpper
            if (ViewModel.ChromaticUpper == null)
            {
                var t = tunings.First();
                var m = scales.First().Modes.First();
                ViewModel.ChromaticUpper = CreateDefaultReedPlate(t, m, ViewModel.HoleCount);
            }
            else
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.ChromaticUpper.Tuning) ?? tunings.First();
                var key = ViewModel.ChromaticUpper.Key ?? "C";
                var mode = FindModeByName(scales, ViewModel.ChromaticUpper.Mode) ?? scales.First().Modes.First();
                ViewModel.ChromaticUpper.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, key, mode);
            }

            // ChromaticLower
            if (ViewModel.ChromaticLower == null)
            {
                var t = tunings.First();
                var m = scales.First().Modes.First();
                ViewModel.ChromaticLower = CreateDefaultReedPlate(t, m, ViewModel.HoleCount);
            }
            else
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.ChromaticLower.Tuning) ?? tunings.First();
                var key = ViewModel.ChromaticLower.Key ?? "C";
                var mode = FindModeByName(scales, ViewModel.ChromaticLower.Mode) ?? scales.First().Modes.First();
                ViewModel.ChromaticLower.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, key, mode);
            }
        }

        private Mode FindModeByName(IReadOnlyList<Scale> scales, string modeName)
        {
            if (string.IsNullOrWhiteSpace(modeName)) return null;
            return scales.SelectMany(s => s.Modes).FirstOrDefault(m => m.Name == modeName);
        }

        private List<HoleViewModel> BuildHolesFromTuning(Tuning tuning, int holes, string key, Mode mode)
        {
            var baseLayout = tuning.BaseLayout;
            // Determine semitone of target key (use first mapping if ambiguous like C#/Db)
            var keySemitone = NoteNameToSemitone(key);

            // MIDI reference: C4 = 60
            var middleC = 60;

            var list = new List<HoleViewModel>();
            for (int i = 0; i < Math.Min(holes, baseLayout.Count); i++)
            {
                var h = baseLayout[i];

                var blowCell = BuildCellFromDegreeString(h.Blow, keySemitone, mode, middleC);
                var drawCell = BuildCellFromDegreeString(h.Draw, keySemitone, mode, middleC);

                list.Add(new HoleViewModel
                {
                    Index = h.Index,
                    Blow = blowCell,
                    Draw = drawCell
                });
            }

            // If tuning has fewer baseLayout items than requested holes, pad by repeating last
            if (list.Count < holes && list.Count > 0)
            {
                var last = list.Last();
                for (int idx = list.Count + 1; idx <= holes; idx++)
                {
                    list.Add(new HoleViewModel
                    {
                        Index = idx,
                        Blow = new NoteCell { Note = last.Blow.Note, Octave = last.Blow.Octave, IsAltered = last.Blow.IsAltered },
                        Draw = new NoteCell { Note = last.Draw.Note, Octave = last.Draw.Octave, IsAltered = last.Draw.IsAltered }
                    });
                }
            }

            return list;
        }

        private NoteCell BuildCellFromDegreeString(string degreeStr, int keySemitone, Mode mode, int middleCMidi)
        {
            if (int.TryParse(degreeStr, out var degree))
            {
                // degree 1 -> index 0
                var idx = Math.Max(0, degree - 1);
                // wrap if mode intervals shorter
                var intervals = mode?.Intervals ?? new List<int> { 0, 2, 4, 5, 7, 9, 11 };
                var interval = intervals[idx % intervals.Count];

                var midi = middleCMidi + keySemitone + interval; // root at C4 + key + interval
                var octave = (midi / 12) - 1;
                var name = SemitoneToName(midi % 12);

                return new NoteCell { Note = name, Octave = octave, IsAltered = name.Contains("#") };
            }

            // fallback: try to treat as literal note name
            var transposed = TransposeNoteName(degreeStr, keySemitone);
            return new NoteCell { Note = transposed.Name, Octave = transposed.Octave, IsAltered = transposed.Name.Contains("#") || transposed.Name.Contains("b") };
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

        // Helpers for note name/semitone mapping and transposition
        private int NoteNameToSemitone(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            // Normalize and prefer sharp name when a slash exists
            var n = name.Replace("\u00A0", "").Trim();
            if (n.Contains('/')) n = n.Split('/')[0];
            // Map
            return n switch
            {
                "C" => 0,
                "C#" => 1,
                "Db" => 1,
                "D" => 2,
                "D#" => 3,
                "Eb" => 3,
                "E" => 4,
                "F" => 5,
                "F#" => 6,
                "Gb" => 6,
                "G" => 7,
                "G#" => 8,
                "Ab" => 8,
                "A" => 9,
                "A#" => 10,
                "Bb" => 10,
                "B" => 11,
                _ => 0
            };
        }

        private (string Name, int Octave) TransposeNoteName(string sourceName, int targetKeySemitone)
        {
            // SourceName may be like "C" or "C#" or "D". Treat as base in C (semitone of sourceName)
            if (string.IsNullOrWhiteSpace(sourceName)) return (string.Empty, 4);
            var s = sourceName.Trim();
            // If source includes octave like C4, try to parse
            int sourceOctave = 4;
            string notePart = s;
            // Extract trailing digits as octave
            var digits = new string(s.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            if (!string.IsNullOrEmpty(digits))
            {
                if (int.TryParse(digits, out var parsedOct))
                {
                    sourceOctave = parsedOct;
                    notePart = s.Substring(0, s.Length - digits.Length);
                }
            }

            if (notePart.Contains('/')) notePart = notePart.Split('/')[0];

            var sourceSem = NoteNameToSemitone(notePart);
            // Source assumed to be relative to C (0). To get final semitone: (sourceSem + shift) % 12
            var shift = (targetKeySemitone - 0 + 12) % 12; // base is C
            var finalSem = (sourceSem + shift) % 12;

            // Adjust octave for overflow
            int octaveOffset = (sourceSem + shift) / 12; // integer division
            // But since sourceSem+shift < 24 we can compute properly when negative
            if (sourceSem + shift < 0) octaveOffset = -1;

            var finalOctave = sourceOctave + octaveOffset;
            var finalName = SemitoneToName(finalSem);

            return (finalName, finalOctave);
        }

        private string SemitoneToName(int sem)
        {
            var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            sem = ((sem % 12) + 12) % 12;
            return names[sem];
        }

        private static List<string> GetKeys() => new()
        {
            "C","C#/Db","D","D#/Eb","E","F","F#/Gb","G","G#/Ab","A","A#/Bb","B"
        };
    }
}
