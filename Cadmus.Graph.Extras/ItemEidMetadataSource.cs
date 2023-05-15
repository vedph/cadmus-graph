﻿using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Cadmus.Graph.Adapters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Graph.Extras;

/// <summary>
/// Graph updater metadata source for item EID. This looks for a
/// <see cref="MetadataPart"/> with no role in the source item, and if found
/// picks its EID metadata value and adds it to the metadata of the target
/// with key = <c>item-eid</c>.
/// </summary>
/// <seealso cref="IMetadataSource" />
public sealed class ItemEidMetadataSource : IMetadataSource
{
    /// <summary>
    /// Supplies metadata for the specified source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="metadata">The metadata.</param>
    /// <param name="repository">The repository.</param>
    /// <param name="context">The optional context.</param>
    /// <exception cref="ArgumentNullException">source or metadata or repository
    /// </exception>
    public void Supply(GraphSource source, IDictionary<string, object> metadata,
        ICadmusRepository? repository, object? context = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (metadata is null) throw new ArgumentNullException(nameof(metadata));
        if (repository is null) throw new ArgumentNullException(nameof(repository));

        // find metadata part with no role
        MetadataPart? part = source.Part as MetadataPart;
        if (part == null)
        {
            IList<IPart> parts = repository.GetItemParts(
                new[] { source.Item.Id }, "it.vedph.metadata");
            part = (MetadataPart?)parts.FirstOrDefault(p => p is MetadataPart);
        }
        // if found, add its eid to metadata
        if (part != null)
        {
            string? eid = part.Metadata.Find(m => m.Name == "eid")?.Value;
            if (!string.IsNullOrEmpty(eid))
            {
                metadata["item-eid"] = eid;
            }
        }
    }
}