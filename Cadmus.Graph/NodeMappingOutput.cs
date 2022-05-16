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

        public IDictionary<string, MappedNode> Nodes
        {
            get
            {
                return _nodes ??= new Dictionary<string, MappedNode>();
            }
            set { _nodes = value; }
        }

        public bool HasNodes { get => _nodes?.Count > 0; }

        public IList<MappedTriple> Triples
        {
            get
            {
                return _triples ??= new List<MappedTriple>();
            }
            set { _triples = value; }
        }

        public bool HasTriples { get => _triples?.Count > 0; }
    }
}
