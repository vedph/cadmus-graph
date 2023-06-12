using System.Text;

namespace Cadmus.Graph.Ef;

public class EfMapping
{
    /// <summary>
    /// Gets or sets a numeric identifier for this mapping. This is
    /// assigned when the mapping is archived in a database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the parent mapping's identifier. This is assigned
    /// when the mapping is archived in a database.
    /// </summary>
    public int? ParentId { get; set; }

    /// <summary>
    /// Gets or sets an optional ordinal value used to define the order
    /// of application of sibling mappings. Default is 0.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// Gets or sets the mapping's human friendly name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The type of the source object mapped by this mapping. This is
    /// meaningful for the root mapping only.
    /// </summary>
    public int SourceType { get; set; }

    /// <summary>
    /// The optional item's facet filter.
    /// </summary>
    public string? FacetFilter { get; set; }

    /// <summary>
    /// The optional item's group filter.
    /// </summary>
    public string? GroupFilter { get; set; }

    /// <summary>
    /// The optional item's flags filter.
    /// </summary>
    public int? FlagsFilter { get; set; }

    /// <summary>
    /// The optional item's title filter.
    /// </summary>
    public string? TitleFilter { get; set; }

    /// <summary>
    /// The optional part's type ID filter.
    /// </summary>
    public string? PartTypeFilter { get; set; }

    /// <summary>
    /// The optional part's role filter.
    /// </summary>
    public string? PartRoleFilter { get; set; }

    /// <summary>
    /// A short description of this mapping.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The source expression representing the data selected by this mapping.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// The template for building the SID for this mapping.
    /// </summary>
    public string? Sid { get; set; }

    public EfMapping? Parent { get; set; }

    public List<EfMapping>? Children { get; set; }

    public List<EfMappingMetaOutput>? MetaOutputs { get; set; }

    public List<EfMappingNodeOutput>? NodeOutputs { get; set; }

    public List<EfMappingTripleOutput>? TripleOutputs { get; set; }

    public EfMapping()
    {
        Name = "";
        Source = "";
    }

    public EfMapping(NodeMapping source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        // copy source into this object
        Id = source.Id;
        ParentId = source.ParentId;
        Ordinal = source.Ordinal;
        Name = source.Name ?? "";
        SourceType = source.SourceType;
        FacetFilter = source.FacetFilter;
        GroupFilter = source.GroupFilter;
        FlagsFilter = source.FlagsFilter;
        TitleFilter = source.TitleFilter;
        PartTypeFilter = source.PartTypeFilter;
        PartRoleFilter = source.PartRoleFilter;
        Description = source.Description;
        Source = source.Source;
        Sid = source.Sid;

        // metadata
        if (source.Output?.HasMetadata == true)
        {
            MetaOutputs = new List<EfMappingMetaOutput>();
            int n = 1;
            foreach (KeyValuePair<string, string> m in source.Output.Metadata)
            {
                MetaOutputs.Add(new EfMappingMetaOutput
                {
                    Mapping = this,
                    Ordinal = n++,
                    Name = m.Key,
                    Value = m.Value
                });
            }
        }

        // nodes
        if (source.Output?.HasNodes == true)
        {
            NodeOutputs = new List<EfMappingNodeOutput>();
            int n = 1;
            foreach (KeyValuePair<string, MappedNode> p in source.Output.Nodes)
            {
                NodeOutputs.Add(new EfMappingNodeOutput
                {
                    Mapping = this,
                    Ordinal = n++,
                    Name = p.Key,
                    Uid = p.Value.Uid ?? "",
                    Label = p.Value.Label ?? "",
                    Tag = p.Value.Tag ?? "",
                });
            }
        }

        // triples
        if (source.Output?.HasTriples == true)
        {
            TripleOutputs = new List<EfMappingTripleOutput>();
            int n = 1;
            foreach (MappedTriple t in source.Output.Triples)
            {
                TripleOutputs.Add(new EfMappingTripleOutput
                {
                    Mapping = this,
                    Ordinal = n++,
                    S = t.S!,
                    P = t.P!,
                    O = t.O,
                    OL = t.OL,
                });
            }
        }

        // children
        if (source.HasChildren)
        {
            Children = new List<EfMapping>();
            foreach (NodeMapping child in source.Children)
            {
                Children.Add(new EfMapping(child));
            }
        }
    }

    public NodeMapping ToNodeMapping()
    {
        return new NodeMapping
        {
            Id = Id,
            ParentId = ParentId == null? 0 : ParentId.Value,
            Ordinal = Ordinal,
            Name = Name,
            SourceType = SourceType,
            FacetFilter = FacetFilter,
            GroupFilter = GroupFilter,
            FlagsFilter = FlagsFilter,
            TitleFilter = TitleFilter,
            PartTypeFilter = PartTypeFilter,
            PartRoleFilter = PartRoleFilter,
            Description = Description,
            Source = Source,
            Sid = Sid,
            Output = new NodeMappingOutput
            {
                Metadata = MetaOutputs?.ToDictionary(m => m.Name, m => m.Value)
                    ?? new Dictionary<string, string>(),
                Nodes = NodeOutputs?.ToDictionary(n => n.Name, n =>
                    new MappedNode
                    {
                        Uid = n.Uid,
                        Label = n.Label,
                        Tag = n.Tag
                    })
                    ?? new Dictionary<string, MappedNode>(),
                Triples = TripleOutputs?.Select(
                    t => new MappedTriple
                    {
                        S = t.S,
                        P = t.P,
                        O = t.O,
                        OL = t.OL
                    }).ToList()
                    ?? new List<MappedTriple>()
            },
            Children = Children?.Select(c => c.ToNodeMapping()).ToList()
                ?? new List<NodeMapping>()
        };
    }

    private static bool AppendFilter(string id, bool filter, StringBuilder sb,
        string value)
    {
        if (!filter)
        {
            sb.Append('[');
            filter = true;
        }
        else sb.Append(", ");

        sb.Append(id).Append('=');
        sb.Append(value);
        return filter;
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append('#').Append(Id)
          .Append(' ').Append(Name)
          .Append(" @").Append(SourceType);

        bool filter = false;
        if (!string.IsNullOrEmpty(FacetFilter))
            filter = AppendFilter("facet", filter, sb, FacetFilter);
        if (!string.IsNullOrEmpty(GroupFilter))
            filter = AppendFilter("group", filter, sb, GroupFilter);
        if (FlagsFilter.HasValue)
        {
            filter = AppendFilter("flags", filter, sb,
                FlagsFilter.Value.ToString("X4"));
        }
        if (!string.IsNullOrEmpty(TitleFilter))
            filter = AppendFilter("title", filter, sb, TitleFilter);
        if (!string.IsNullOrEmpty(PartTypeFilter))
            filter = AppendFilter("type", filter, sb, PartTypeFilter);
        if (!string.IsNullOrEmpty(PartRoleFilter))
            AppendFilter("role", filter, sb, PartRoleFilter);
        if (filter) sb.Append(']');

        sb.Append(": ").Append(Source);

        sb.Append(" -> ");
        if (MetaOutputs?.Count > 0) sb.Append("M=").Append(MetaOutputs.Count);
        if (NodeOutputs?.Count > 0) sb.Append("N=").Append(NodeOutputs.Count);
        if (TripleOutputs?.Count > 0) sb.Append("T=").Append(TripleOutputs.Count);

        return sb.ToString();
    }
}
