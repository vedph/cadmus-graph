using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Cadmus.Graph.Adapters;

/// <summary>
/// Base class for JSON-based <see cref="IGraphSourceAdapter"/> implementations.
/// </summary>
/// <seealso cref="IGraphSourceAdapter" />
public abstract class JsonGraphSourceAdapter
{
    protected readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonGraphSourceAdapter"/>
    /// class.
    /// </summary>
    protected JsonGraphSourceAdapter()
    {
        _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Adapt the source to the mapping process, eventually also setting
    /// <paramref name="filter"/> and <paramref name="metadata"/>
    /// accordingly.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="filter">The filter to set.</param>
    /// <param name="metadata">The metadata to set.</param>
    /// <returns>Adapted object or null.</returns>
    protected abstract object? Adapt(GraphSource source,
        RunNodeMappingFilter filter, IDictionary<string, string> metadata);

    /// <summary>
    /// Adapts the specified source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="metadata">The target dictionary for metadata generated
    /// by the adapter.</param>
    /// <returns>
    /// The adaptation result, or null.
    /// </returns>
    /// <exception cref="ArgumentNullException">source or metadata</exception>
    public Tuple<object?, RunNodeMappingFilter> Adapt(
        GraphSource source, IDictionary<string, string> metadata)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (metadata is null) throw new ArgumentNullException(nameof(metadata));

        RunNodeMappingFilter filter = new();
        object? result = Adapt(source, filter, metadata);

        return Tuple.Create(
            (object?)JsonSerializer.Serialize(result, _options),
            filter);
    }
}
