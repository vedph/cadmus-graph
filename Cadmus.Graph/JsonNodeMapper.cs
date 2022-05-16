using Fusi.Tools.Config;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using DevLab.JmesPath;

namespace Cadmus.Graph
{
    /// <summary>
    /// JSON-based node mapper.
    /// <para>Tag: <c>node-mapper.json</c>.</para>
    /// </summary>
    /// <seealso cref="NodeMapper" />
    /// <seealso cref="INodeMapper" />
    [Tag("node-mapper.json")]
    public sealed class JsonNodeMapper : NodeMapper, INodeMapper
    {
        private readonly JmesPath _jmes;
        private JsonDocument? _doc;

        public JsonNodeMapper()
        {
            _jmes = new();
        }

        protected override string ResolveDataExpression(string expression)
        {
            if (_doc == null) return "";

            // corner case: "." just means current value.
            // Also, any value kind not being an object or an array is just
            // a primitive, so we end up returning the current value.
            if (expression == "." ||
                (_doc.RootElement.ValueKind != JsonValueKind.Object
                && _doc.RootElement.ValueKind != JsonValueKind.Array))
            {
                return _doc.RootElement.ToString();
            }

            // else evaluate expression from current object/array
            string json = JsonSerializer.Serialize(_doc.RootElement);
            string? result = _jmes.Transform(json, expression);
            if (string.IsNullOrEmpty(result)) return "";
            return JsonDocument.Parse(result).ToString() ?? "";
        }

        private void AddNodes(string sid, NodeMapping mapping, GraphSet target)
        {
            foreach (var p in mapping.Output!.Nodes)
            {
                string uri = UriBuilder(sid, FillTemplate(p.Value.Uid!));
                UriNode node = new()
                {
                    Uri = uri,
                    SourceType = mapping.SourceType,
                    Sid = sid,
                    Label = p.Value.Label ?? uri
                };
                ContextNodes[p.Key] = node;
                target.Nodes.Add(node);
            }
        }

        private void AddTriples(string sid, NodeMapping mapping, GraphSet target)
        {
            foreach (MappedTriple tripleSource in mapping.Output!.Triples)
            {
                UriTriple triple = new()
                {
                    Sid = sid,
                    SubjectUri = tripleSource.S,
                    PredicateUri = tripleSource.P,
                    ObjectUri = tripleSource.O,
                    ObjectLiteral = tripleSource.OL != null
                        ? FillTemplate(tripleSource.OL)
                        : null
                };
                target.Triples.Add(triple);
            }
        }

        private void ApplyMapping(string sid, string json, NodeMapping mapping,
            GraphSet target)
        {
            Logger?.LogDebug("Mapping " + mapping);
            string? result = _jmes.Transform(json, mapping.Source);

            if (mapping.Output != null)
            {
                _doc = JsonDocument.Parse(result);

                // nodes
                if (mapping.Output.HasNodes) AddNodes(sid, mapping, target);

                // triples
                if (mapping.Output.HasTriples) AddTriples(sid, mapping, target);

                _doc = null;
            }
            if (mapping.HasChildren)
            {
                foreach (NodeMapping child in mapping.Children!)
                    ApplyMapping(sid, result, child, target);
            }
        }

        /// <summary>
        /// Map the specified source into the <paramref name="target"/> graphset.
        /// </summary>
        /// <param name="sid">The source SID.</param>
        /// <param name="source">The source object.</param>
        /// <param name="mapping">The mapping to apply.</param>
        /// <param name="target">The target graphset.</param>
        /// <exception cref="ArgumentNullException">sid, mapping or target
        /// </exception>
        public void Map(string sid, object source, NodeMapping mapping,
            GraphSet target)
        {
            if (sid is null)
                throw new ArgumentNullException(nameof(sid));
            if (mapping is null)
                throw new ArgumentNullException(nameof(mapping));
            if (target is null)
                throw new ArgumentNullException(nameof(target));

            // source is JSON
            string? json = source as string;
            if (string.IsNullOrEmpty(json)) return;

            ApplyMapping(sid, json, mapping, target);
        }
    }
}
