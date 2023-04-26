﻿using Cadmus.Core;
using Cadmus.Graph.Adapters;
using System;
using System.Collections.Generic;

namespace Cadmus.Graph;

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

    /// <summary>
    /// Gets the metadata used by the updater. You can add more metadata
    /// manually, or set the <see cref="MetadataSupplier"/> property so that
    /// it will be invoked before each update. All metadata are automatically
    /// reset after updating.
    /// </summary>
    public IDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets or sets the optional metadata supplier to be used by this updater.
    /// </summary>
    public MetadataSupplier? MetadataSupplier { get; set; }

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
        Metadata = new Dictionary<string, string>();
    }

    private void Update(object data, RunNodeMappingFilter filter)
    {
        GraphSet set = new();
        foreach (NodeMapping mapping in _repository.FindMappings(filter))
            _mapper.Map(data, mapping, set);

        _repository.UpdateGraph(set);
        Metadata.Clear();
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
        MetadataSupplier?.Supply(_mapper.Context, Metadata);

        var df = _itemAdapter.Adapt(_mapper.Context, Metadata);
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
        MetadataSupplier?.Supply(_mapper.Context, Metadata);

        var df = _partAdapter.Adapt(_mapper.Context, Metadata);
        if (df.Item1 != null) Update(df.Item1, df.Item2);
    }
}
