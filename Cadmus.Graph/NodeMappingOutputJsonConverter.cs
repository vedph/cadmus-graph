using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cadmus.Graph
{
    public class NodeMappingOutputJsonConverter : JsonConverter<NodeMappingOutput>
    {
        private static void ReadNodes(ref Utf8JsonReader reader,
            NodeMappingOutput output)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected object for output.nodes");

            reader.Read();
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                string? name = reader.GetString();
                if (reader.TokenType != JsonTokenType.PropertyName || name == null)
                    throw new JsonException("Expected property for output.nodes object");

                reader.Read();
                string nodeText = reader.GetString()
                    ?? throw new JsonException(
                        $"Expected string value after output.nodes['{name}']");

                MappedNode? node = MappedNode.Parse(nodeText);
                if (node == null) throw new JsonException("Invalid node: " + nodeText);
                output.Nodes[name] = node;
                reader.Read();
            }
        }

        private static void ReadTriples(ref Utf8JsonReader reader,
            NodeMappingOutput output)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected array for output.triples");

            reader.Read();
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                string? tripleText = reader.GetString();
                if (tripleText == null)
                    throw new JsonException("Expected string item in output.triples array");
                output.Triples.Add(MappedTriple.Parse(tripleText)
                    ?? throw new JsonException("Invalid triple: " + tripleText));
                reader.Read();
            }
        }

        public override NodeMappingOutput? Read(ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected output object");

            NodeMappingOutput output = new();

            // output.nodes and/or triples
            reader.Read();
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    switch (reader.GetString())
                    {
                        case "nodes":
                            reader.Read();
                            ReadNodes(ref reader, output);
                            break;
                        case "triples":
                            reader.Read();
                            ReadTriples(ref reader, output);
                            break;
                    }
                }
                if (!reader.Read()) break;
            }

            return output;
        }

        public override void Write(Utf8JsonWriter writer,
            NodeMappingOutput value,
            JsonSerializerOptions options)
        {
            if (value == null) return;

            writer.WriteStartObject("output");

            // nodes: []
            if (value.HasNodes)
            {
                writer.WriteStartArray();
                foreach (var p in value.Nodes)
                {
                    // "name":
                    writer.WriteStartObject(p.Key);
                    // "value" with form "uri label [tag]" where only "uri"
                    // is required
                    writer.WriteStringValue(p.Value.ToString());
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            // triples: []
            if (value.HasTriples)
            {
                writer.WriteStartArray();
                foreach (MappedTriple t in value.Triples)
                {
                    writer.WriteStringValue(t.ToString());
                }
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
    }
}
