namespace Cadmus.Graph.Ef;

public class EfUriLookup
{
    public int Id { get; set; }
    public string Uri { get; set; }

    public EfNode? Node { get; set; }

    public EfUriLookup()
    {
        Uri = "";
    }

    public override string ToString()
    {
        return $"#{Id} {Uri}";
    }
}
