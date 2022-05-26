using Fusi.Tools;
using System.Collections.Generic;

namespace Cadmus.Graph
{
    /// <summary>
    /// Graph source adapter. Implementors adapt a specific data source, like
    /// item, part, or thesaurus, to the graph mapping process.
    /// </summary>
    public interface IGraphSourceAdapter
    {
        /// <summary>
        /// Adapts the specified source.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="metadata">The target dictionary for metadata generated
        /// by the adapter.</param>
        /// <returns>The adaptation result, or null.</returns>
        object? Adapt(object source, IDictionary<string, object> metadata);
    }
}
