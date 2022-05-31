using Cadmus.Graph.Adapters;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Cadmus.Graph
{
    public interface INodeMapper : IHasDataDictionary
    {
        /// <summary>
        /// Gets or sets the optional logger to use.
        /// </summary>
        ILogger? Logger { get; set; }

        /// <summary>
        /// Gets or sets the URI builder function. This is used to build URIs
        /// from SID and UID.
        /// </summary>
        public IUidBuilder UidBuilder { get; set; }

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
        /// Sets the macros to use in this mapper.
        /// </summary>
        /// <param name="macros">The macros.</param>
        void SetMacros(IDictionary<string, INodeMappingMacro>? macros);

        /// <summary>
        /// Maps the data <paramref name="source"/> into the graph set
        /// <paramref name="target"/>, using the specified
        /// <paramref name="mapping"/>.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="mapping">The mapping.</param>
        /// <param name="target">The target.</param>
        void Map(object source, NodeMapping mapping, GraphSet target);
    }
}
