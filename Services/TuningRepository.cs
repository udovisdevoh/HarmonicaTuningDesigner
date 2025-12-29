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
                .Select(tuningEl => new Tuning
                {
                    Id = (string)tuningEl.Attribute("id")!,
                    Name = (string)tuningEl.Attribute("name")!,
                    BaseHoles = (int)tuningEl.Element("Base")!.Attribute("holes")!,
                    BaseLayout = tuningEl.Element("Base")!
                        .Elements("Hole")
                        .Select(holeEl => new HoleDefinition
                        {
                            Index = (int)holeEl.Attribute("index")!,
                            Blow = holeEl.Element("Blow")!.Value,
                            Draw = holeEl.Element("Draw")!.Value
                        }).ToList()
                }).ToList();
        }
    }

}
