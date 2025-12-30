namespace HarmonicaTuningDesigner
{
    public class Tuning
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int BaseHoles { get; set; }
        public List<HoleDefinition> BaseLayout { get; set; } = new();

        // Expansions parsed from XML; not applied directly to BaseLayout
        public List<Expansion> Expansions { get; set; } = new();

        // Return the layout for a requested hole count; applies matching expansions up to that size
        public List<HoleDefinition> GetLayout(int holes)
        {
            // start from a shallow copy of base preserving original indices (1..BaseHoles)
            var layout = BaseLayout.Select(h => new HoleDefinition { Index = h.Index, Blow = h.Blow, Draw = h.Draw }).ToList();

            if (Expansions != null && Expansions.Any())
            {
                // Apply expansions in ascending holes order, but only those with Holes <= requested holes
                foreach (var exp in Expansions.OrderBy(e => e.Holes))
                {
                    if (exp.Holes > holes) break;

                    if (exp.Prepend != null && exp.Prepend.Any())
                    {
                        // compute index for new prepended holes so they become 0, -1, -2... with XML order preserved
                        var currentMin = layout.Min(h => h.Index);
                        var start = currentMin - exp.Prepend.Count;
                        var toInsert = exp.Prepend.Select(h => new HoleDefinition { Index = start++, Blow = h.Blow, Draw = h.Draw }).ToList();

                        layout.InsertRange(0, toInsert);
                    }

                    if (exp.Append != null && exp.Append.Any())
                    {
                        // assign indices after current max
                        var currentMax = layout.Max(h => h.Index);
                        var idx = currentMax + 1;
                        foreach (var h in exp.Append)
                        {
                            layout.Add(new HoleDefinition { Index = idx++, Blow = h.Blow, Draw = h.Draw });
                        }
                    }

                    // If we've reached or exceeded the requested holes, stop applying further expansions
                    if (layout.Count >= holes) break;
                }
            }

            // If more holes than requested, trim from the left (keep leftmost holes)
            if (layout.Count > holes)
            {
                layout = layout.Take(holes).ToList();
            }

            return layout;
        }
    }

    public class Expansion
    {
        public int Holes { get; set; }
        public List<HoleDefinition> Prepend { get; set; } = new();
        public List<HoleDefinition> Append { get; set; } = new();
    }
}
