namespace Cadmus.Graph.Ef;

public class EfNode : Node
{
    public EfUriLookup? UriLookup { get; set; }
    public EfProperty? Property { get; set; }
    public List<EfNodeClass>? Classes { get; set; }

    public EfNode()
    {
    }

    public EfNode(Node node)
    {
        Id = node.Id;
        IsClass = node.IsClass;
        Tag = node.Tag;
        Label = node.Label;
        SourceType = node.SourceType;
        Sid = node.Sid;
    }
}
