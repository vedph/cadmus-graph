using Cadmus.Core.Config;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System;
using System.Text;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Cadmus.Graph.Ef;

/// <summary>
/// Base class for Entity-Framework-based graph repositories.
/// </summary>
public abstract class EfGraphRepository : IConfigurable<EfGraphRepositoryOptions>
{
    /// <summary>
    /// Gets the connection string.
    /// </summary>
    protected string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the optional cache to use for mappings. This improves
    /// performance when fetching mappings from the database. All the
    /// mappings are stored with key <c>nm-</c> + the mapping's ID.
    /// Avoid using this if editing mappings.
    /// </summary>
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

    #region Namespace Lookup
    /// <summary>
    /// Gets the specified page of namespaces with their prefixes.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<NamespaceEntry> GetNamespaces(NamespaceFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();

        IQueryable<EfNamespaceEntry> lookups =
            context.NamespaceEntries.AsNoTracking();

        if (!string.IsNullOrEmpty(filter.Prefix))
            lookups = lookups.Where(l => l.Id.Contains(filter.Prefix));
        if (!string.IsNullOrEmpty(filter.Uri))
            lookups = lookups.Where(l => l.Uri.Contains(filter.Uri));

        // get total and ret if zero
        int total = lookups.Count();
        if (total == 0)
        {
            return new DataPage<NamespaceEntry>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<NamespaceEntry>());
        }

        var results = lookups.OrderBy(l => l.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(l => l.ToNamespaceEntry())
            .ToList();

        return new DataPage<NamespaceEntry>(filter.PageNumber,
            filter.PageSize, total, results);
    }

    /// <summary>
    /// Looks up the namespace from its prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The namespace, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">prefix</exception>
    public string? LookupNamespace(string prefix)
    {
        if (prefix is null) throw new ArgumentNullException(nameof(prefix));

        using CadmusGraphDbContext context = GetContext();
        EfNamespaceEntry? lookup = context.NamespaceEntries
            .FirstOrDefault(l => l.Id == prefix);
        return lookup?.Uri;
    }

    /// <summary>
    /// Adds the specified namespace prefix. If it already exists, nothing
    /// is done.
    /// </summary>
    /// <param name="prefix">The namespace prefix.</param>
    /// <param name="uri">The namespace URI corresponding to
    /// <paramref name="prefix" />.</param>
    /// <exception cref="ArgumentNullException">prefix or uri</exception>
    public void AddNamespace(string prefix, string uri)
    {
        if (prefix == null) throw new ArgumentNullException(nameof(prefix));
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        using CadmusGraphDbContext context = GetContext();
        EfNamespaceEntry? old = context.NamespaceEntries
            .FirstOrDefault(l => l.Id == prefix && l.Uri == uri);
        if (old == null)
        {
            context.NamespaceEntries.Add(new EfNamespaceEntry
            {
                Id = prefix,
                Uri = uri
            });
            context.SaveChanges();
        }
    }

    /// <summary>
    /// Deletes a namespace by prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <exception cref="ArgumentNullException">prefix</exception>
    public void DeleteNamespaceByPrefix(string prefix)
    {
        if (prefix == null) throw new ArgumentNullException(nameof(prefix));

        using CadmusGraphDbContext context = GetContext();
        EfNamespaceEntry? entry = context.NamespaceEntries
            .FirstOrDefault(l => l.Id == prefix);
        if (entry != null)
        {
            context.NamespaceEntries.Remove(entry);
            context.SaveChanges();
        }
    }

    /// <summary>
    /// Deletes the specified namespace with all its prefixes.
    /// </summary>
    /// <param name="uri">The namespace URI.</param>
    public void DeleteNamespaceByUri(string uri)
    {
        if (uri is null) throw new ArgumentNullException(nameof(uri));

        using CadmusGraphDbContext context = GetContext();
        IEnumerable<EfNamespaceEntry> entries = context.NamespaceEntries
            .Where(l => l.Uri == uri);
        if (entries.Any())
        {
            context.NamespaceEntries.RemoveRange(entries);
            context.SaveChanges();
        }
    }
    #endregion

    #region UID Lookup
    /// <summary>
    /// Adds the specified UID, eventually completing it with a suffix.
    /// </summary>
    /// <param name="uid">The UID as calculated from its source, without any
    /// suffix.</param>
    /// <param name="sid">The SID identifying the source for this UID.</param>
    /// <returns>The UID, eventually suffixed.</returns>
    public string BuildUid(string unsuffixed, string sid)
    {
        if (unsuffixed == null) throw new ArgumentNullException(nameof(unsuffixed));
        if (sid == null) throw new ArgumentNullException(nameof(sid));

        using CadmusGraphDbContext context = GetContext();

        // check if any unsuffixed UID is already in use
        if (!context.UidEntries.Any(l => l.Unsuffixed == unsuffixed))
        {
            // no: just insert the unsuffixed UID
            context.UidEntries.Add(new EfUidEntry
            {
                Sid = sid,
                Unsuffixed = unsuffixed,
                HasSuffix = false
            });
            return unsuffixed;
        }

        // yes: check if a record with the same unsuffixed & SID exists.
        // If so, reuse it; otherwise, add a new suffixed UID
        EfUidEntry? entry = context.UidEntries
            .FirstOrDefault(l => l.Unsuffixed == unsuffixed && l.Sid == sid);

        // found: reuse it, nothing gets inserted
        if (entry != null)
        {
            int oldId = entry.Id;
            return entry.HasSuffix ? unsuffixed + "#" + oldId : unsuffixed;
        }
        // not found: add a new suffix
        entry = new EfUidEntry
        {
            Sid = sid,
            Unsuffixed = unsuffixed,
            HasSuffix = true
        };
        context.UidEntries.Add(entry);
        context.SaveChanges();
        return unsuffixed + "#" + entry.Id;
    }
    #endregion

    #region URI Lookup
    private static int AddUri(string uri, CadmusGraphDbContext context)
    {
        // if the URI already exists, just return its ID
        EfUriEntry? entry = context.UriEntries.AsNoTracking()
            .FirstOrDefault(l => l.Uri == uri);
        if (entry != null) return entry.Id;

        // otherwise, add it
        entry = new EfUriEntry
        {
            Uri = uri
        };
        context.UriEntries.Add(entry);
        context.SaveChanges();
        return entry.Id;
    }

    private static int AddUri(string uri, CadmusGraphDbContext context,
        out bool newUri)
    {
        // if the URI already exists, just return its ID
        EfUriEntry? entry = context.UriEntries.AsNoTracking()
            .FirstOrDefault(l => l.Uri == uri);
        if (entry != null)
        {
            newUri = false;
            return entry.Id;
        }

        // otherwise, add it
        entry = new EfUriEntry
        {
            Uri = uri
        };
        context.UriEntries.Add(entry);
        context.SaveChanges();
        newUri = true;
        return entry.Id;
    }

    /// <summary>
    /// Adds the specified URI in the mapped URIs set.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>ID assigned to the URI.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public int AddUri(string uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        using CadmusGraphDbContext context = GetContext();
        return AddUri(uri, context);
    }

    /// <summary>
    /// Lookups the URI from its numeric ID.
    /// </summary>
    /// <param name="id">The numeric ID for the URI.</param>
    /// <returns>The URI, or null if not found.</returns>
    public string? LookupUri(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfUriEntry? entry = context.UriEntries.AsNoTracking()
            .FirstOrDefault(l => l.Id == id);
        return entry?.Uri;
    }

    /// <summary>
    /// Lookups the numeric ID from its URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The ID, or 0 if not found.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public int LookupId(string uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        using CadmusGraphDbContext context = GetContext();
        EfUriEntry? entry = context.UriEntries.AsNoTracking()
            .FirstOrDefault(l => l.Uri == uri);
        return entry?.Id ?? 0;
    }
    #endregion

    #region Node
    private static IQueryable<EfNode> ApplyNodeFilterBase(
        IQueryable<EfNode> nodes, NodeFilterBase filter)
    {
        // uid
        if (!string.IsNullOrEmpty(filter.Uid))
            nodes = nodes.Where(n => n.UriEntry!.Uri.Contains(filter.Uid));

        // class
        if (filter.IsClass != null)
            nodes = nodes.Where(n => n.IsClass == filter.IsClass.Value);

        // tag
        if (filter.Tag != null)
        {
            if (filter.Tag.Length == 0) nodes = nodes.Where(n => n.Tag == null);
            else nodes = nodes.Where(n => n.Tag == filter.Tag);
        }

        // label
        if (!string.IsNullOrEmpty(filter.Label))
        {
            nodes = nodes.Where(
                n => n.Label != null && n.Label.Contains(filter.Label));
        }

        // source type
        if (filter.SourceType != null)
            nodes = nodes.Where(n => n.SourceType == filter.SourceType.Value);

        // sid
        if (!string.IsNullOrEmpty(filter.Sid))
        {
            if (filter.IsSidPrefix)
                nodes = nodes.Where(n => n.Sid != null && n.Sid.StartsWith(filter.Sid));
            else
                nodes = nodes.Where(n => n.Sid == filter.Sid);
        }

        // class IDs
        if (filter.ClassIds?.Count > 0)
        {
            // nodes with any of the specified class IDs
            nodes = nodes.Where(n =>
                n.Classes != null && n.Classes.Count > 0 &&
                n.Classes!.Any(c => filter.ClassIds.Contains(c.Id)));
        }

        return nodes;
    }

    /// <summary>
    /// Gets the requested page of nodes.
    /// </summary>
    /// <param name="filter">The nodes filter.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriNode> GetNodes(NodeFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfNode> nodes = filter.LinkedNodeId > 0
            ? filter.LinkedNodeId switch
            {
                'S' => context.Nodes
                    .Include(n => n.UriEntry)
                    .Include(n => n.ObjectTriples)
                    .AsNoTracking(),
                'O' => context.Nodes
                    .Include(n => n.UriEntry)
                    .Include(n => n.SubjectTriples)
                    .AsNoTracking(),
                _ => context.Nodes
                    .Include(n => n.UriEntry)
                    .Include(n => n.SubjectTriples)
                    .Include(n => n.ObjectTriples)
                    .AsNoTracking()
            }
        : context.Nodes
                .Include(n => n.UriEntry)
                .AsNoTracking();

        nodes = ApplyNodeFilterBase(nodes, filter);

        // additional filter: linked node ID
        if (filter.LinkedNodeId > 0)
        {
            nodes = char.ToUpperInvariant(filter.LinkedNodeRole) switch
            {
                // filter nodes which once joined with triples have any triple
                // with the specified linked node ID as object and
                // the filter node ID as subject
                'S' => nodes.Where(n =>
                    n.ObjectTriples != null && n.ObjectTriples.Count > 0 &&
                    n.ObjectTriples.Any(t => t.ObjectId == n.Id &&
                                        t.SubjectId == filter.LinkedNodeId)),

                // filter nodes which once joined with triples have any triple
                // with the specified linked node ID as subject and
                // the filter node ID as object
                'O' => nodes.Where(n =>
                    n.SubjectTriples != null && n.SubjectTriples.Count > 0 &&
                    n.SubjectTriples.Any(t => t.SubjectId == n.Id &&
                                         t.ObjectId == filter.LinkedNodeId)),

                // filter nodes as either S or O
                _ => nodes.Where(n =>
                    (n.ObjectTriples != null && n.ObjectTriples.Count > 0 &&
                     n.ObjectTriples.Any(t => t.ObjectId == n.Id &&
                                         t.SubjectId == filter.LinkedNodeId)) ||
                    (n.SubjectTriples != null && n.SubjectTriples.Count > 0 &&
                     n.SubjectTriples.Any(t => t.SubjectId == n.Id &&
                                          t.ObjectId == filter.LinkedNodeId))),
            };
        }

        int total = nodes.Count();
        if (total == 0)
        {
            return new DataPage<UriNode>(filter.PageNumber, filter.PageSize, 0,
                Array.Empty<UriNode>());
        }

        nodes = nodes.OrderBy(n => n.Label).ThenBy(n => n.Id);
        List<EfNode> results = nodes.Skip(filter.GetSkipCount())
            .Take(filter.PageSize).ToList();
        return new DataPage<UriNode>(filter.PageNumber, filter.PageSize, total,
            results.Select(n => n.ToUriNode(n.UriEntry!.Uri)).ToArray());
    }

    /// <summary>
    /// Gets all the nodes with the specified IDs.
    /// </summary>
    /// <param name="ids">The nodes IDs.</param>
    /// <returns>List of nodes (or null), one per ID.</returns>
    public IList<UriNode?> GetNodes(IList<int> ids)
    {
        if (ids is null) throw new ArgumentNullException(nameof(ids));

        using CadmusGraphDbContext context = GetContext();
        IList<EfNode> nodes = context.Nodes
            .Include(n => n.UriEntry)
            .Where(n => ids.Contains(n.Id))
            .AsNoTracking()
            .ToList();

        return ids.Select(id =>
        {
            EfNode? node = nodes.FirstOrDefault(n => n.Id == id);
            return node?.ToUriNode(node.UriEntry!.Uri);
        }).ToList();
    }

    /// <summary>
    /// Gets the node with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The node or null if not found.</returns>
    public UriNode? GetNode(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfNode? node = context.Nodes
            .Include(n => n.UriEntry)
            .AsNoTracking()
            .FirstOrDefault(n => n.Id == id);
        return node?.ToUriNode(node.UriEntry!.Uri);
    }

    private UriNode? GetNodeByUri(string uri, CadmusGraphDbContext context)
    {
        EfNode? node = context.Nodes
            .Include(n => n.UriEntry)
            .AsNoTracking()
            .FirstOrDefault(n => n.UriEntry!.Uri == uri);
        return node?.ToUriNode(node.UriEntry!.Uri);
    }

    /// <summary>
    /// Gets the node by its URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The node or null if not found.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public UriNode? GetNodeByUri(string uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        using CadmusGraphDbContext context = GetContext();
        return GetNodeByUri(uri, context);
    }

    private (int A, int Sub) GetASubIds()
    {
        using CadmusGraphDbContext context = GetContext();

        // a
        EfNode? a = context.Nodes.Include(n => n.UriEntry)
            .AsNoTracking()
            .FirstOrDefault(n => n.UriEntry!.Uri == "rdf:type");
        if (a == null)
        {
            context.Nodes.Add(a = new EfNode
            {
                Id = AddUri("rdf:type", context),
                Label = "is-a",
                Tag = "property"
            });
        }

        // sub
        EfNode? sub = context.Nodes.Include(n => n.UriEntry)
            .AsNoTracking()
            .FirstOrDefault(n => n.UriEntry!.Uri == "rdfs:subClassOf");
        if (sub == null)
        {
            context.Nodes.Add(sub = new EfNode
            {
                Id = AddUri("rdfs:subClassOf", context),
                Label = "is-subclass-of",
                Tag = "property"
            });
        }

        context.SaveChanges();
        return (a.Id, sub.Id);
    }

    private static void UpdateNodeClasses(int nodeId, int aId, int subId,
        CadmusGraphDbContext context)
    {
        context.Database.ExecuteSqlRaw(
            $"CALL populate_node_class({nodeId},{aId},{subId})");
    }

    private void AddNode(Node node, bool noUpdate, CadmusGraphDbContext? context)
    {
        bool localContext = context == null;
        context ??= GetContext();

        EfNode? old = context.Nodes.FirstOrDefault(n => n.Id == node.Id);
        if (noUpdate && old != null) return;
        if (old != null)
        {
            old.IsClass = node.IsClass;
            old.Tag = node.Tag;
            old.Label = node.Label;
            old.SourceType = node.SourceType;
            old.Sid = node.Sid;
        }
        else context.Nodes.Add(new EfNode(node));

        (int aId, int subId) = GetASubIds();
        UpdateNodeClasses(node.Id, aId, subId, context);

        context.SaveChanges();
        if (localContext) context.Dispose();
    }

    /// <summary>
    /// Adds or updates the specified node. Note that it is assumed that
    /// the node's ID has been set, either because the node already exists,
    /// or because its ID has been calculated with <see cref="AddUri(string)"/>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="noUpdate">True to avoid updating an existing node.
    /// When this is true, the node is added when not existing; when
    /// existing, nothing is done.</param>
    /// <exception cref="ArgumentNullException">node</exception>
    public void AddNode(Node node, bool noUpdate = false)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        AddNode(node, noUpdate, null);
    }

    /// <summary>
    /// Bulk imports the specified nodes. Note that here the nodes being
    /// imported are assumed to have a URI, while their ID will be
    /// calculated during import according to the URI value. Nodes without
    /// URI will not be imported.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    public void ImportNodes(IEnumerable<UriNode> nodes)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));

        using CadmusGraphDbContext context = GetContext();
        (int aId, int subId) = GetASubIds();
        foreach (UriNode node in nodes.Where(n => n.Uri != null))
        {
            EfUriEntry? uri = context.UriEntries.AsNoTracking()
                .FirstOrDefault(e => e.Uri == node.Uri);
            if (uri == null)
            {
                uri = new EfUriEntry
                {
                    Uri = node.Uri!
                };
                context.UriEntries.Add(uri);
            }
            context.Nodes.Add(new EfNode
            {
                Id = uri.Id,
                UriEntry = uri,
                IsClass = node.IsClass,
                Tag = node.Tag,
                Label = node.Label,
                SourceType = node.SourceType,
                Sid = node.Sid
            });
        }
        context.SaveChanges();

        foreach (int id in nodes.Select(n => n.Id))
            UpdateNodeClasses(id, aId, subId, context);
    }

    /// <summary>
    /// Deletes the node with the specified ID.
    /// </summary>
    /// <param name="id">The node identifier.</param>
    public void DeleteNode(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfNode? node = context.Nodes.FirstOrDefault(n => n.Id == id);
        if (node == null) return;
        context.Nodes.Remove(node);
        context.SaveChanges();
    }

    /// <summary>
    /// Gets the nodes included in a triple with the specified predicate ID
    /// and other node ID, either as its subject or as its object.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriNode> GetLinkedNodes(LinkedNodeFilter filter)
    {
        if (filter is null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfNode> nodes;

        if (filter.IsObject)
        {
            nodes = context.Nodes
                .Include(n => n.UriEntry)
                .Include(n => n.SubjectTriples)
                .AsNoTracking()
                .Where(n => n.Id == filter.OtherNodeId &&
                    n.SubjectTriples!.Any(t => t.PredicateId == filter.PredicateId));
        }
        else
        {
            nodes = context.Nodes
                .Include(n => n.UriEntry)
                .Include(n => n.ObjectTriples)
                .AsNoTracking()
                .Where(n => n.Id == filter.OtherNodeId &&
                    n.ObjectTriples!.Any(t => t.PredicateId == filter.PredicateId));
        }

        nodes = ApplyNodeFilterBase(nodes, filter);

        // get total and ret if zero
        int total = nodes.Count();
        if (total == 0)
        {
            return new DataPage<UriNode>(
                filter.PageNumber, filter.PageSize, 0, Array.Empty<UriNode>());
        }

        nodes = nodes.OrderBy(n => n.Label)
                     .ThenBy(n => n.UriEntry!.Uri).ThenBy(n => n.Id)
                     .Skip(filter.GetSkipCount()).Take(filter.PageSize);

        List<EfNode> results = nodes.ToList();
        return new DataPage<UriNode>(filter.PageNumber, filter.PageSize, total,
            results.Select(n => n.ToUriNode(n.UriEntry!.Uri)).ToList());
    }

    protected abstract string BuildRegexMatch(string field, string pattern);

    protected string BuildRawRegexSql(
        IList<Tuple<string, string?>> fieldAndPatterns)
    {
        StringBuilder sb = new();
        int n = 0;
        foreach (Tuple<string, string?> fp in fieldAndPatterns
            .Where(t => t.Item2 != null))
        {
            if (++n > 1) sb.AppendLine(" AND");
            sb.AppendLine(BuildRegexMatch(fp.Item1, fp.Item2!));
        }
        return sb.ToString();
    }

    private IQueryable<EfTriple> GetFilteredTriples(LiteralFilter filter,
        CadmusGraphDbContext context)
    {
        IQueryable<EfTriple> triples;

        if (!string.IsNullOrEmpty(filter.LiteralPattern))
        {
            triples = context.Triples
                .FromSqlRaw("SELECT * FROM triple WHERE " +
                BuildRawRegexSql(new[]
                {
                    Tuple.Create("o_lit", (string?)filter.LiteralPattern)
                }));
        }
        else triples = context.Triples;

        triples = triples.Include(t => t.Subject).ThenInclude(n => n.UriEntry)
                         .Include(t => t.Predicate).ThenInclude(n => n.UriEntry)
                         .Include(t => t.Object).ThenInclude(n => n.UriEntry);

        if (!string.IsNullOrEmpty(filter.LiteralType))
            triples = triples.Where(t => t.LiteralType == filter.LiteralType);

        if (!string.IsNullOrEmpty(filter.LiteralLanguage))
            triples = triples.Where(t => t.LiteralLanguage == filter.LiteralLanguage);

        if (filter.MinLiteralNumber.HasValue)
        {
            triples = triples.Where(t => t.LiteralNumber != null &&
                t.LiteralNumber >= filter.MinLiteralNumber.Value);
        }

        if (filter.MaxLiteralNumber.HasValue)
        {
            triples = triples.Where(t => t.LiteralNumber != null &&
                t.LiteralNumber <= filter.MaxLiteralNumber.Value);
        }

        return triples.AsNoTracking();
    }

    private IQueryable<EfTriple> GetFilteredTriples(TripleFilter filter,
        CadmusGraphDbContext context)
    {
        IQueryable<EfTriple> triples = GetFilteredTriples((LiteralFilter)filter,
            context);

        // sid
        if (!string.IsNullOrEmpty(filter.Sid))
        {
            if (filter.IsSidPrefix)
                triples = triples.Where(t => t.Sid!.StartsWith(filter.Sid));
            else
                triples = triples.Where(t => t.Sid == filter.Sid);
        }

        // tag (null if empty)
        if (!string.IsNullOrEmpty(filter.Tag))
        {
            if (filter.Tag.Length == 0)
                triples = triples.Where(t => t.Tag == null);
            else
                triples = triples.Where(t => t.Tag == filter.Tag);
        }

        return triples;
    }

    /// <summary>
    /// Gets the literals included in a triple with the specified subject ID
    /// and predicate ID.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriTriple> GetLinkedLiterals(LinkedLiteralFilter filter)
    {
        if (filter is null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();

        IQueryable<EfTriple> triples = GetFilteredTriples(filter, context);
        
        triples = triples.Where(t => t.SubjectId == filter.SubjectId &&
            t.PredicateId == filter.PredicateId &&
            t.ObjectId == null);

        // get total and return if zero
        int total = triples.Count();
        if (total == 0)
        {
            return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize,
                0, Array.Empty<UriTriple>());
        }

        triples = triples.OrderBy(t => t.ObjectLiteralIx).ThenBy(t => t.Id)
            .Skip(filter.GetSkipCount()).Take(filter.PageSize);

        List<EfTriple> results = triples.ToList();
        return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize, total,
            results.Select(t => t.ToUriTriple()).ToList());
    }
    #endregion

    #region Property
    /// <summary>
    /// Gets the specified page of properties.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    public DataPage<UriProperty> GetProperties(PropertyFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfProperty> properties = context.Properties
            .Include(p => p.Node)
            .ThenInclude(n => n.UriEntry)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(filter.Uid))
        {
            properties = properties.Where(
                p => p.Node!.UriEntry!.Uri.Contains(filter.Uid));
        }

        if (!string.IsNullOrEmpty(filter.DataType))
            properties = properties.Where(p => p.DataType == filter.DataType);

        if (!string.IsNullOrEmpty(filter.LiteralEditor))
            properties = properties.Where(p => p.LitEditor == filter.LiteralEditor);

        // get total and return if zero
        int total = properties.Count();
        if (total == 0)
        {
            return new DataPage<UriProperty>(filter.PageNumber, filter.PageSize,
                0, Array.Empty<UriProperty>());
        }

        properties = properties.OrderBy(p => p.Node!.UriEntry!.Uri)
            .Skip(filter.GetSkipCount()).Take(filter.PageSize);

        List<EfProperty> results = properties.ToList();

        return new DataPage<UriProperty>(filter.PageNumber, filter.PageSize,
            total, results.Select(p => p.ToUriProperty()).ToList());
    }

    /// <summary>
    /// Gets the property with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The property or null if not found.</returns>
    public UriProperty? GetProperty(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfProperty? property = context.Properties
            .Include(p => p.Node)
            .ThenInclude(n => n.UriEntry)
            .AsNoTracking()
            .FirstOrDefault(p => p.Id == id);
        return property?.ToUriProperty();
    }

    /// <summary>
    /// Gets the property by its URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The property or null if not found.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public UriProperty? GetPropertyByUri(string uri)
    {
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        using CadmusGraphDbContext context = GetContext();
        EfProperty? property = context.Properties
            .Include(p => p.Node)
            .ThenInclude(n => n.UriEntry)
            .AsNoTracking()
            .FirstOrDefault(p => p.Node!.UriEntry!.Uri == uri);
        return property?.ToUriProperty();
    }

    /// <summary>
    /// Adds or updates the specified property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <exception cref="ArgumentNullException">property</exception>
    public void AddProperty(Property property)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));

        using CadmusGraphDbContext context = GetContext();
        EfProperty? old = context.Properties
            .Include(p => p.Node)
            .ThenInclude(n => n.UriEntry)
            .FirstOrDefault(p => p.Id == property.Id);
        if (old != null)
        {
            old.DataType = property.DataType;
            old.LitEditor = property.LiteralEditor;
            old.Description = property.Description;
        }
        else
        {
            context.Properties.Add(new EfProperty(property));
        }
        context.SaveChanges();
    }

    /// <summary>
    /// Deletes the property with the specified ID.
    /// </summary>
    /// <param name="id">The property identifier.</param>
    public void DeleteProperty(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfProperty? property = context.Properties
            .FirstOrDefault(p => p.Id == id);
        if (property != null)
        {
            context.Properties.Remove(property);
            context.SaveChanges();
        }
    }
    #endregion

    #region Node Mappings
    private IQueryable<EfMapping> GetFilteredMappings(NodeMappingFilter filter,
        CadmusGraphDbContext context)
    {
        IQueryable<EfMapping> mappings;

        if (!string.IsNullOrEmpty(filter.Group) ||
            !string.IsNullOrEmpty(filter.Title))
        {
            mappings = context.Mappings
                .FromSqlRaw("SELECT * FROM node_mapping WHERE " +
                BuildRawRegexSql(new[]
                {
                    Tuple.Create("group_filter", filter.Group),
                    Tuple.Create("title_filter", filter.Title)
                }))
                .Include(m => m.MetaOutputs)
                .Include(m => m.NodeOutputs)
                .Include(m => m.TripleOutputs)
                .AsNoTracking();
        }
        else
        {
            mappings = context.Mappings
            .Include(m => m.MetaOutputs)
            .Include(m => m.NodeOutputs)
            .Include(m => m.TripleOutputs)
            .AsNoTracking();
        }

        if (filter.ParentId != null)
            mappings = mappings.Where(m => m.ParentId == filter.ParentId);

        if (filter.SourceType != null)
            mappings = mappings.Where(m => m.SourceType == filter.SourceType);

        if (!string.IsNullOrEmpty(filter.Name))
            mappings = mappings.Where(m => m.Name.Contains(filter.Name));

        if (!string.IsNullOrEmpty(filter.Facet))
            mappings = mappings.Where(m => m.FacetFilter == filter.Facet);

        if (filter.Flags.HasValue)
        {
            mappings = mappings.Where(
                m => (m.FlagsFilter & filter.Flags) == filter.Flags);
        }

        if (!string.IsNullOrEmpty(filter.PartType))
            mappings = mappings.Where(m => m.PartTypeFilter == filter.PartType);

        if (!string.IsNullOrEmpty(filter.PartRole))
            mappings = mappings.Where(m => m.PartRoleFilter == filter.PartRole);

        return mappings;
    }

    private IQueryable<EfMapping> GetFilteredMappings(
        RunNodeMappingFilter filter, CadmusGraphDbContext context)
    {
        IQueryable<EfMapping> mappings;

        // group and title
        if (!string.IsNullOrEmpty(filter.Group) ||
            !string.IsNullOrEmpty(filter.Title))
        {
            StringBuilder sb = new();
            sb.AppendLine("SELECT * FROM node_mapping WHERE ");

            if (!string.IsNullOrEmpty(filter.Group))
            {
                sb.Append("(group_filter IS NULL OR ")
                  .Append(BuildRawRegexSql(new[]
                    {
                        Tuple.Create("group_filter", (string?)filter.Group),
                    }))
                  .AppendLine(")");
            }
            if (!string.IsNullOrEmpty(filter.Title))
            {
                if (!string.IsNullOrEmpty(filter.Group)) sb.AppendLine("AND");

                sb.Append("(title_filter IS NULL OR ")
                  .Append(BuildRawRegexSql(new[]
                    {
                        Tuple.Create("title_filter", (string?)filter.Title),
                    }))
                  .AppendLine(")");
            }

            mappings = context.Mappings
                .FromSqlRaw(sb.ToString())
                .Include(m => m.MetaOutputs)
                .Include(m => m.NodeOutputs)
                .Include(m => m.TripleOutputs)
                .AsNoTracking();
        }
        else
        {
            mappings = context.Mappings
            .Include(m => m.MetaOutputs)
            .Include(m => m.NodeOutputs)
            .Include(m => m.TripleOutputs)
            .AsNoTracking();
        }

        // only root mappings for the requested source type
        mappings = mappings.Where(m => m.ParentId == null &&
            m.SourceType == filter.SourceType);

        // facet
        if (!string.IsNullOrEmpty(filter.Facet))
        {
            mappings = mappings.Where(
                m => m.FacetFilter == null || m.FacetFilter == filter.Facet);
        }

        // flags (all the flags specified must be present)
        if (filter.Flags.HasValue)
        {
            mappings = mappings.Where(
                m => m.FlagsFilter == null ||
                ((m.FlagsFilter & filter.Flags) == filter.Flags));
        }

        // part type
        if (!string.IsNullOrEmpty(filter.PartType))
        {
            mappings = mappings.Where(m => m.PartTypeFilter == null ||
                m.PartTypeFilter == filter.PartType);
        }

        // part role
        if (!string.IsNullOrEmpty(filter.PartRole))
        {
            mappings = mappings.Where(m => m.PartRoleFilter == null ||
                m.PartRoleFilter == filter.PartRole);
        }

        return mappings;
    }

    private NodeMapping GetPopulatedMapping(EfMapping mapping,
        CadmusGraphDbContext context)
    {
        // use the cached mapping if any
        if (Cache != null &&
            Cache.TryGetValue($"nm-{mapping.Id}", out NodeMapping? m))
        {
            return m!;
        }

        mapping.Children = context.Mappings
            .Where(m => m.ParentId == mapping.Id)
            .OrderBy(m => m.Name).ThenBy(m => m.Id)
            .ToList();

        foreach (EfMapping child in mapping.Children)
            GetPopulatedMapping(child, context);

        NodeMapping result = mapping.ToNodeMapping();
        Cache?.Set($"nm-{mapping.Id}", result);
        return result;
    }

    /// <summary>
    /// Gets the specified page of node mappings.
    /// </summary>
    /// <param name="filter">The filter. Set page size=0 to get all
    /// the mappings at once.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<NodeMapping> GetMappings(NodeMappingFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfMapping> mappings = GetFilteredMappings(filter, context);

        // get total and return if it's zero
        int total = mappings.Count();
        if (total == 0)
        {
            return new DataPage<NodeMapping>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<NodeMapping>());
        }

        mappings = mappings.OrderBy(m => m.Name).ThenBy(m => m.Id);

        List<EfMapping> results = filter.PageSize > 0
            ? mappings.Skip(filter.GetSkipCount()).Take(filter.PageSize).ToList()
            : mappings.Skip(filter.GetSkipCount()).ToList();

        // populate descendants of each mapping, taking advantage of cache
        return new DataPage<NodeMapping>(filter.PageNumber, filter.PageSize,
            total, results.Select(m => GetPopulatedMapping(m, context)).ToList());
    }

    /// <summary>
    /// Gets the node mapping with the specified ID, including all its
    /// descendant mappings.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The mapping or null if not found.</returns>
    public NodeMapping? GetMapping(int id)
    {
        using CadmusGraphDbContext context = GetContext();

        EfMapping? mapping = context.Mappings
            .Include(m => m.MetaOutputs)
            .Include(m => m.NodeOutputs)
            .Include(m => m.TripleOutputs)
            .FirstOrDefault(m => m.Id == id);
        if (mapping == null) return null;

        return GetPopulatedMapping(mapping, context);
    }

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

    /// <summary>
    /// Deletes the specified node mapping.
    /// </summary>
    /// <param name="id">The mapping identifier.</param>
    public void DeleteMapping(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfMapping? mapping = context.Mappings.FirstOrDefault(m => m.Id == id);
        if (mapping == null) return;
        context.Mappings.Remove(mapping);
        context.SaveChanges();
    }

    /// <summary>
    /// Finds all the applicable mappings.
    /// </summary>
    /// <param name="filter">The filter to match.</param>
    /// <returns>List of mappings.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public IList<NodeMapping> FindMappings(RunNodeMappingFilter filter)
    {
        if (filter is null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();

        IList<EfMapping> mappings = GetFilteredMappings(filter, context)
            .OrderBy(m => m.Ordinal)
            .ToList();

        return mappings.Select(m => GetPopulatedMapping(m, context)).ToList();
    }

    private static JsonSerializerOptions GetMappingJsonSerializerOptions()
    {
        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new NodeMappingOutputJsonConverter());
        return options;
    }

    /// <summary>
    /// Imports mappings from the specified JSON code representing a mappings
    /// document.
    /// </summary>
    /// <param name="json">The json.</param>
    /// <returns>The number of root mappings imported.</returns>
    /// <exception cref="ArgumentNullException">json</exception>
    /// <exception cref="InvalidDataException">Invalid JSON mappings document
    /// </exception>
    public int Import(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        NodeMappingDocument? doc =
            JsonSerializer.Deserialize<NodeMappingDocument>(json,
            GetMappingJsonSerializerOptions())
            ?? throw new InvalidDataException("Invalid JSON mappings document");

        int n = 0;
        foreach (NodeMapping mapping in doc.GetMappings())
        {
            AddMapping(mapping);
            n++;
        }
        return n;
    }

    /// <summary>
    /// Exports mappings to JSON code representing a mappings document.
    /// </summary>
    /// <returns>JSON.</returns>
    public string Export()
    {
        NodeMappingDocument doc = new();
        NodeMappingFilter filter = new();
        DataPage<NodeMapping> page = GetMappings(filter);
        do
        {
            doc.DocumentMappings.AddRange(page.Items);
            filter.PageNumber++;
        } while (filter.PageNumber < page.PageCount);

        return JsonSerializer.Serialize(doc, GetMappingJsonSerializerOptions());
    }
    #endregion

    #region Triples
    /// <summary>
    /// Gets the specified page of triples.
    /// </summary>
    /// <param name="filter">The filter. You can set the page size to 0
    /// to get all the matches at once.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriTriple> GetTriples(TripleFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfTriple> triples = GetFilteredTriples(filter, context);

        // get total and return if zero
        int total = triples.Count();
        if (total == 0)
        {
            return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize,
                0, Array.Empty<UriTriple>());
        }

        triples = triples.OrderBy(t => t.Subject!.UriEntry!.Uri)
            .ThenBy(t => t.Predicate!.UriEntry!.Uri)
            .ThenBy(t => t.Id)
            .Skip(filter.GetSkipCount());

        if (filter.PageSize > 0) triples = triples.Take(filter.PageSize);

        return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize,
            total, triples.Select(t => t.ToUriTriple()).ToList());
    }

    /// <summary>
    /// Gets the triple with the specified ID.
    /// </summary>
    /// <param name="id">The triple's ID.</param>
    public UriTriple? GetTriple(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfTriple? triple = context.Triples
            .Include(t => t.Subject)
            .Include(t => t.Predicate)
            .Include(t => t.Object)
            .FirstOrDefault(t => t.Id == id);
        return triple?.ToUriTriple();
    }

    private static int FindTripleByValue(Triple triple,
        CadmusGraphDbContext context)
    {
        IQueryable<EfTriple> triples = context.Triples
            .Where(t => t.SubjectId == triple.SubjectId &&
                        t.PredicateId == triple.PredicateId &&
                        t.Sid == triple.Sid &&
                        t.Tag == triple.Tag);

        if (triple.ObjectId > 0)
            triples = triples.Where(t => t.ObjectId == triple.ObjectId);
        else
            triples = triples.Where(t => t.ObjectId == null &&
                t.ObjectLiteral == triple.ObjectLiteral &&
                t.LiteralType == triple.LiteralType &&
                t.LiteralLanguage == triple.LiteralLanguage);

        return triples.Select(t => t.Id).FirstOrDefault();
    }

    private static void AddTriple(Triple triple, CadmusGraphDbContext context)
    {
        // nope if exactly the same triple already exists.
        // In this case, update the triple's ID to ensure it's valid
        int existingId = FindTripleByValue(triple, context);
        if (existingId > 0)
        {
            triple.Id = existingId;
            return;
        }

        EfTriple? old = triple.Id == 0 ? null : context.Triples.Find(triple.Id);
        if (old == null)
        {
            EfTriple newTriple = new(triple);
            context.Triples.Add(newTriple);
            context.SaveChanges();
            triple.Id = newTriple.Id;
        }
        else
        {
            old.SubjectId = triple.SubjectId;
            old.PredicateId = triple.PredicateId;
            old.ObjectId = triple.ObjectId;
            old.ObjectLiteral = triple.ObjectLiteral;
            old.ObjectLiteralIx = triple.ObjectLiteralIx;
            old.LiteralType = triple.LiteralType;
            old.LiteralLanguage = triple.LiteralLanguage;
            old.LiteralNumber = triple.LiteralNumber;
            old.Sid = triple.Sid;
            old.Tag = triple.Tag;
            context.SaveChanges();
        }
    }

    /// <summary>
    /// Adds or updates the specified triple. If the triple is new (ID=0)
    /// and a triple with all the same values already exists, nothing is
    /// done.
    /// When <paramref name="triple"/> has ID=0 (=new triple), its
    /// <see cref="Triple.Id"/> property gets updated by this method
    /// after insertion.
    /// </summary>
    /// <param name="triple">The triple.</param>
    /// <exception cref="ArgumentNullException">triple</exception>
    public void AddTriple(Triple triple)
    {
        if (triple == null) throw new ArgumentNullException(nameof(triple));

        using CadmusGraphDbContext context = GetContext();
        AddTriple(triple, context);
    }

    /// <summary>
    /// Bulk imports the specified triples. Note that it is assumed that
    /// all the triple's non-literal terms have a URI; if it is missing,
    /// the triple will not be imported. The URI is used to generate the
    /// corresponding numeric IDs used internally.
    /// </summary>
    /// <param name="triples">The triples.</param>
    /// <exception cref="ArgumentNullException">triples</exception>
    public void ImportTriples(IEnumerable<UriTriple> triples)
    {
        if (triples is null) throw new ArgumentNullException(nameof(triples));

        using CadmusGraphDbContext context = GetContext();
        foreach (UriTriple triple in triples)
        {
            if (triple.SubjectUri == null || triple.PredicateUri == null ||
                (triple.ObjectLiteral == null && triple.ObjectUri == null))
            {
                continue;
            }

            // add subject - the node is added if not present
            int id = AddUri(triple.SubjectUri, context, out bool newUri);
            triple.SubjectId = id;
            if (newUri)
            {
                AddNode(new Node
                {
                    Id = id,
                    Label = triple.SubjectUri,
                    Sid = triple.Sid,
                }, false, context);
            }

            // add predicate - the node is added if not present
            id = AddUri(triple.PredicateUri, context, out newUri);
            triple.PredicateId = id;
            if (newUri)
            {
                AddNode(new Node
                {
                    Id = id,
                    Label = triple.PredicateUri,
                    Sid = triple.Sid,
                    Tag = Node.TAG_PROPERTY
                }, false, context);
            }

            // add object if it's a node
            if (triple.ObjectUri != null)
            {
                id = AddUri(triple.ObjectUri, context, out newUri);
                triple.ObjectId = id;
                if (newUri)
                {
                    AddNode(new Node
                    {
                        Id = id,
                        Label = triple.ObjectUri,
                        Sid = triple.Sid
                    }, false, context);
                }
            }

            // add triple
            context.Triples.Add(new EfTriple(triple));
        }
        context.SaveChanges();
    }

    /// <summary>
    /// Deletes the triple with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public void DeleteTriple(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfTriple? triple = context.Triples.Find(id);
        if (triple != null)
        {
            context.Triples.Remove(triple);
            context.SaveChanges();

            if (triple.ObjectId != null)
            {
                (int aId, int subId) = GetASubIds();
                UpdateNodeClasses(triple.ObjectId.Value, aId, subId, context);
            }
        }
    }

    /// <summary>
    /// Gets the specified page of triples variously filtered, and grouped
    /// by their predicate.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="sort">The sort order: any combination of <c>c</c>=by
    /// count, ascending; <c>C</c>=by count, descending; <c>u</c>=by URI,
    /// ascending; <c>U</c>=by URI, descending.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter or sort</exception>
    public DataPage<TripleGroup> GetTripleGroups(TripleFilter filter,
        string sort = "Cu")
    {
        if (filter is null) throw new ArgumentNullException(nameof(filter));
        if (sort is null) throw new ArgumentNullException(nameof(sort));

        using CadmusGraphDbContext context = GetContext();
        // inner query:
        // select p_id, count(p_id) as cnt FROM triple
        // where ...
        // group by p_id
        IQueryable<EfTriple> triples = GetFilteredTriples(filter, context);
        IQueryable<TripleGroup> groups = triples.GroupBy(t => t.PredicateId)
            .Select(g => new TripleGroup
            {
                PredicateId = g.Key,
                Count = g.Count()
            });
        int total = groups.Count();
        if (total == 0)
        {
            return new DataPage<TripleGroup>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<TripleGroup>());
        }

        // get groups PredicateUri by joining with node
        groups = groups.Join(context.Nodes.Include(n => n.UriEntry),
            g => g.PredicateId,
            n => n.Id,
            (g, n) => new TripleGroup
            {
                PredicateId = g.PredicateId,
                PredicateUri = n.UriEntry.Uri,
                Count = g.Count
            });

        // sorting
        IOrderedQueryable<TripleGroup> sortedGroups = sort[0] switch
        {
            'c' => groups.OrderBy(g => g.Count),
            'C' => groups.OrderByDescending(g => g.Count),
            'u' => groups.OrderBy(g => g.PredicateUri),
            'U' => groups.OrderByDescending(g => g.PredicateUri),
            _ => groups.OrderByDescending(g => g.Count),
        };
        for (int i = 1; i < sort.Length; i++)
        {
            sortedGroups = sort[i] switch
            {
                'c' => sortedGroups.ThenBy(g => g.Count),
                'C' => sortedGroups.ThenByDescending(g => g.Count),
                'u' => sortedGroups.ThenBy(g => g.PredicateUri),
                'U' => sortedGroups.ThenByDescending(g => g.PredicateUri),
                _ => sortedGroups.ThenByDescending(g => g.Count),
            };
        }

        List<TripleGroup> results = sortedGroups.Skip(filter.GetSkipCount())
            .Take(filter.PageSize).ToList();

        return new DataPage<TripleGroup>(filter.PageNumber, filter.PageSize,
            total, results);
    }
    #endregion

    #region Thesaurus
    /// <summary>
    /// Adds the specified thesaurus as a set of class nodes.
    /// </summary>
    /// <param name="thesaurus">The thesaurus.</param>
    /// <param name="includeRoot">If set to <c>true</c>, include a root node
    /// corresponding to the thesaurus ID. This typically happens for
    /// non-hierarchic thesauri, where a flat list of entries is grouped
    /// under a single root.</param>
    /// <param name="prefix">The optional prefix to prepend to each ID.</param>
    /// <exception cref="ArgumentNullException">thesaurus</exception>
    public void AddThesaurus(Thesaurus thesaurus, bool includeRoot,
        string? prefix = null)
    {
        if (thesaurus is null) throw new ArgumentNullException(nameof(thesaurus));

        // nothing to do for aliases
        if (thesaurus.TargetId != null) return;

        using CadmusGraphDbContext context = GetContext();

        // ensure that we have rdfs:subClassOf
        Node? sub = GetNodeByUri("rdfs:subClassOf", context);
        if (sub == null)
        {
            EfNode ef = new()
            {
                Id = AddUri("rdfs:subClassOf", context),
                Label = "rdfs:subClassOf",
                IsClass = true
            };
            context.Nodes.Add(ef);
            sub = ef.ToUriNode("rdfs:subClassOf");
        }

        // include root if requested
        EfNode? root = null;
        if (includeRoot)
        {
            int atIndex = thesaurus.Id.LastIndexOf('@');
            string id = atIndex > -1
                ? thesaurus.Id[..atIndex]
                : thesaurus.Id;
            string uri = string.IsNullOrEmpty(prefix)
                ? id : prefix + id;

            root = new EfNode
            {
                Id = AddUri(uri, context),
                Label = id,
                IsClass = true,
                SourceType = Node.SOURCE_THESAURUS,
                Tag = "thesaurus",
                Sid = thesaurus.Id
            };
            AddNode(root, true, context);
        }

        Dictionary<string, int> ids = new();
        thesaurus.VisitByLevel(entry =>
        {
            // node
            string uri = string.IsNullOrEmpty(prefix)
                ? entry.Id : prefix + entry.Id;
            EfNode node = new()
            {
                Id = AddUri(uri, context),
                IsClass = true,
                Label = entry.Id,
                SourceType = Node.SOURCE_THESAURUS,
                Tag = "thesaurus",
                Sid = thesaurus.Id
            };
            AddNode(node, true, context);
            ids[entry.Id] = node.Id;

            // triple
            if (entry.Parent != null)
            {
                AddTriple(new Triple
                {
                    SubjectId = node.Id,
                    PredicateId = sub.Id,
                    ObjectId = ids[entry.Parent.Id]
                }, context);
            }
            else if (root != null)
            {
                AddTriple(new Triple
                {
                    SubjectId = node.Id,
                    PredicateId = sub.Id,
                    ObjectId = root.Id
                }, context);
            }
            return true;
        });
        context.SaveChanges();
    }
    #endregion

    #region Node Classes
    /// <summary>
    /// Updates the classes for all the nodes belonging to any class.
    /// </summary>
    /// <param name="cancel">The cancel.</param>
    /// <param name="progress">The progress.</param>
    public Task UpdateNodeClassesAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        (int aId, int subId) = GetASubIds();

        using CadmusGraphDbContext context = GetContext();

        // get total nodes to go
        int total = context.Nodes.Count(n => !n.IsClass);
        ProgressReport? report = progress != null ? new ProgressReport() : null;
        int oldPercent = 0;

        foreach (int id in context.Nodes.Where(n => !n.IsClass).Select(n => n.Id))
        {
            if (cancel.IsCancellationRequested) break;

            UpdateNodeClasses(id, aId, subId, context);

            if (report != null && ++report.Count % 10 == 0)
            {
                report.Percent = report.Count * 100 / total;
                if (report.Percent != oldPercent)
                {
                    progress!.Report(report);
                    oldPercent = report.Percent;
                }
            }
        }
        context.SaveChanges();

        if (report != null)
        {
            report.Percent = 100;
            report.Count = total;
            progress!.Report(report);
        }

        return Task.CompletedTask;
    }
    #endregion

    public void DeleteGraphSet(string sourceId)
    {
        throw new NotImplementedException();
    }

    public GraphSet GetGraphSet(string sourceId)
    {
        throw new NotImplementedException();
    }

    public void UpdateGraph(GraphSet set)
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
