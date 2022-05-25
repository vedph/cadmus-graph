using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cadmus.Graph
{
    /// <summary>
    /// JSON converter for <see cref="NodeMappingOutput"/>. This is used
    /// to serialize and deserialize mappings using a more human-readable format.
    /// </summary>
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

        private static void ReadMetadata(ref Utf8JsonReader reader,
            NodeMappingOutput output)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected object for output.metadata");

            reader.Read();
            while (reader.TokenType != JsonTokenType.EndObject)
            {
                string? name = reader.GetString();
                if (reader.TokenType != JsonTokenType.PropertyName || name == null)
                    throw new JsonException("Expected property for output.metadata object");

                reader.Read();
                output.Metadata[name] = reader.GetString()
                    ?? throw new JsonException(
                        $"Expected string value after output.metadata['{name}']");
                reader.Read();
            }
        }

        /// <summary>
        /// Read the object.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="typeToConvert">Type to convert.</param>
        /// <param name="options">Options.</param>
        /// <returns>Object read or null.</returns>
        /// <exception cref="JsonException">error</exception>
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
                        case "metadata":
                            reader.Read();
                            ReadMetadata(ref reader, output);
                            break;
                        default:
                            throw new JsonException(
                                "Unexpected property in output object: " +
                                reader.GetString());
                    }
                }
                if (!reader.Read()) break;
            }

            return output;
        }

        /// <summary>
        /// Write the object.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="options">Options.</param>
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
