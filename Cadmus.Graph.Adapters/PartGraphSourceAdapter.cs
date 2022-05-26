using Cadmus.Core;
using System.Collections.Generic;

namespace Cadmus.Graph.Adapters
{
    /// <summary>
    /// Graph source adapter for <see cref="IPart"/>.
    /// </summary>
    /// <seealso cref="JsonGraphSourceAdapter" />
    /// <seealso cref="IGraphSourceAdapter" />
    public sealed class PartGraphSourceAdapter : JsonGraphSourceAdapter,
        IGraphSourceAdapter
    {
        public const string M_PART_ID = "part-id";
        public const string M_PART_TYPE_ID = "part-type-id";
        public const string M_PART_ROLE_ID = "part-role-id";

        /// <summary>
        /// Extracts the metadata from the <paramref name="source" /> object
        /// into <paramref name="metadata" />.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="metadata">The metadata.</param>
        protected override void ExtractMetadata(object source,
            IDictionary<string, object> metadata)
        {
            IPart? part = source as IPart;
            if (part == null) return;

            metadata[M_PART_ID] = part.Id;
            metadata[ItemGraphSourceAdapter.M_ITEM_ID] = part.ItemId;
            metadata[M_PART_TYPE_ID] = part.TypeId;
            metadata[M_PART_ROLE_ID] = part.RoleId;
        }
    }
}
