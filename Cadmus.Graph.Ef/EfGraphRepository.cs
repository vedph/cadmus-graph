﻿using Cadmus.Core.Config;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
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
    /// Gets or sets the cache.
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
        EfNode? node = context.Nodes
            .Include(n => n.UriEntry)
            .AsNoTracking()
            .FirstOrDefault(n => n.UriEntry!.Uri == uri);
        return node?.ToUriNode(node.UriEntry!.Uri);
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

        using CadmusGraphDbContext context = GetContext();
        EfNode? old = context.Nodes.FirstOrDefault(n => n.Id == node.Id);
        if (noUpdate && old != null) return;
        if (old != null)
        {
            old.IsClass = node.IsClass;
            old.Tag = node.Tag;
            old.Label = node.Label;
            old.SourceType = node.SourceType;
            old.Sid = node.Sid;
        } else context.Nodes.Add(new EfNode(node));

        (int aId, int subId) = GetASubIds();
        UpdateNodeClasses(node.Id, aId, subId, context);

        context.SaveChanges();
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

    protected string BuildRawRegexSql(string sqlHead,
        IList<Tuple<string, string>> fieldAndPatterns)
    {
        StringBuilder sb = new(sqlHead);
        foreach (var fp in fieldAndPatterns)
        {
            sb.AppendLine();
            sb.Append(BuildRegexMatch(fp.Item1, fp.Item2));
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
                .FromSqlRaw(BuildRawRegexSql("SELECT * FROM triple WHERE",
                new[]
                {
                    Tuple.Create("o_lit", filter.LiteralPattern)
                }));
        }
        else triples = context.Triples;

        triples = triples.Include(t => t.Subject).ThenInclude(n => n.UriEntry);

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

    public void DeleteGraphSet(string sourceId)
    {
        throw new NotImplementedException();
    }

    public void DeleteMapping(int id)
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

    public GraphSet GetGraphSet(string sourceId)
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

    public void ImportTriples(IEnumerable<UriTriple> triples)
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
