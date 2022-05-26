using Cadmus.Core;
using System.Collections.Generic;

namespace Cadmus.Graph.Adapters
{
    /// <summary>
    /// Graph source adapter for <see cref="IItem"/>'s.
    /// </summary>
    /// <seealso cref="JsonGraphSourceAdapter" />
    /// <seealso cref="IGraphSourceAdapter" />
    public sealed class ItemGraphSourceAdapter : JsonGraphSourceAdapter,
        IGraphSourceAdapter
    {
        public const string M_ITEM_ID = "item-id";
        public const string M_ITEM_TITLE = "item-title";
        public const string M_ITEM_FACET = "item-facet";
        public const string M_ITEM_GROUP = "item-group";

        /// <summary>
        /// Extracts the metadata from the <paramref name="source"/> object
        /// into <paramref name="metadata"/>.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="metadata">The metadata.</param>
        protected override void ExtractMetadata(object source,
            IDictionary<string, object> metadata)
        {
            IItem? item = source as IItem;
            if (item == null) return;

            metadata[M_ITEM_ID] = item.Id;
            metadata[M_ITEM_TITLE] = item.Title;
            metadata[M_ITEM_FACET] = item.FacetId;

            if (!string.IsNullOrEmpty(item.GroupId))
            {
                metadata[M_ITEM_GROUP] = item.GroupId;
                if (item.GroupId.IndexOf('/') > -1)
                {
                    int n = 0;
                    foreach (string g in item.GroupId.Split('/'))
                        metadata[$"{M_ITEM_GROUP}@{++n}"] = g;
                }
            }
        }
    }
}
