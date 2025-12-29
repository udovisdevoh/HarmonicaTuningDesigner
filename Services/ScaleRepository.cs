using System.Xml.Linq;

namespace HarmonicaTuningDesigner
{
    public class ScaleRepository
    {
        private readonly List<Scale> _scales;

        public ScaleRepository(string xmlPath)
        {
            _scales = LoadScales(xmlPath);
        }

        public IReadOnlyList<Scale> GetAll() => _scales;

        private static List<Scale> LoadScales(string path)
        {
            var doc = XDocument.Load(path);

            return doc.Root!
                .Elements("Scale")
                .Select(scaleEl => new Scale
                {
                    Id = (string)scaleEl.Attribute("id")!,
                    Name = (string)scaleEl.Attribute("name")!,
                    Intervals = ParseIntervals(scaleEl.Element("Intervals")?.Value),
                    Modes = scaleEl.Element("Modes")!
                        .Elements("Mode")
                        .Select(modeEl => new Mode
                        {
                            Id = (string)modeEl.Attribute("id")!,
                            Name = (string)modeEl.Attribute("name")!,
                            Degree = (int)modeEl.Attribute("degree")!,
                            Intervals = ParseIntervals(modeEl.Element("Intervals")?.Value)
                        }).ToList()
                }).ToList();
        }

        private static List<int> ParseIntervals(string? raw)
            => raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(int.Parse)
                   .ToList() ?? new();
    }
}
