using Xunit;
using System.Text.Json;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Cadmus.Graph.Test
{
    public sealed class JsonNodeMapperTest
    {
        private readonly string _json =
            "{ \"id\": \"colors\", \"entries\": " +
            "[ { \"id\": \"r\", \"value\": \"red\" }, " +
            "{ \"id\": \"g\", \"value\": \"green\" }, " +
            "{ \"id\": \"b\", \"value\": \"blue\" } ] } ";

        private static IList<NodeMapping> LoadMappings(string name)
        {
            using StreamReader reader = new(
                Assembly.GetExecutingAssembly()!
                .GetManifestResourceStream($"Cadmus.Graph.Test.Assets.{name}.json")!,
                Encoding.UTF8);

            JsonSerializerOptions options = new()
            {
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new NodeMappingOutputJsonConverter());

            return JsonSerializer.Deserialize<IList<NodeMapping>>(reader.ReadToEnd(),
                options) ?? Array.Empty<NodeMapping>();
        }

        [Fact]
        public void Map_Birth()
        {
            NodeMapping mapping = LoadMappings("Mappings")
                .First(m => m.Id == "events.type=birth");
            GraphSet set = new();

            JsonNodeMapper mapper = new();
            mapper.Map("sid", _json, mapping, set);

            // TODO add assertions like:
            Assert.Equal(2, set.Nodes.Count);

            //JmesPath jmes = new();
            //string? x = jmes.Transform(_json, "id");
            //JsonDocument doc = JsonDocument.Parse(x);
        }
    }
}