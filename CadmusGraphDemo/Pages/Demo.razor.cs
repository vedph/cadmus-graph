using Cadmus.Graph;
using Microsoft.AspNetCore.Components.Forms;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace CadmusGraphDemo.Pages
{
    public partial class Demo
    {
        private readonly JsonNodeMapper _mapper;

        private DemoModel Model { get; }
        private EditContext Context { get; }

        public Demo()
        {
            _mapper = new();
            Model = new DemoModel
            {
                Input = LoadResourceText("Events.json"),
                Mappings = LoadResourceText("Mappings.json"),
            };
            Context = new EditContext(Model);
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
            Model.Error = null;
            if (string.IsNullOrEmpty(Model.Input) ||
                string.IsNullOrEmpty(Model.Mappings))
            {
                return;
            }

            try
            {
                GraphSet set = new();
                IList<NodeMapping> mappings = LoadMappings(Model.Mappings);
                int i = 0;
                foreach (NodeMapping mapping in mappings)
                {
                    _mapper.Map($"m_{i++}", Model.Input, mapping, set);
                }
                Model.Graph = set;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Model.Error = ex.Message;
            }
        }
    }
}
