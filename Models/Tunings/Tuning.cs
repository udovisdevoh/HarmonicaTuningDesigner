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
            // start from a shallow copy of base
            var layout = BaseLayout.Select(h => new HoleDefinition { Index = h.Index, Blow = h.Blow, Draw = h.Draw }).ToList();

            if (Expansions != null && Expansions.Any())
            {
                // Apply expansions in ascending holes order, but only those with Holes <= requested holes
                foreach (var exp in Expansions.OrderBy(e => e.Holes))
                {
                    if (exp.Holes > holes) break;

                    if (exp.Prepend != null && exp.Prepend.Any())
                    {
                        layout.InsertRange(0, exp.Prepend.Select(h => new HoleDefinition { Index = 0, Blow = h.Blow, Draw = h.Draw }));
                    }

                    if (exp.Append != null && exp.Append.Any())
                    {
                        layout.AddRange(exp.Append.Select(h => new HoleDefinition { Index = 0, Blow = h.Blow, Draw = h.Draw }));
                    }

                    // If we've reached or exceeded the requested holes, stop applying further expansions
                    if (layout.Count >= holes) break;
                }
            }

            // Reindex
            for (int i = 0; i < layout.Count; i++)
            {
                layout[i].Index = i + 1;
            }

            // Trim if necessary
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
