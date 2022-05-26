using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Cadmus.Graph.Adapters
{
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
            _options = new();
        }

        /// <summary>
        /// Extracts the metadata from the <paramref name="source"/> object
        /// into <paramref name="metadata"/>.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="metadata">The metadata.</param>
        protected abstract void ExtractMetadata(object source,
            IDictionary<string, object> metadata);

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
        public object? Adapt(object source, IDictionary<string, object> metadata)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (metadata is null)
                throw new ArgumentNullException(nameof(metadata));

            ExtractMetadata(source, metadata);

            return JsonSerializer.Serialize(source, _options);
        }
    }
}
