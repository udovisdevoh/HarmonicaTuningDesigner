using HarmonicaTuningDesigner;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace HarmonicaTuningDesigner.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ScaleRepository _scaleRepo;
        private readonly TuningRepository _tuning_repo;
        private readonly ChordService _chordService = new();

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

            // If a rotate request was posted, handle it before building holes so new key/mode are used
            var rotate = Request.Form["rotateMode"].FirstOrDefault();
            if (!string.IsNullOrEmpty(rotate))
            {
                TryHandleRotate(rotate, scales);
            }

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

            // Compute available notes for UI
            ComputeAvailableNotes();

            // Populate detected chords so initial page shows chord list without user interaction
            PopulateChords();

            return Page();
        }

        private void TryHandleRotate(string rotateValue, IReadOnlyList<Scale> scales)
        {
            // rotateValue expected: "PlateId:up" or "PlateId:down"
            var parts = rotateValue.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return;
            var plateId = parts[0];
            var direction = parts[1];

            ReedPlateViewModel plate = plateId switch
            {
                "Diatonic" => ViewModel.Diatonic,
                "ChromaticUpper" => ViewModel.ChromaticUpper,
                "ChromaticLower" => ViewModel.ChromaticLower,
                _ => null
            };

            if (plate == null) return;

            // Find current mode and parent scale
            var currentMode = scales.SelectMany(s => s.Modes).FirstOrDefault(m => m.Name == plate.Mode);
            if (currentMode == null) return;
            var parentScale = scales.FirstOrDefault(s => s.Modes.Any(m => m.Name == currentMode.Name));
            if (parentScale == null) return;

            // Compute old and new key semitones
            var oldKeySem = NoteNameToSemitone(plate.Key ?? "C");
            int newKeySem;
            if (direction.Equals("up", StringComparison.OrdinalIgnoreCase))
                newKeySem = (oldKeySem + 7) % 12; // up a fifth
            else
                newKeySem = (oldKeySem + 5) % 12; // down a fifth == up a fourth (+5 mod12)

            // Determine new mode degree by advancing degree within the parent scale according to circle of fifths.
            // For a 7-mode diatonic scale: moving up a fifth advances degree by +4 (mod 7); moving down a fifth advances by +3 (mod 7).
            var oldDegree = currentMode.Degree; // 1-based
            int degreeOffset = direction.Equals("up", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
            var newDegree = ((oldDegree - 1 + degreeOffset) % parentScale.Modes.Count) + 1;

            var newMode = parentScale.Modes.FirstOrDefault(m => m.Degree == newDegree) ?? currentMode;

            plate.Mode = newMode.Name;

            // Map semitone to the key label used in the AvailableKeys list (e.g., "F#/Gb") so the select has a matching option
            plate.Key = MapSemitoneToAvailableKey(newKeySem);

            // Remove posted ModelState so updated model values are rendered by Razor helpers
            ModelState.Remove($"ViewModel.{plateId}.Key");
            ModelState.Remove($"ViewModel.{plateId}.Mode");
        }

        private string MapSemitoneToAvailableKey(int semitone)
        {
            var baseName = SemitoneToName(semitone);
            var keys = ViewModel.AvailableKeys ?? GetKeys();

            // Try to find an entry that contains the base name (handles combined entries like "F#/Gb")
            var match = keys.FirstOrDefault(k => k.Split('/').Any(p => p.Equals(baseName, StringComparison.OrdinalIgnoreCase)));
            if (!string.IsNullOrEmpty(match)) return match;

            // Fallback: if an exact match exists return it, otherwise return base name
            var exact = keys.FirstOrDefault(k => k.Equals(baseName, StringComparison.OrdinalIgnoreCase));
            return exact ?? baseName;
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
                // default to C Ionian Richter and compute actual notes
                var t = tunings.FirstOrDefault(x => x.Name == "Richter") ?? tunings.First();
                var m = scales.SelectMany(s => s.Modes).FirstOrDefault(mm => mm.Name == "Ionian") ?? scales.First().Modes.First();
                var holes = BuildHolesFromTuning(t, ViewModel.HoleCount, "C", m);
                ViewModel.Diatonic = new ReedPlateViewModel
                {
                    Key = "C",
                    Tuning = t.Name,
                    Mode = m.Name,
                    Holes = holes
                };
            }
            else
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.Diatonic.Tuning) ?? tunings.First();
                var key = ViewModel.Diatonic.Key ?? "C";
                var mode = FindModeByName(scales, ViewModel.Diatonic.Mode) ?? scales.SelectMany(s => s.Modes).FirstOrDefault(mm => mm.Name == "Ionian") ?? scales.First().Modes.First();
                ViewModel.Diatonic.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, key, mode);
                // ensure tuning/key/mode values are set
                ViewModel.Diatonic.Tuning = t.Name;
                ViewModel.Diatonic.Key = key;
                ViewModel.Diatonic.Mode = mode.Name;
            }

            // ChromaticUpper
            if (ViewModel.ChromaticUpper == null)
            {
                var t = tunings.FirstOrDefault(x => x.Name == "Richter") ?? tunings.First();
                var m = scales.SelectMany(s => s.Modes).FirstOrDefault(mm => mm.Name == "Ionian") ?? scales.First().Modes.First();
                var holes = BuildHolesFromTuning(t, ViewModel.HoleCount, "C", m);
                ViewModel.ChromaticUpper = new ReedPlateViewModel
                {
                    Key = "C",
                    Tuning = t.Name,
                    Mode = m.Name,
                    Holes = holes
                };
            }
            else
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.ChromaticUpper.Tuning) ?? tunings.First();
                var key = ViewModel.ChromaticUpper.Key ?? "C";
                var mode = FindModeByName(scales, ViewModel.ChromaticUpper.Mode) ?? scales.SelectMany(s => s.Modes).FirstOrDefault(mm => mm.Name == "Ionian") ?? scales.First().Modes.First();
                ViewModel.ChromaticUpper.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, key, mode);
                ViewModel.ChromaticUpper.Tuning = t.Name;
                ViewModel.ChromaticUpper.Key = key;
                ViewModel.ChromaticUpper.Mode = mode.Name;
            }

            // ChromaticLower
            if (ViewModel.ChromaticLower == null)
            {
                var t = tunings.FirstOrDefault(x => x.Name == "Richter") ?? tunings.First();
                var m = scales.SelectMany(s => s.Modes).FirstOrDefault(mm => mm.Name == "Ionian") ?? scales.First().Modes.First();
                var holes = BuildHolesFromTuning(t, ViewModel.HoleCount, "C", m);
                ViewModel.ChromaticLower = new ReedPlateViewModel
                {
                    Key = "C",
                    Tuning = t.Name,
                    Mode = m.Name,
                    Holes = holes
                };
            }
            else
            {
                var t = tunings.FirstOrDefault(x => x.Name == ViewModel.ChromaticLower.Tuning) ?? tunings.First();
                var key = ViewModel.ChromaticLower.Key ?? "C";
                var mode = FindModeByName(scales, ViewModel.ChromaticLower.Mode) ?? scales.SelectMany(s => s.Modes).FirstOrDefault(mm => mm.Name == "Ionian") ?? scales.First().Modes.First();
                ViewModel.ChromaticLower.Holes = BuildHolesFromTuning(t, ViewModel.HoleCount, key, mode);
                ViewModel.ChromaticLower.Tuning = t.Name;
                ViewModel.ChromaticLower.Key = key;
                ViewModel.ChromaticLower.Mode = mode.Name;
            }

            // Compute available notes for UI
            ComputeAvailableNotes();

            // Populate detected chords so initial page shows chord list without user interaction
            PopulateChords();
        }

        private void ComputeAvailableNotes()
        {
            var set = new HashSet<int>();

            if (ViewModel.HarmonicaType == "Chromatic")
            {
                if (ViewModel.ChromaticUpper?.Holes != null)
                {
                    foreach (var h in ViewModel.ChromaticUpper.Holes)
                    {
                        set.Add(NoteNameToSemitone(h.Blow.Note));
                        set.Add(NoteNameToSemitone(h.Draw.Note));
                    }
                }
                if (ViewModel.ChromaticLower?.Holes != null)
                {
                    foreach (var h in ViewModel.ChromaticLower.Holes)
                    {
                        set.Add(NoteNameToSemitone(h.Blow.Note));
                        set.Add(NoteNameToSemitone(h.Draw.Note));
                    }
                }
            }
            else
            {
                if (ViewModel.Diatonic?.Holes != null)
                {
                    foreach (var h in ViewModel.Diatonic.Holes)
                    {
                        set.Add(NoteNameToSemitone(h.Blow.Note));
                        set.Add(NoteNameToSemitone(h.Draw.Note));
                    }
                }
            }

            var ordered = set.OrderBy(x => x).Select(x => SemitoneToName(x)).ToList();
            ViewModel.AvailableNotes = ordered;

            // Also compute missing notes (those semitones not present 0..11)
            var all = Enumerable.Range(0, 12).ToList();
            var missing = all.Where(a => !set.Contains(a)).OrderBy(x => x).Select(x => SemitoneToName(x)).ToList();
            ViewModel.MissingNotes = missing;
        }

        private void PopulateChords()
        {
            if (ViewModel.Diatonic != null)
            {
                ViewModel.Diatonic.Chords = _chordService.FindChords(ViewModel.Diatonic.Holes);
            }
            if (ViewModel.ChromaticUpper != null)
            {
                ViewModel.ChromaticUpper.Chords = _chordService.FindChords(ViewModel.ChromaticUpper.Holes);
            }
            if (ViewModel.ChromaticLower != null)
            {
                ViewModel.ChromaticLower.Chords = _chordService.FindChords(ViewModel.ChromaticLower.Holes);
            }
        }

        private Mode FindModeByName(IReadOnlyList<Scale> scales, string modeName)
        {
            if (string.IsNullOrWhiteSpace(modeName)) return null;
            return scales.SelectMany(s => s.Modes).FirstOrDefault(m => m.Name == modeName);
        }

        private List<HoleViewModel> BuildHolesFromTuning(Tuning tuning, int holes, string key, Mode mode)
        {
            // Use tuning.GetLayout so expansions that target the requested holes are applied
            var baseLayout = tuning.GetLayout(holes);
            var keySemitone = NoteNameToSemitone(key);
            var middleC = 60;

            var list = new List<HoleViewModel>();

            var intervals = mode?.Intervals ?? new List<int> { 0, 2, 4, 5, 7, 9, 11 };

            // Blow: group every 3 holes
            for (int i = 0; i < Math.Min(holes, baseLayout.Count); i++)
            {
                var h = baseLayout[i];

                int baseBlowMidi = ComputeBaseMidiFromDegreeOrNote(h.Blow, keySemitone, intervals, middleC);
                int blowGroup = i / 3; // groups of 3 holes
                int blowMidi = baseBlowMidi + blowGroup * 12;

                int drawMidi;

                // Use 10-hole draw grouping for all 10-hole tunings to keep conventional harmonica octave layout
                if (holes == 10)
                {
                    int drawGroup;
                    if (i <= 2) drawGroup = 0; // holes 1-3
                    else if (i <= 6) drawGroup = 1; // holes 4-7
                    else drawGroup = 2; // holes 8-10

                    int baseDrawMidi = ComputeBaseMidiFromDegreeOrNote(h.Draw, keySemitone, intervals, middleC);
                    drawMidi = baseDrawMidi + drawGroup * 12;
                }
                else
                {
                    // General draw handling: pick smallest octave so draw is >= previous draw and reasonably close to blow
                    int baseDrawMidi = ComputeBaseMidiFromDegreeOrNote(h.Draw, keySemitone, intervals, middleC);

                    // Start with same group as blow
                    drawMidi = baseDrawMidi + (i / 3) * 12;

                    // Move draw up until it's >= blow - 6 semitones (so draw sits near blow)
                    while (drawMidi < blowMidi - 6) drawMidi += 12;

                    // ensure non-decreasing across holes
                    if (list.Count > 0)
                    {
                        var prevDraw = list.Last().Draw;
                        var prevDrawMidi = (prevDraw.Octave + 1) * 12 + NoteNameToSemitone(prevDraw.Note);
                        while (drawMidi <= prevDrawMidi) drawMidi += 12;
                    }
                }

                var blowName = SemitoneToName(blowMidi % 12);
                var drawName = SemitoneToName(drawMidi % 12);
                var blowOct = (blowMidi / 12) - 1;
                var drawOct = (drawMidi / 12) - 1;

                list.Add(new HoleViewModel
                {
                    Index = h.Index,
                    Blow = new NoteCell { Note = blowName, Octave = blowOct, IsAltered = blowName.Contains("#") },
                    Draw = new NoteCell { Note = drawName, Octave = drawOct, IsAltered = drawName.Contains("#") }
                });
            }

            // Pad if needed
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

        private int ComputeBaseMidiFromDegreeOrNote(string degreeStr, int keySemitone, List<int> intervals, int middleCMidi)
        {
            if (string.IsNullOrWhiteSpace(degreeStr)) return middleCMidi + keySemitone;

            var s = degreeStr.Trim();

            // Support accidental prefixes like '#4' or 'b4' or '?4'/'?4'
            int accidental = 0;
            if (s.StartsWith("#") || s.StartsWith("?"))
            {
                accidental = 1;
                s = s.Substring(1);
            }
            else if (s.StartsWith("b") || s.StartsWith("?"))
            {
                accidental = -1;
                s = s.Substring(1);
            }

            if (int.TryParse(s, out var degree))
            {
                var idx = Math.Max(0, degree - 1);
                var interval = intervals[idx % intervals.Count] + accidental;
                // If accidental pushes interval outside 0-11, adjust octave accordingly
                var intervalMod = ((interval % 12) + 12) % 12;
                var octaveOffset = (int)Math.Floor((double)interval / 12);
                var midi = middleCMidi + keySemitone + intervalMod + octaveOffset * 12;
                return midi;
            }

            // fallback: try to parse literal note name possibly with octave
            var transposed = TransposeNoteName(degreeStr, keySemitone);
            var sem = NoteNameToSemitone(transposed.Name);
            var midiFromName = (transposed.Octave + 1) * 12 + sem;
            return midiFromName;
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
            // Use tuning.GetLayout so expansions for requested hole count are applied
            var baseLayout = tuning.GetLayout(holes);
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

        // Helpers
        private int NoteNameToSemitone(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            var n = name.Replace("\u00A0", "").Trim();
            if (n.Contains('/')) n = n.Split('/')[0];
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
            if (string.IsNullOrWhiteSpace(sourceName)) return (string.Empty, 4);
            var s = sourceName.Trim();
            int sourceOctave = 4;
            string notePart = s;
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
            var shift = (targetKeySemitone - 0 + 12) % 12;
            var finalSem = (sourceSem + shift) % 12;
            int octaveOffset = (sourceSem + shift) / 12;
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
