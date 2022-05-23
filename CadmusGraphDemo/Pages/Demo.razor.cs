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
        private readonly List<NodeMapping> _mappings;

        private DemoModel Model { get; }
        private EditContext Context { get; }

        public Demo()
        {
            _mapper = new();
            _mappings = new();
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

        private void LoadMappings()
        {
            try
            {
                _mappings.Clear();
                JsonSerializerOptions options = new()
                {
                    AllowTrailingCommas = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new NodeMappingOutputJsonConverter());

                _mappings.AddRange(JsonSerializer.Deserialize<IList<NodeMapping>>(
                    Model.Mappings ?? "{}",
                    options) ?? Array.Empty<NodeMapping>());

                Model.MappingCount = _mappings.Count;
            }
            catch (Exception ex)
            {
                Model.Error = ex.Message;
            }
        }

        private async Task Map()
        {
            Model.Error = null;
            if (string.IsNullOrEmpty(Model.Input) ||
                string.IsNullOrEmpty(Model.Mappings))
            {
                return;
            }

            try
            {
                if (_mappings.Count == 0) LoadMappings();

                Model.IsRunning = true;
                // https://stackoverflow.com/questions/56604886/blazor-display-wait-or-spinner-on-api-call
                await Task.Delay(1);

                // setup context
                GraphSet set = new();
                _mapper.Data.Clear();

                // mock metadata from item
                _mapper.Data["item-id"] = Guid.NewGuid().ToString();
                _mapper.Data["item-uri"] = "x:items/my-item";
                _mapper.Data["item-label"] = "Petrarch";
                _mapper.Data["group-id"] = "group";
                _mapper.Data["facet-id"] = "facet";
                _mapper.Data["flags"] = "3";
                // mock metada from part
                _mapper.Data["part-id"] = Guid.NewGuid().ToString();

                // apply mappings
                await Task.Run(() =>
                {
                    foreach (NodeMapping mapping in _mappings)
                        _mapper.Map(Model.Input, mapping, set);
                });
                Model.Graph = set;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Model.Error = ex.Message;
            }
            finally
            {
                Model.IsRunning = false;
                await Task.Delay(1);
            }
        }
    }
}
