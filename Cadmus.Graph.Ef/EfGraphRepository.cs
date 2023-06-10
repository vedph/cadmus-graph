using Cadmus.Core.Config;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Microsoft.Extensions.Caching.Memory;

namespace Cadmus.Graph.Ef;

public abstract class EfGraphRepository : IConfigurable<EfGraphRepositoryOptions>
{
    /// <summary>
    /// Gets the connection string.
    /// </summary>
    protected string? ConnectionString { get; set; }

    public IMemoryCache? Cache { get; set; }

    /// <summary>
    /// Configures this repository with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(EfGraphRepositoryOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        ConnectionString = options.ConnectionString;
    }

    /// <summary>
    /// Gets a new DB context configured for <see cref="ConnectionString"/>.
    /// </summary>
    /// <returns>DB context.</returns>
    protected abstract CadmusGraphDbContext GetContext();

    /// <summary>
    /// Adds the mapping.
    /// </summary>
    /// <param name="mapping">The mapping.</param>
    /// <returns>The mapping ID.</returns>
    /// <exception cref="ArgumentNullException">mapping</exception>
    public int AddMapping(NodeMapping mapping)
    {
        if (mapping is null) throw new ArgumentNullException(nameof(mapping));

        using CadmusGraphDbContext context = GetContext();

        // replace mapping if existing
        EfMapping? old = context.Mappings
            .FirstOrDefault(m => m.Id == mapping.Id);
        if (old != null) context.Remove(old);

        // add new mapping with its children
        context.Mappings.Add(new EfMapping(mapping));

        context.SaveChanges();
        return mapping.Id;
    }

    public void AddNamespace(string prefix, string uri)
    {
        throw new NotImplementedException();
    }

    public void AddNode(Node node, bool noUpdate = false)
    {
        throw new NotImplementedException();
    }

    public void AddProperty(Property property)
    {
        throw new NotImplementedException();
    }

    public void AddThesaurus(Thesaurus thesaurus, bool includeRoot, string? prefix = null)
    {
        throw new NotImplementedException();
    }

    public void AddTriple(Triple triple)
    {
        throw new NotImplementedException();
    }

    public int AddUri(string uri)
    {
        throw new NotImplementedException();
    }

    public string BuildUid(string unsuffixed, string sid)
    {
        throw new NotImplementedException();
    }

    public void DeleteGraphSet(string sourceId)
    {
        throw new NotImplementedException();
    }

    public void DeleteMapping(int id)
    {
        throw new NotImplementedException();
    }

    public void DeleteNamespaceByPrefix(string prefix)
    {
        throw new NotImplementedException();
    }

    public void DeleteNamespaceByUri(string uri)
    {
        throw new NotImplementedException();
    }

    public void DeleteNode(int id)
    {
        throw new NotImplementedException();
    }

    public void DeleteProperty(int id)
    {
        throw new NotImplementedException();
    }

    public void DeleteTriple(int id)
    {
        throw new NotImplementedException();
    }

    public string Export()
    {
        throw new NotImplementedException();
    }

    public IList<NodeMapping> FindMappings(RunNodeMappingFilter filter)
    {
        throw new NotImplementedException();
    }

    public GraphSet GetGraphSet(string sourceId)
    {
        throw new NotImplementedException();
    }

    public DataPage<UriTriple> GetLinkedLiterals(LinkedLiteralFilter filter)
    {
        throw new NotImplementedException();
    }

    public DataPage<UriNode> GetLinkedNodes(LinkedNodeFilter filter)
    {
        throw new NotImplementedException();
    }

    public NodeMapping? GetMapping(int id)
    {
        throw new NotImplementedException();
    }

    public DataPage<NodeMapping> GetMappings(NodeMappingFilter filter)
    {
        throw new NotImplementedException();
    }

    public DataPage<NamespaceEntry> GetNamespaces(NamespaceFilter filter)
    {
        throw new NotImplementedException();
    }

    public UriNode? GetNode(int id)
    {
        throw new NotImplementedException();
    }

    public UriNode? GetNodeByUri(string uri)
    {
        throw new NotImplementedException();
    }

    public DataPage<UriNode> GetNodes(NodeFilter filter)
    {
        throw new NotImplementedException();
    }

    public IList<UriNode?> GetNodes(IList<int> ids)
    {
        throw new NotImplementedException();
    }

    public DataPage<UriProperty> GetProperties(PropertyFilter filter)
    {
        throw new NotImplementedException();
    }

    public UriProperty? GetProperty(int id)
    {
        throw new NotImplementedException();
    }

    public UriProperty? GetPropertyByUri(string uri)
    {
        throw new NotImplementedException();
    }

    public UriTriple? GetTriple(int id)
    {
        throw new NotImplementedException();
    }

    public DataPage<TripleGroup> GetTripleGroups(TripleFilter filter, string sort = "Cu")
    {
        throw new NotImplementedException();
    }

    public DataPage<UriTriple> GetTriples(TripleFilter filter)
    {
        throw new NotImplementedException();
    }

    public int Import(string json)
    {
        throw new NotImplementedException();
    }

    public void ImportNodes(IEnumerable<UriNode> nodes)
    {
        throw new NotImplementedException();
    }

    public void ImportTriples(IEnumerable<UriTriple> triples)
    {
        throw new NotImplementedException();
    }

    public int LookupId(string uri)
    {
        throw new NotImplementedException();
    }

    public string? LookupNamespace(string prefix)
    {
        throw new NotImplementedException();
    }

    public string? LookupUri(int id)
    {
        throw new NotImplementedException();
    }

    public void UpdateGraph(GraphSet set)
    {
        throw new NotImplementedException();
    }

    public Task UpdateNodeClassesAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Options used to configure <see cref="EfGraphRepository"/>.
/// </summary>
public class EfGraphRepositoryOptions
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
}
