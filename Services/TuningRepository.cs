using System.Xml.Linq;

namespace HarmonicaTuningDesigner
{
    public class TuningRepository
    {
        private readonly List<Tuning> _tunings;

        public TuningRepository(string xmlPath)
        {
            _tunings = LoadTunings(xmlPath);
        }

        public IReadOnlyList<Tuning> GetAll() => _tunings;

        private static List<Tuning> LoadTunings(string path)
        {
            var doc = XDocument.Load(path);

            return doc.Root!
                .Elements("Tuning")
                .Select(tuningEl =>
                {
                    var baseEl = tuningEl.Element("Base")!;
                    var baseList = baseEl
                        .Elements("Hole")
                        .Select(holeEl => new HoleDefinition
                        {
                            Index = (int)holeEl.Attribute("index")!,
                            Blow = holeEl.Element("Blow")!.Value,
                            Draw = holeEl.Element("Draw")!.Value
                        })
                        .ToList();

                    var expansions = new List<Expansion>();
                    var expansionsEl = tuningEl.Element("Expansions");
                    if (expansionsEl != null)
                    {
                        expansions = expansionsEl.Elements("Expansion")
                            .Select(expEl => new Expansion
                            {
                                Holes = (int)expEl.Attribute("holes")!,
                                Prepend = expEl.Element("Prepend")?.Elements("Hole")
                                    .Select(h => new HoleDefinition
                                    {
                                        Index = 0,
                                        Blow = h.Element("Blow")!.Value,
                                        Draw = h.Element("Draw")!.Value
                                    }).ToList() ?? new List<HoleDefinition>(),
                                Append = expEl.Element("Append")?.Elements("Hole")
                                    .Select(h => new HoleDefinition
                                    {
                                        Index = 0,
                                        Blow = h.Element("Blow")!.Value,
                                        Draw = h.Element("Draw")!.Value
                                    }).ToList() ?? new List<HoleDefinition>()
                            }).ToList();
                    }

                    return new Tuning
                    {
                        Id = (string)tuningEl.Attribute("id")!,
                        Name = (string)tuningEl.Attribute("name")!,
                        BaseHoles = (int)baseEl.Attribute("holes")!,
                        BaseLayout = baseList,
                        Expansions = expansions
                    };
                }).ToList();
        }
    }
}
