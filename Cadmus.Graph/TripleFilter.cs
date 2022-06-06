using Fusi.Tools.Data;
using System.Collections.Generic;

namespace Cadmus.Graph
{
    /// <summary>
    /// Filter for <see cref="Triple"/>.
    /// </summary>
    /// <seealso cref="PagingOptions" />
    public class TripleFilter : PagingOptions
    {
        /// <summary>
        /// Gets or sets the subject node identifier to match.
        /// </summary>
        public int SubjectId { get; set; }

        /// <summary>
        /// Gets or sets the predicate node identifier which must be matched.
        /// </summary>
        public HashSet<int>? PredicateIds { get; set; }

        /// <summary>
        /// Gets or sets the predicate node identifier which must NOT be matched.
        /// </summary>
        public HashSet<int>? NotPredicateIds { get; set; }

        /// <summary>
        /// Gets or sets the object identifier to match.
        /// </summary>
        public int ObjectId { get; set; }

        /// <summary>
        /// Gets or sets the object literal regular expression to match.
        /// </summary>
        public string? ObjectLiteral { get; set; }

        /// <summary>
        /// Gets or sets the type of the object literal. This corresponds to
        /// literal suffixes after <c>^^</c> in Turtle: e.g.
        /// <c>"12.3"^^xs:double</c>.
        /// </summary>
        public string? LiteralType { get; set; }

        /// <summary>
        /// Gets or sets the object literal language. This is meaningful only
        /// for string literals, and usually is an ISO639 code.
        /// </summary>
        public string? LiteralLanguage { get; set; }

        /// <summary>
        /// Gets or sets the minimum numeric value for a numeric object literal.
        /// </summary>
        public double? MinLiteralNumber { get; set; }

        /// <summary>
        /// Gets or sets the maximum numeric value for a numeric object literal.
        /// </summary>
        public double? MaxLiteralNumber { get; set; }

        /// <summary>
        /// Gets or sets the sid.
        /// </summary>
        public string? Sid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="Sid"/> represents
        /// the initial portion of the SID being searched, rather than the
        /// full SID.
        /// </summary>
        public bool IsSidPrefix { get; set; }

        /// <summary>
        /// Gets or sets the tag filter to match. If null, no tag filtering
        /// is applied; if empty, only triples with a null tag are matched;
        /// otherwise, the triples with the same tag must be matched.
        /// </summary>
        public string? Tag { get; set; }
    }
}
