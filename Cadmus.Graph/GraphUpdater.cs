using Cadmus.Core;
using Cadmus.Graph.Adapters;
using System;
using System.Collections.Generic;

namespace Cadmus.Graph
{
    /// <summary>
    /// Graph updater. This is a top level helper class, used to update the
    /// graph whenever an item or part gets saved.
    /// </summary>
    public class GraphUpdater
    {
        private readonly IGraphRepository _repository;
        private readonly INodeMapper _mapper;
        private readonly ItemGraphSourceAdapter _itemAdapter;
        private readonly PartGraphSourceAdapter _partAdapter;
        private readonly IDictionary<string, string> _metadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphUpdater"/> class.
        /// </summary>
        /// <param name="repository">The repository.</param>
        /// <exception cref="ArgumentNullException">repository</exception>
        public GraphUpdater(IGraphRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mapper = new JsonNodeMapper();
            _itemAdapter = new ItemGraphSourceAdapter();
            _partAdapter = new PartGraphSourceAdapter();
            _metadata = new Dictionary<string, string>();
        }

        private void Update(object data, RunNodeMappingFilter filter)
        {
            _metadata.Clear();
            GraphSet set = new();
            foreach (NodeMapping mapping in _repository.FindMappings(filter))
                _mapper.Map(data, mapping, set);

            _repository.UpdateGraph(set);
        }

        /// <summary>
        /// Updates the graph from the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="ArgumentNullException">item</exception>
        public void Update(IItem item)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));

            _mapper.Context = new GraphSource(item);
            var df = _itemAdapter.Adapt(_mapper.Context, _metadata);
            if (df.Item1 != null) Update(df.Item1, df.Item2);
        }

        /// <summary>
        /// Updates the graph from the specified item's part.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="part">The part.</param>
        /// <exception cref="ArgumentNullException">item or part</exception>
        public void Update(IItem item, IPart part)
        {
            if (item is null) throw new ArgumentNullException(nameof(item));
            if (part is null) throw new ArgumentNullException(nameof(part));

            _mapper.Context = new GraphSource(item, part);
            var df = _partAdapter.Adapt(_mapper.Context, _metadata);
            if (df.Item1 != null) Update(df.Item1, df.Item2);
        }
    }
}
