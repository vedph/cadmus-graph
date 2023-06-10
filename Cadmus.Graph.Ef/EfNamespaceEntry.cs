namespace Cadmus.Graph.Ef;

public class EfNamespaceEntry
{
    public string Id { get; set; }
    public string Uri { get; set; }

    public EfNamespaceEntry()
    {
        Id = "";
        Uri = "";
    }

    public EfNamespaceEntry(NamespaceEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        Id = entry.Prefix!;
        Uri = entry.Uri!;
    }

    public NamespaceEntry ToNamespaceEntry()
    {
        return new NamespaceEntry
        {
            Prefix = Id,
            Uri = Uri
        };
    }

    public override string ToString()
    {
        return $"{Id}={Uri}";
    }
}
