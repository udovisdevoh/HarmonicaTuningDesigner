using System.Text.RegularExpressions;

namespace HarmonicaTuningDesigner
{
    public enum ChordType
    {
        Major,
        Minor,
        Diminished,
        Augmented
    }

    public record ChordMatch(string Root, ChordType Type, int StartHoleIndex, int EndHoleIndex, bool IsBlow, List<string> Notes);

    // Service to detect chords in a sequence of notes. Notes expected as note names (C, C#, D...)
    public class ChordService
    {
        private static readonly Dictionary<ChordType, int[]> Intervals = new()
        {
            { ChordType.Major, new[] { 0, 4, 7 } },
            { ChordType.Minor, new[] { 0, 3, 7 } },
            { ChordType.Diminished, new[] { 0, 3, 6 } },
            { ChordType.Augmented, new[] { 0, 4, 8 } }
        };

        private readonly string[] _names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private readonly Dictionary<string,int> _map;

        public ChordService()
        {
            _map = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _names.Length; i++) _map[_names[i]] = i;
            // add flats variants
            _map["Db"] = _map["C#"];
            _map["Eb"] = _map["D#"];
            _map["Gb"] = _map["F#"];
            _map["Ab"] = _map["G#"];
            _map["Bb"] = _map["A#"];
        }

        // Find contiguous sequences of either all-blow or all-draw notes that form triads or multiples thereof.
        // Accepts sequences where notes may repeat and inversions are allowed (order doesn't matter).
        public List<ChordMatch> FindChords(List<HoleViewModel> holes)
        {
            var results = new List<ChordMatch>();
            if (holes == null || holes.Count == 0) return results;

            // Build two sequences: blow notes and draw notes as semitone indices per hole index
            var blowSeq = holes.Select(h => (Name: NormalizeName(h.Blow.Note), Semitone: NoteToSemitoneSafe(h.Blow.Note))).ToList();
            var drawSeq = holes.Select(h => (Name: NormalizeName(h.Draw.Note), Semitone: NoteToSemitoneSafe(h.Draw.Note))).ToList();

            // Search contiguous runs up to the full length
            results.AddRange(FindChordsInSequence(blowSeq, true));
            results.AddRange(FindChordsInSequence(drawSeq, false));

            return results.OrderBy(r => r.StartHoleIndex).ToList();
        }

        private IEnumerable<ChordMatch> FindChordsInSequence(List<(string Name,int Semitone)> seq, bool isBlow)
        {
            var results = new List<ChordMatch>();
            int n = seq.Count;
            for (int start = 0; start < n; start++)
            {
                for (int end = start; end < n; end++)
                {
                    // Extract subarray
                    var sub = seq.GetRange(start, end - start + 1);
                    // Must contain at least 3 notes to consider triads
                    if (sub.Count < 3) continue;

                    var sems = sub.Select(s => s.Semitone).Where(s => s >= 0).ToList();
                    if (sems.Count == 0) continue;

                    // We allow repeats: so look for whether the multiset of semitones can be partitioned into groups that each contain a triad (with possible duplicates)
                    // For simplicity: try any combination of 3 semitones from the sub (order not important) and check if they form a chord intervals set (mod12).
                    // If any combination of three unique positions forms a chord, consider the entire window matching that chord type.

                    bool matched = false;
                    ChordType matchedType = ChordType.Major;
                    int matchedRoot = 0;

                    // Build unique semitone set for testing inversions
                    var uniq = sems.Select(s => ((s % 12) + 12) % 12).Distinct().ToList();

                    // If less than 2 distinct pitch classes, skip
                    if (uniq.Count < 2) continue;

                    foreach (var type in Intervals.Keys)
                    {
                        var ints = Intervals[type];
                        // try each possible root 0..11 compare
                        for (int root = 0; root < 12; root++)
                        {
                            var required = new HashSet<int>(ints.Select(i => (root + i) % 12));
                            // If required is subset of uniq then the window contains those pitch classes as a subset
                            if (required.IsSubsetOf(uniq))
                            {
                                matched = true;
                                matchedType = type;
                                matchedRoot = root;
                                break;
                            }
                        }
                        if (matched) break;
                    }

                    if (matched)
                    {
                        var notes = sub.Select(s => s.Name).ToList();
                        var rootName = _names[matchedRoot];
                        results.Add(new ChordMatch(rootName, matchedType, start + 1, end + 1, isBlow, notes));
                        // Skip overlapping windows that start inside this window to avoid many duplicates: move start forward
                        // We'll still catch longer windows in other iterations
                    }
                }
            }

            return results;
        }

        private int NoteToSemitoneSafe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            var n = NormalizeName(name);
            if (_map.TryGetValue(n, out var s)) return s;
            return -1;
        }

        private string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var n = name.Trim();
            if (n.Contains('/')) n = n.Split('/')[0];
            // Remove digits if present
            n = Regex.Replace(n, "\\d+$", "");
            return n;
        }
    }
}
