namespace Cadmus.Graph.Ef;

public class EfNamespaceLookup
{
    public string Id { get; set; }
    public string Uri { get; set; }

    public EfNamespaceLookup()
    {
        Id = "";
        Uri = "";
    }

    public override string ToString()
    {
        return $"{Id}={Uri}";
    }
}
