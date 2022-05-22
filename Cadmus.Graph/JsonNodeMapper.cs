﻿using Fusi.Tools.Config;
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
        private string _sourceType;

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

            return JsonDocument.Parse(result).RootElement.ToString() ?? "";
        }

        private void AddNodes(string sid, NodeMapping mapping, GraphSet target)
        {
            foreach (var p in mapping.Output!.Nodes)
            {
                string uri = UidBuilder.Build(sid,
                    FillTemplate(p.Value.Uid!, true));
                UriNode node = new()
                {
                    Uri = uri,
                    SourceType = _sourceType,
                    Sid = sid,
                    Label = p.Value.Label ?? uri
                };
                ContextNodes[p.Key] = node;
                target.Nodes.Add(node);
            }
        }

        private void AddTriples(string sid, NodeMapping mapping, GraphSet target)
        {
            int n = 0;
            foreach (MappedTriple tripleSource in mapping.Output!.Triples)
            {
                n++;
                if (string.IsNullOrEmpty(tripleSource.S))
                {
                    throw new CadmusGraphException(
                        $"Undefined triple subject at mapping #{n}: {mapping}");
                }
                if (string.IsNullOrEmpty(tripleSource.P))
                {
                    throw new CadmusGraphException(
                        $"Undefined triple predicate at mapping #{n}: {mapping}");
                }
                if (string.IsNullOrEmpty(tripleSource.O) && tripleSource.OL == null)
                {
                    throw new CadmusGraphException(
                        $"Undefined triple object at mapping #{n}: {mapping}");
                }

                UriTriple triple = new()
                {
                    Sid = sid,
                    SubjectUri = FillTemplate(tripleSource.S!, true),
                    PredicateUri = FillTemplate(tripleSource.P!, true),
                    ObjectUri = FillTemplate(tripleSource.O!, true),
                    ObjectLiteral = tripleSource.OL != null
                        ? FillTemplate(tripleSource.OL, false)
                        : null
                };
                target.Triples.Add(triple);
            }
        }

        private void BuildOutput(string sid, NodeMapping mapping, GraphSet target)
        {
            if (mapping.Output == null) return;

            // nodes
            if (mapping.Output.HasNodes) AddNodes(sid, mapping, target);
            // triples
            if (mapping.Output.HasTriples) AddTriples(sid, mapping, target);
        }

        private void ApplyMapping(string? sid, string json, NodeMapping mapping,
            GraphSet target, int itemIndex = -1)
        {
            Logger?.LogDebug("Mapping " + mapping);

            // generate SID if required
            if (sid == null && mapping.Sid != null)
            {
                _doc = JsonDocument.Parse(json);
                sid = FillTemplate(mapping.Sid!, false);
            }

            // if we're dealing with an array's item, we do not want to compute
            // the mapping's expression, but just use the received json
            // representing the item itself.
            string? result;
            if (itemIndex == -1)
            {
                try
                {
                    result = mapping.Source == "."
                        ? json
                        : _jmes.Transform(json, mapping.Source);
                }
                catch (Exception ex)
                {
                    throw new AggregateException(
                        $"Eval error \"{ex.Message}\" at mapping {mapping}", ex);
                }
            }
            else result = json;

            // get the result into the current document
            _doc = JsonDocument.Parse(result);

            // process it according to its root type:
            switch (_doc.RootElement.ValueKind)
            {
                // null or undefined does not trigger output
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    break;
                case JsonValueKind.Object:
                    if (mapping.Output != null)
                        BuildOutput(sid, mapping, target);
                    break;
                // an array does not trigger output, but applies its mapping
                // to each of its items
                case JsonValueKind.Array:
                    int index = 0;
                    foreach (JsonElement item in
                        _doc.RootElement.EnumerateArray())
                    {
                        Data["index"] = index;
                        ApplyMapping(sid, item.GetRawText(),
                            mapping, target, index++);
                    }
                    Data.Remove("index");
                    break;
                // else it's a terminal, build output
                default:
                    // set current leaf variable
                    Data["."] = _doc.RootElement.ToString();
                    if (mapping.Output != null)
                        BuildOutput(sid, mapping, target);
                    break;
            }
            _doc = null;

            // process this mapping's children recursively
            if (mapping.HasChildren)
            {
                foreach (NodeMapping child in mapping.Children!)
                    ApplyMapping(sid, result, child, target);
            }
        }

        /// <summary>
        /// Map the specified source into the <paramref name="target"/> graphset.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="mapping">The mapping to apply.</param>
        /// <param name="target">The target graphset.</param>
        /// <exception cref="ArgumentNullException">mapping or target</exception>
        public void Map(object source, NodeMapping mapping, GraphSet target)
        {
            if (mapping is null)
                throw new ArgumentNullException(nameof(mapping));
            if (target is null)
                throw new ArgumentNullException(nameof(target));

            // source is JSON
            string? json = source as string;
            if (string.IsNullOrEmpty(json)) return;

            // set source type once for all the descendants
            _sourceType = mapping.SourceType
                ?? throw new CadmusGraphException(
                    "Source type not set for mapping " + mapping);

            ApplyMapping(null, json, mapping, target);
        }
    }
}
