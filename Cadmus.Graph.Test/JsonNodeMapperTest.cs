using Xunit;
using System.Text.Json;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;
using DevLab.JmesPath;

namespace Cadmus.Graph.Test
{
    public sealed class JsonNodeMapperTest
    {
        private readonly string _json =
            "{ \"id\": " +
            "\"colors\", \"entries\": " +
            "[ { \"id\": \"r\", \"value\": \"red\" }, " +
            "{ \"id\": \"g\", \"value\": \"green\" }, " +
            "{ \"id\": \"b\", \"value\": \"blue\" } ], " +
            "\"size\": { \"w\": 21, \"h\": 29.7 } } ";

        private static Stream GetResourceStream(string name)
        {
            return Assembly.GetExecutingAssembly()!
                .GetManifestResourceStream($"Cadmus.Graph.Test.Assets.{name}")!;
        }

        private static string LoadResourceText(string name)
        {
            using StreamReader reader = new(GetResourceStream(name),
                Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static IList<NodeMapping> LoadMappings(string name)
        {
            using StreamReader reader = new(GetResourceStream(name),
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

        private static void ResetMapperMetadata(INodeMapper mapper)
        {
            mapper.Data.Clear();
            mapper.Data["item"] = new UriNode
            {
                Id = 1,
                Label = "My item",
                Sid = Guid.NewGuid().ToString(),
                Uri = "x:items/my-item"
            };
            mapper.Data["item-id"] = Guid.NewGuid().ToString();
            mapper.Data["part-id"] = Guid.NewGuid().ToString();
            mapper.Data["group-id"] = "group";
            mapper.Data["facet-id"] = "facet";
            mapper.Data["flags"] = "3";
        }

        [Fact]
        public void Map_Birth()
        {
            // @@
            //JmesPath jmes = new();
            //string r = jmes.Transform(_json, "id");
            //r = jmes.Transform(_json, "entries");
            //r = jmes.Transform(_json, "entries[0].id");
            //r = jmes.Transform(_json, "size");
            //r = jmes.Transform(_json, "size.h");
            //r = jmes.Transform(_json, "x");
            // @@

            NodeMapping mapping = LoadMappings("Mappings.json")
                .First(m => m.Id == "events.type=birth");
            GraphSet set = new();

            JsonNodeMapper mapper = new();
            ResetMapperMetadata(mapper);
            string json = LoadResourceText("Events.json");
            mapper.Map(json, mapping, set);

            // TODO add assertions like:
            Assert.Equal(2, set.Nodes.Count);

            //JmesPath jmes = new();
            //string? x = jmes.Transform(_json, "id");
            //JsonDocument doc = JsonDocument.Parse(x);
        }
    }
}