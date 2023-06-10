namespace Cadmus.Graph.Ef;

public class EfUidLookup
{
    public int Id { get; set; }
    public string Sid { get; set; }
    public string Unsuffixed { get; set; }
    public bool HasSuffix { get; set; }

    public EfUidLookup()
    {
        Sid = "";
        Unsuffixed = "";
    }

    public override string ToString()
    {
        return $"#{Id} {Unsuffixed}{(HasSuffix ? "*" : "")}";
    }
}
