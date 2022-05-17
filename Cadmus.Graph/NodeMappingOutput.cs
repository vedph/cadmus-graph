using System.Collections.Generic;

namespace Cadmus.Graph
{
    /// <summary>
    /// The output definition of a <see cref="NodeMapping"/>.
    /// </summary>
    public class NodeMappingOutput
    {
        private IDictionary<string, MappedNode>? _nodes;
        private IList<MappedTriple>? _triples;

        /// <summary>
        /// The nodes to emit, keyed under some mapping-scoped ID. This ID can
        /// then be used to recall the node's UID or its other properties.
        /// </summary>
        public IDictionary<string, MappedNode> Nodes
        {
            get
            {
                return _nodes ??= new Dictionary<string, MappedNode>();
            }
            set { _nodes = value; }
        }

        /// <summary>
        /// True if this output has nodes.
        /// </summary>
        public bool HasNodes { get => _nodes?.Count > 0; }

        /// <summary>
        /// The triples to emit.
        /// </summary>
        public IList<MappedTriple> Triples
        {
            get
            {
                return _triples ??= new List<MappedTriple>();
            }
            set { _triples = value; }
        }

        /// <summary>
        /// True if this output has triples.
        /// </summary>
        public bool HasTriples { get => _triples?.Count > 0; }

        /// <summary>
        /// Return a string representing this object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            return $"N={_nodes?.Count ?? 0}, T={_triples?.Count ?? 0}";
        }
    }
}
