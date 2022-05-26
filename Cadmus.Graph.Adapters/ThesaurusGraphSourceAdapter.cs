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
        public const string M_THESAURUS_ID = "thesaurus-id";
        public const string M_THESAURUS_LANG = "thesaurus-lang";

        /// <summary>
        /// Extracts the metadata from the <paramref name="source" /> object
        /// into <paramref name="metadata" />.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="metadata">The metadata.</param>
        protected override void ExtractMetadata(object source,
            IDictionary<string, object> metadata)
        {
            Thesaurus? thesaurus = source as Thesaurus;
            if (thesaurus == null) return;

            metadata[M_THESAURUS_ID] = thesaurus.Id;
            int i = thesaurus.Id.LastIndexOf('@');
            metadata[M_THESAURUS_LANG] = i > -1
                ? thesaurus.Id[(i + 1)..] : "en";
        }
    }
}
