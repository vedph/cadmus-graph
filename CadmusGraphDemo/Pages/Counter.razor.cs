using Cadmus.Graph;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace CadmusGraphDemo.Pages
{
    public partial class Counter
    {
        private readonly JsonNodeMapper _mapper;
        public MappingModel Mapping { get; set; }

        public Counter()
        {
            _mapper = new();
            Mapping = new()
            {
                Input = LoadResourceText("Events.json"),
                Mappings = LoadResourceText("Mappings.json")
            };
        }

        private static Stream GetResourceStream(string name)
        {
            return Assembly.GetExecutingAssembly()!
                .GetManifestResourceStream($"CadmusGraphDemo.Assets.{name}")!;
        }

        private static string LoadResourceText(string name)
        {
            using StreamReader reader = new(GetResourceStream(name),
                Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static IList<NodeMapping> LoadMappings(string json)
        {
            JsonSerializerOptions options = new()
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new NodeMappingOutputJsonConverter());

            return JsonSerializer.Deserialize<IList<NodeMapping>>(json,
                options) ?? Array.Empty<NodeMapping>();
        }

        private void Map()
        {
            Mapping.Error = null;
            if (string.IsNullOrEmpty(Mapping.Input) ||
                string.IsNullOrEmpty(Mapping.Mappings))
            {
                return;
            }

            try
            {
                GraphSet set = new();
                IList<NodeMapping> mappings = LoadMappings(Mapping.Mappings);
                int i = 0;
                foreach (NodeMapping mapping in mappings)
                {
                    _mapper.Map($"m_{i++}", Mapping.Input, mapping, set);
                }
                Mapping.Graph = set;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Mapping.Error = ex.Message;
            }
        }
    }
}
