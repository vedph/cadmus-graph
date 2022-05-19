﻿using Fusi.Tools;
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
            return Regex.Replace(template, @"\${([^}]+)}", (Match m) =>
            {
                string id = m.Groups[0].Value;
                return _macros.ContainsKey(id) ? _macros[id].Run(Context) ?? "" : "";
            });
        }

        protected abstract string ResolveDataExpression(string expression);

        private string ResolveDataExpressions(string template)
        {
            return Regex.Replace(template, @"\@{(?<id>[^}]+)}", (Match m) =>
            {
                return ResolveDataExpression(m.Groups[1].Value);
            });
        }

        /// <summary>
        /// Fill the specified template by resolving macros (<c>!{...}</c>),
        /// metadata placeholders (<c>${...}</c>), and data expression
        /// placeholders <c>@{...}</c>.
        /// </summary>
        public string FillTemplate(string template)
        {
            if (template is null)
                throw new ArgumentNullException(nameof(template));

            // metadata
            string filled = TextTemplate.FillTemplate(template, Data, "${", "}");

            // node keys
            filled = TextTemplate.FillTemplate(filled, ContextNodes, "?{", "}");

            // macros
            filled = ResolveMacros(filled);

            // data expressions
            return ResolveDataExpressions(filled);
        }
    }
}
