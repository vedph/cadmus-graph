using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Graph
{
    /// <summary>
    /// A node defined in a <see cref="NodeMapping"/>.
    /// </summary>
    public class MappedNode
    {
        /// <summary>
        /// The node's UID template.
        /// </summary>
        public string? Uid { get; set; }

        /// <summary>
        /// The optional node's label template.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// The optional node's tag template.
        /// </summary>
        public string? Tag { get; set; }

        /// <summary>
        /// Parse the text representing a <see cref="MappedNode"/>. This consists
        /// of a URI, optionally followed by space plus a label, and/or a tag
        /// between square brackets.
        /// </summary>
        /// <param name="text">Text or null.</param>
        /// <returns>Node or null.</returns>
        public static MappedNode? Parse(string? text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            Match m = Regex.Match(text,
                @"^(?<u>[^ ]+)(?:\s+(?<l>[^[][^\s]*))?(?:\s+\[(?<t>[^]]+)\])?");
            return m.Success
                ? new MappedNode
                {
                    Uid = m.Groups["u"].Value,
                    Label = m.Groups["l"].Value.Length == 0? null : m.Groups["l"].Value,
                    Tag = m.Groups["t"].Value.Length == 0? null : m.Groups["t"].Value
                }
                : null;
        }

        /// <summary>
        /// Convert this node into a parsable representation.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(Uid);

            if (Label != null) sb.Append(' ').Append(Label);
            if (Tag != null) sb.Append(" [").Append(Tag).Append(']');

            return sb.ToString();
        }
    }
}
