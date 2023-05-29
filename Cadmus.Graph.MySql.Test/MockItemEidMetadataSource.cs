using Cadmus.Core.Storage;
using Cadmus.Graph.Adapters;
using System.Collections.Generic;

namespace Cadmus.Graph.MySql.Test;

internal sealed class MockItemEidMetadataSource : IMetadataSource
{
    private readonly string? _eid;

    public MockItemEidMetadataSource(string? eid)
    {
        _eid = eid;
    }

    public void Supply(GraphSource source, IDictionary<string, object> metadata,
        ICadmusRepository? repository, object? context = null)
    {
        if (_eid != null) metadata["item-eid"] = _eid;
    }
}
