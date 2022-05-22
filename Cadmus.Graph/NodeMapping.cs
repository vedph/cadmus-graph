using System.Collections.Generic;
using System.Text;

namespace Cadmus.Graph
{
    /// <summary>
    /// Node mapping.
    /// </summary>
    public sealed class NodeMapping
    {
        private IList<NodeMapping>? _children;

        /// <summary>
        /// The mapping's ID.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// The type of the source object mapped by this mapping, like item,
        /// part, or thesaurus.
        /// </summary>
        public string? SourceType { get; set; }

        /// <summary>
        /// The optional item's facet filter.
        /// </summary>
        public string? FacetFilter { get; set; }

        /// <summary>
        /// The optional item's group filter.
        /// </summary>
        public string? GroupFilter { get; set; }

        /// <summary>
        /// The optional item's flags filter.
        /// </summary>
        public int? FlagsFilter { get; set; }

        /// <summary>
        /// The optional part's type ID filter.
        /// </summary>
        public string? PartTypeFilter { get; set; }

        /// <summary>
        /// The optional part's role filter.
        /// </summary>
        public string? PartRoleFilter { get; set; }

        /// <summary>
        /// A short description of this mapping.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The source expression representing the data selected by this mapping.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// The template for building the SID for this mapping. This property
        /// is meaningful only for non-children mappings.
        /// </summary>
        public string? Sid { get; set; }

        /// <summary>
        /// The output of this mapping.
        /// </summary>
        public NodeMappingOutput? Output { get; set; }

        /// <summary>
        /// The optional children mappings of this mapping.
        /// </summary>
        public IList<NodeMapping>? Children
        {
            get { return _children ??= new List<NodeMapping>(); }
            set { _children = value; }
        }

        /// <summary>
        /// True if this mapping has children.
        /// </summary>
        public bool HasChildren => _children?.Count > 0;

        private static bool AppendFilter(string id, bool filter, StringBuilder sb,
            string value)
        {
            if (!filter)
            {
                sb.Append('[');
                filter = true;
            }
            else sb.Append(", ");

            sb.Append(id).Append('=');
            sb.Append(value);
            return filter;
        }

        /// <summary>
        /// Converts to string.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            StringBuilder sb = new();

            sb.Append('@').Append(SourceType);
            if (!string.IsNullOrEmpty(Id)) sb.Append(" #").Append(Id);

            bool filter = false;
            if (!string.IsNullOrEmpty(FacetFilter))
                filter = AppendFilter("facet", filter, sb, FacetFilter);
            if (!string.IsNullOrEmpty(GroupFilter))
                filter = AppendFilter("group", filter, sb, GroupFilter);
            if (FlagsFilter.HasValue)
            {
                filter = AppendFilter("flags", filter, sb,
                    FlagsFilter.Value.ToString("X4"));
            }
            if (!string.IsNullOrEmpty(PartTypeFilter))
                filter = AppendFilter("type", filter, sb, PartTypeFilter);
            if (!string.IsNullOrEmpty(PartRoleFilter))
                AppendFilter("role", filter, sb, PartRoleFilter);

            sb.Append(": ").Append(Source);
            if (Output != null) sb.Append(" -> ").Append(Output);

            return sb.ToString();
        }
    }
}