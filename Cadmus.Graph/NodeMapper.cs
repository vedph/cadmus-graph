using Cadmus.Graph.Macros;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Globalization;
using Cadmus.Graph.Adapters;
using Fusi.Text.Unicode;
using System.Text.RegularExpressions;

namespace Cadmus.Graph
{
    /// <summary>
    /// Base class for node mappers.
    /// </summary>
    public abstract class NodeMapper : DataDictionary
    {
        private readonly Dictionary<string, INodeMappingMacro> _macros;

        /// <summary>
        /// Gets or sets the optional logger to use.
        /// </summary>
        public ILogger? Logger { get; set; }

        /// <summary>
        /// Gets or sets the URI builder function. This is used to build URIs
        /// from SID and UID.
        /// </summary>
        public IUidBuilder UidBuilder { get; set; }

        /// <summary>
        /// Gets or sets the optional macro functions eventually used to resolve
        /// placeholders during the mapping process. Each macro gets an object
        /// representing the mapping context, and returns a computed value.
        /// </summary>
        protected IDictionary<string, INodeMappingMacro> Macros => _macros;

        /// <summary>
        /// The object representing the mapping source context, usually
        /// corresponding to the context of mapping's source, like an
        /// item and/or a part.
        /// The source is directly passed to <see cref="INodeMapper.Map"/>;
        /// this rather refers to the source's context. For instance, when
        /// mapping a part you would still need to know about its parent item.
        /// </summary>
        public GraphSource? Context { get; set; }

        /// <summary>
        /// Gets or sets the context nodes of this mapper. These are the nodes
        /// created during the mapping process, keyed under some arbitrary
        /// identifier defined in the mapping configuration.
        /// </summary>
        protected IDictionary<string, UriNode> ContextNodes { get; }

        /// <summary>
        /// Creates a new instance of the <see cref="NodeMapper"/> object.
        /// </summary>
        protected NodeMapper()
        {
            _macros = new Dictionary<string, INodeMappingMacro>();
            ContextNodes = new Dictionary<string, UriNode>();
            // by default use a RAM-based builder
            UidBuilder = new RamUidBuilder();

            // builtin macros
            _macros["_hdate"] = new HistoricalDateMacro();
        }

        public void SetMacros(IDictionary<string, INodeMappingMacro>? macros)
        {
            _macros.Clear();
            if (macros != null)
            {
                foreach (var p in macros) _macros[p.Key] = p.Value;
            }
        }

        private string ResolveMacro(string macro)
        {
            // syntax of placeholder's value for macro is:
            // id or id(" & "-delimited args)
            int i = macro.IndexOf('(');

            string id;
            string[]? args = null;
            if (i == -1)
            {
                id = macro;
            }
            else
            {
                id = macro[..i];
                if (macro[^1] == ')') macro = macro[..^1];
                args = macro[(i + 1)..].Split(" & ");
            }

            return _macros.ContainsKey(id)
                ? _macros[id].Run(Context, args) ?? ""
                : "";
        }

        protected abstract string ResolveDataExpression(string expression);

        private string ResolveNode(string template)
        {
            // - {?node} or {?node:uri} => uri
            // - {?node:label} => label
            // - {?node:sid} => sid
            // - {?node:src_type} => source type
            string key;
            string? prop = null;
            int i = template.LastIndexOf(':');
            if (i > -1)
            {
                key = template[..i];
                prop = template[(i + 1)..];
            }
            else
            {
                key = template;
            }
            if (!ContextNodes.ContainsKey(key)) return "";
            UriNode node = ContextNodes[key];

            return prop switch
            {
                "label" => node.Label ?? "",
                "sid" => node.Sid ?? "",
                "src_type" => node.SourceType.ToString(CultureInfo.InvariantCulture),
                _ => node.Uri ?? "",
            };
        }

        private string ResolveNode(TemplateNode node)
        {
            if (node.ChildrenCount == 0) return "";

            StringBuilder sb = new();
            foreach (TemplateNode child in node.Children
                .Where(child => child.Value != null))
            {
                sb.Append(child.Value);
            }

            string value = sb.ToString();
            switch (node.Type)
            {
                case TemplateNodeType.Node:
                    return ResolveNode(value);

                case TemplateNodeType.Metadatum:
                    if (Data.ContainsKey(value))
                        return Data[value]?.ToString() ?? "";
                    break;

                case TemplateNodeType.Expression:
                    return ResolveDataExpression(value);

                case TemplateNodeType.Macro:
                    return ResolveMacro(value);
            }
            return "";
        }

        /// <summary>
        /// Fill the specified template by resolving macros (<c>!{...}</c>),
        /// node placeholders (<c>?{...}</c>), metadata placeholders
        /// (<c>${...}</c>), and data expression placeholders <c>@{...}</c>.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <param name="uidFilter">True to apply <see cref="UidFilter"/> to
        /// the result before returning it.</param>
        public string ResolveTemplate(string template, bool uidFilter)
        {
            if (template is null)
                throw new ArgumentNullException(nameof(template));

            TemplateTree tree = TemplateTree.Create(template);
            string resolved = tree.Resolve(ResolveNode);
            return uidFilter ? UidFilter.Apply(resolved) : resolved;
        }
    }
}
