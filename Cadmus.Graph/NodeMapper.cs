using Fusi.Tools;
using Fusi.Tools.Text;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
        public Func<string, string, string> UriBuilder { get; set; }

        /// <summary>
        /// Gets or sets the optional macro functions eventually used to resolve
        /// placeholders during the mapping process. Each macro gets an object
        /// representing the mapping context, and returns a computed value.
        /// </summary>
        protected IDictionary<string, INodeMappingMacro> Macros => _macros;

        /// <summary>
        /// The object representing the mapping context, usually corresponding
        /// to the context of mapping's source, like an item, a part, or a
        /// thesaurus. The source is directly passed to <see cref="INodeMapper.Map"/>;
        /// this rather refers to the source's context. For instance, when
        /// mapping a part you would still need to know about its parent item.
        /// </summary>
        public object? Context { get; set; }

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

            UriBuilder = new Func<string, string, string>(
                (string uid, string sid) => sid + "/" + uid);
        }

        public void SetMacros(IList<INodeMappingMacro>? macros)
        {
            _macros.Clear();
            if (macros != null)
            {
                foreach (var p in macros) _macros[p.Id] = p;
            }
        }

        private string ResolveMacros(string template)
        {
            return Regex.Replace(template, @"!{([^}]+)}", (Match m) =>
            {
                string id = m.Groups[0].Value;
                return _macros.ContainsKey(id) ? _macros[id].Run(Context) ?? "" : "";
            });
        }

        protected abstract string ResolveDataExpression(string expression);

        private string ResolveNode(string template)
        {
            // - ?{node} or ?{node:uri} => uri
            // - ?{node:label} => label
            // - ?{node:sid} => sid
            // - ?{node:src_type} => source type
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
                "src_type" => node.SourceType ?? "",
                _ => node.Uri ?? "",
            };
        }

        /// <summary>
        /// Fill the specified template by resolving macros (<c>!{...}</c>),
        /// node placeholders (<c>?{...}</c>), metadata placeholders
        /// (<c>${...}</c>), and data expression placeholders <c>@{...}</c>.
        /// </summary>
        public string FillTemplate(string template)
        {
            if (template is null)
                throw new ArgumentNullException(nameof(template));

            // expressions (@)
            string filled = TextTemplate.FillTemplate(template,
                id => ResolveDataExpression(id), "@{", "}");

            // node keys (?)
            filled = TextTemplate.FillTemplate(filled,
                id => ResolveNode(id), "?{", "}");

            // metadata ($)
            filled = TextTemplate.FillTemplate(filled, Data, "${", "}");

            // macros (!)
            return ResolveMacros(filled);
        }
    }
}
