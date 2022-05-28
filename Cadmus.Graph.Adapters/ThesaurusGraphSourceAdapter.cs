using Cadmus.Core.Config;
using System.Collections.Generic;

namespace Cadmus.Graph.Adapters
{
    /// <summary>
    /// Graph source adapter for <see cref="Thesaurus"/>.
    /// </summary>
    /// <seealso cref="JsonGraphSourceAdapter" />
    /// <seealso cref="IGraphSourceAdapter" />
    public sealed class ThesaurusGraphSourceAdapter : JsonGraphSourceAdapter,
        IGraphSourceAdapter
    {
        /// <summary>
        /// The thesaurus identifier metadata key.
        /// </summary>
        public const string M_THESAURUS_ID = "thesaurus-id";

        /// <summary>
        /// The thesaurus language metadata key.
        /// </summary>
        public const string M_THESAURUS_LANG = "thesaurus-lang";

        /// <summary>
        /// Adapt the source to the mapping process, eventually also setting
        /// <paramref name="filter" /> and <paramref name="metadata" />
        /// accordingly.
        /// </summary>
        /// <param name="source">The source. This must be an object implementing
        /// <see cref="IPart"/>.</param>
        /// <param name="filter">The filter to set.</param>
        /// <param name="metadata">The metadata to set.</param>
        /// <returns>
        /// Adapted object or null.
        /// </returns>
        protected override object? Adapt(GraphSource source,
            RunNodeMappingFilter filter, IDictionary<string, string> metadata)
        {
            ItemGraphSourceAdapter.ExtractItemMetadata(source, filter, metadata);

            Thesaurus? thesaurus = source.Thesaurus;
            if (thesaurus == null) return null;

            // filter
            filter.SourceType = NodeMapping.SOURCE_TYPE_THESAURUS;

            // metadata
            metadata[M_THESAURUS_ID] = thesaurus.Id;
            int i = thesaurus.Id.LastIndexOf('@');
            metadata[M_THESAURUS_LANG] = i > -1
                ? thesaurus.Id[(i + 1)..] : "en";

            return thesaurus;
        }
    }
}
