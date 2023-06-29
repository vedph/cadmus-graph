using Cadmus.Core.Storage;
using Cadmus.Graph.Adapters;
using System.Collections.Generic;

namespace Cadmus.Graph.Sql.Test;

internal sealed class MockItemEidMetadataSource : IMetadataSource
{
    private readonly string? _pid;
    private readonly string? _eid;

    public MockItemEidMetadataSource(string? pid, string? eid)
    {
        _pid = pid;
        _eid = eid;
    }

    public void Supply(GraphSource source, IDictionary<string, object> metadata,
        ICadmusRepository? repository, object? context = null)
    {
        if (_pid != null) metadata["metadata-pid"] = _pid;
        if (_eid != null) metadata["item-eid"] = _eid;
    }
}
