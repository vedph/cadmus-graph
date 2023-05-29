namespace Cadmus.Graph.MySql.Test;

internal static class MetadataSupplierExtensions
{
    /// <summary>
    /// Adds the <c>item-eid</c> metadatum supplier.
    /// </summary>
    /// <param name="supplier">The supplier to extend.</param>
    public static MetadataSupplier AddMockItemEid(this MetadataSupplier supplier,
        string pid, string eid)
    {
        supplier.AddMetadataSource(new MockItemEidMetadataSource(pid, eid));
        return supplier;
    }
}
