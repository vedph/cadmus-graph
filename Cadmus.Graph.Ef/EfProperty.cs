using System.Text;

namespace Cadmus.Graph.Ef;

public class EfProperty
{
    public int Id { get; set; }
    public string? DataType { get; set; }
    public string? LitEditor { get; set; }
    public string? Description { get; set; }

    public EfNode? Node { get; set; }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append('#').Append(Id);
        if (DataType != null) sb.Append(' ').Append(DataType);
        if (LitEditor != null) sb.Append(' ').Append(LitEditor);
        return sb.ToString();
    }
}
