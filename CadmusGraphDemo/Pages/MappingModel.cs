using Cadmus.Graph;

namespace CadmusGraphDemo.Pages
{
    public class MappingModel
    {
        public string? Input { get; set; }
        public string? Mappings { get; set; }
        public GraphSet Graph { get; set; }
        public string? Error { get; set; }

        public MappingModel()
        {
            Graph = new GraphSet();
        }
    }
}
