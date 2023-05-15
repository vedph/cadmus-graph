using Cadmus.Core.Config;
using Cadmus.Index.Sql;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Microsoft.Extensions.Caching.Memory;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Graph.Sql;

/// <summary>
/// Base class for SQL-based graph repositories.
/// </summary>
public abstract class SqlGraphRepository : IConfigurable<SqlOptions>
{
    /// <summary>
    /// Gets the connection string.
    /// </summary>
    protected string? ConnectionString { get; set; }

    /// <summary>
    /// Gets the SQL helper.
    /// </summary>
    protected ISqlHelper SqlHelper { get; }

    /// <summary>
    /// Gets the SQL compiler, set once in the constructor.
    /// </summary>
    protected Compiler SqlCompiler { get; }

    /// <summary>
    /// Gets or sets the optional cache to use for mappings. This improves
    /// performance when fetching mappings from the database. All the
    /// mappings are stored with key <c>nm-</c> + the mapping's ID.
    /// Avoid using this if editing mappings.
    /// </summary>
    public IMemoryCache? Cache { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlGraphRepository"/>
    /// class.
    /// </summary>
    /// <param name="compiler">The SQL compiler.</param>
    /// <param name="sqlHelper">The SQL helper</param>
    /// <exception cref="ArgumentNullException">compiler or sqlHelper</exception>
    protected SqlGraphRepository(Compiler compiler, ISqlHelper sqlHelper)
    {
        SqlCompiler = compiler ??
            throw new ArgumentNullException(nameof(compiler));
        SqlHelper = sqlHelper ??
            throw new ArgumentNullException(nameof(sqlHelper));
    }

    /// <summary>
    /// Configures the specified options. This sets the connection string.
    /// If overriding this for more options, be sure to call the base
    /// implementation.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(SqlOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        ConnectionString = options.ConnectionString;
    }

    /// <summary>
    /// Gets a connection.
    /// </summary>
    /// <returns>Connection.</returns>
    protected abstract IDbConnection GetConnection();

    /// <summary>
    /// Gets the query factory.
    /// </summary>
    /// <returns>Query factory.</returns>
    protected QueryFactory GetQueryFactory()
    {
        QueryFactory qf = new(GetConnection(), SqlCompiler);
        qf.Connection.Open();
        return qf;
    }

    #region Namespace Lookup
    private static void ApplyNamespaceFilter(NamespaceFilter filter,
        Query query)
    {
        if (!string.IsNullOrEmpty(filter.Prefix))
            query.WhereLike("id", "%" + filter.Prefix + "%");

        if (!string.IsNullOrEmpty(filter.Uri))
            query.WhereLike("uri", "%" + filter.Uri + "%");
    }

    /// <summary>
    /// Gets the specified page of namespaces with their prefixes.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<NamespaceEntry> GetNamespaces(NamespaceFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        using QueryFactory qf = GetQueryFactory();
        var query = qf.Query("namespace_lookup");
        ApplyNamespaceFilter(filter, query);

        // get count and ret if no result
        int total = query.Clone().Count<int>(new[] { "id" });
        if (total == 0)
        {
            return new DataPage<NamespaceEntry>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<NamespaceEntry>());
        }

        // complete query and get page
        query.Select("id", "uri")
             .OrderBy("id", "uri")
             .Skip(filter.GetSkipCount()).Limit(filter.PageSize);
        List<NamespaceEntry> nss = new();
        foreach (var d in query.Get())
        {
            nss.Add(new NamespaceEntry
            {
                Prefix = d.id,
                Uri = d.uri
            });
        }
        return new DataPage<NamespaceEntry>(filter.PageNumber,
            filter.PageSize, total, nss);
    }

    /// <summary>
    /// Looks up the namespace from its prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The namespace, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">prefix</exception>
    public string? LookupNamespace(string prefix)
    {
        if (prefix == null) throw new ArgumentNullException(nameof(prefix));

        using QueryFactory qf = GetQueryFactory();
        return qf.Query("namespace_lookup")
                 .Where("id", prefix)
                 .Select("uri").Get<string>().FirstOrDefault();
    }

    /// <summary>
    /// Adds or updates the specified namespace prefix.
    /// </summary>
    /// <param name="prefix">The namespace prefix.</param>
    /// <param name="uri">The namespace URI corresponding to
    /// <paramref name="prefix" />.</param>
    /// <exception cref="ArgumentNullException">prefix or uri</exception>
    public void AddNamespace(string prefix, string uri)
    {
        if (prefix == null) throw new ArgumentNullException(nameof(prefix));
        if (uri == null) throw new ArgumentNullException(nameof(uri));

        using QueryFactory qf = GetQueryFactory();
        bool update = qf.Query("namespace_lookup")
            .Where("id", prefix)
            .Where("uri", uri).Exists();

        if (update)
        {
            qf.Query("namespace_lookup").Where("id", prefix)
                .Update(new { uri });
        }
        else
        {
            qf.Query("namespace_lookup").Insert(new
            {
                id = prefix,
                uri
            });
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

        using QueryFactory qf = GetQueryFactory();
        qf.Query("namespace_lookup").Where("id", prefix).Delete();
    }

    /// <summary>
    /// Deletes the specified namespace with all its prefixes.
    /// </summary>
    /// <param name="uri">The namespace URI.</param>
    public void DeleteNamespaceByUri(string uri)
    {
        if (uri is null) throw new ArgumentNullException(nameof(uri));

        using QueryFactory qf = GetQueryFactory();
        qf.Query("namespace_lookup").Where("uri", uri).Delete();
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
    public string BuildUid(string uid, string sid)
    {
        if (uid == null) throw new ArgumentNullException(nameof(uid));
        if (sid == null) throw new ArgumentNullException(nameof(sid));

        using QueryFactory qf = GetQueryFactory();
        // check if any unsuffixed UID is already in use
        if (!qf.Query("uid_lookup").Where("unsuffixed", uid).Exists())
        {
            // no: just insert the unsuffixed UID
            qf.Query("uid_lookup").Insert(new
            {
                sid,
                unsuffixed = uid,
                has_suffix = false
            });
            return uid;
        }

        // yes: check if a record with the same unsuffixed & SID exists.
        // If so, reuse it; otherwise, add a new suffixed UID
        var d = qf.Query("uid_lookup")
                  .Where("unsuffixed", uid)
                  .Where("sid", sid)
                  .Select("id", "has_suffix").Get().FirstOrDefault();
        if (d != null)
        {
            // found: reuse it, nothing gets inserted
            int oldId = d.id;
            bool hasSuffix = Convert.ToBoolean(d.has_suffix);
            return hasSuffix ? uid + "#" + oldId : uid;
        }
        // not found: add a new suffix
        int id = qf.Query("uid_lookup").InsertGetId<int>(new
        {
            sid,
            unsuffixed = uid,
            has_suffix = true
        });
        return uid + "#" + id;
    }
    #endregion

    #region URI Lookup
    private static Tuple<int, bool> AddUri(string uri, QueryFactory qf,
        IDbTransaction trans)
    {
        // if the URI already exists, just return its ID
        int id = qf.Query("uri_lookup")
                    .Where("uri", uri).Get<int>(trans).FirstOrDefault();
        if (id > 0) return Tuple.Create(id, false);

        // else insert it
        return Tuple.Create(qf.Query("uri_lookup").InsertGetId<int>(new
        {
            uri
        }, trans), true);
    }

    private static int AddUri(string uri, QueryFactory qf)
    {
        // if the URI already exists, just return its ID
        int id = qf.Query("uri_lookup")
                    .Where("uri", uri).Get<int>().FirstOrDefault();
        if (id > 0) return id;

        // else insert it
        return qf.Query("uri_lookup").InsertGetId<int>(new
        {
            uri
        });
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

        using QueryFactory qf = GetQueryFactory();
        return AddUri(uri, qf);
    }

    /// <summary>
    /// Lookups the URI from its numeric ID.
    /// </summary>
    /// <param name="id">The numeric ID for the URI.</param>
    /// <returns>The URI, or null if not found.</returns>
    public string? LookupUri(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        return qf.Query("uri_lookup")
             .Where("id", id)
             .Select("uri")
             .Get<string>().FirstOrDefault();
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

        using QueryFactory qf = GetQueryFactory();
        return qf.Query("uri_lookup")
             .Where("uri", uri).Get<int>().FirstOrDefault();
    }
    #endregion

    #region Node
    private static void ApplyNodeFilterBase(NodeFilterBase filter, Query query)
    {
        // uid
        if (!string.IsNullOrEmpty(filter.Uid))
        {
            query.Join("uri_lookup AS ul", "node.id", "ul.id")
                 .WhereLike("uid", "%" + filter.Uid + "%");
        }

        // class
        if (filter.IsClass.HasValue)
            query.Where("is_class", filter.IsClass.Value);

        // tag
        if (filter.Tag != null)
        {
            if (filter.Tag.Length == 0) query.WhereNull("tag");
            else query.Where("tag", filter.Tag);
        }

        // label
        if (!string.IsNullOrEmpty(filter.Label))
            query.WhereLike("label", "%" + filter.Label + "%");

        // source type
        if (filter.SourceType.HasValue)
            query.Where("source_type", filter.SourceType.Value);

        // sid
        if (!string.IsNullOrEmpty(filter.Sid))
        {
            if (filter.IsSidPrefix) query.WhereLike("sid", filter.Sid + "%");
            else query.Where("sid", filter.Sid);
        }

        // class IDs
        if (filter.ClassIds?.Count > 0)
        {
            query.Join("node_class AS nc", "node.id", "nc.node_id")
                 .WhereIn("nc.class_id", filter.ClassIds);
        }
    }

    private static void ApplyNodeFilter(NodeFilter filter, Query query)
    {
        ApplyNodeFilterBase(filter, query);

        // linked node ID and role
        if (filter.LinkedNodeId > 0)
        {
            switch (char.ToUpperInvariant(filter.LinkedNodeRole))
            {
                case 'S':
                    query.Join("triple AS t",
                        j => j.WhereRaw("t.o_id=node.id AND t.s_id=" +
                        filter.LinkedNodeId));
                    break;
                case 'O':
                    query.Join("triple AS t",
                        j => j.WhereRaw("t.s_id=node.id AND t.o_id=" +
                        filter.LinkedNodeId));
                    break;
                default:
                    query.Join("triple AS t", j => j.WhereRaw(
                        $"(t.o_id=node.id AND t.s_id={filter.LinkedNodeId}) OR " +
                        $"(t.s_id=node.id AND t.o_id={filter.LinkedNodeId})"));
                    break;
            }
        }
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

        using QueryFactory qf = GetQueryFactory();
        Query query = qf.Query("node");
        ApplyNodeFilter(filter, query);

        // get total
        int total = query.Clone().Count<int>(new[] { "node.id" });
        if (total == 0)
        {
            return new DataPage<UriNode>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<UriNode>());
        }

        // complete query and get page
        query.Join("uri_lookup AS ul", "ul.id", "node.id")
             .Select("node.id", "node.is_class", "node.tag", "node.label",
                     "node.source_type", "node.sid", "ul.uri")
             .OrderBy("node.label", "node.id")
             .Skip(filter.GetSkipCount()).Limit(filter.PageSize);
        List<UriNode> nodes = new();
        foreach (var d in query.Get()) nodes.Add(GetUriNode(d));
        return new DataPage<UriNode>(filter.PageNumber, filter.PageSize,
            total, nodes);
    }

    private UriNode? GetNode(int id, QueryFactory qf)
    {
        var d = qf.Query("node")
          .Join("uri_lookup AS ul", "node.id", "ul.id")
          .Where("node.id", id)
          .Select("node.id", "node.is_class", "node.tag", "node.label",
                  "node.source_type", "node.sid", "ul.uri")
          .Get().FirstOrDefault();
        return d == null ? null : GetUriNode(d);
    }

    /// <summary>
    /// Gets the node with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The node or null if not found.</returns>
    public UriNode? GetNode(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        return GetNode(id, qf);
    }

    private static UriNode? GetNodeByUri(string uri, QueryFactory qf,
        IDbTransaction? trans = null)
    {
        Query query = qf.Query("node")
          .Join("uri_lookup AS ul", "node.id", "ul.id")
          .Where("uri", uri)
          .Select("node.id", "node.is_class", "node.tag", "node.label",
                  "node.source_type", "node.sid");

        var d = trans != null
            ? query.Get(trans).FirstOrDefault()
            : query.Get().FirstOrDefault();
        return d == null ? null : GetUriNode(d);
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

        using QueryFactory qf = GetQueryFactory();
        return GetNodeByUri(uri, qf);
    }

    /// <summary>
    /// Gets all the nodes with the specified IDs.
    /// </summary>
    /// <param name="ids">The nodes IDs.</param>
    /// <returns>List of nodes (or null), one per ID.</returns>
    public IList<UriNode?> GetNodes(IList<int> ids)
    {
        if (ids is null) throw new ArgumentNullException(nameof(ids));

        using QueryFactory qf = GetQueryFactory();
        List<UriNode?> nodes = new(ids.Count);
        foreach (int id in ids) nodes.Add(GetNode(id, qf));
        return nodes;
    }

    /// <summary>
    /// Adds the node only if it does not exist; else do nothing.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="qf">The query factory.</param>
    protected static void AddNodeIfNotExists(Node node, QueryFactory qf)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (qf is null) throw new ArgumentNullException(nameof(qf));

        if (qf.Query("node").Where("id", node.Id).Exists()) return;

        qf.Query("node").Insert(new
        {
            id = node.Id,
            is_class = node.IsClass,
            label = node.Label,
            tag = node.Tag,
            source_type = node.SourceType,
            sid = node.Sid
        });
    }

    private void AddNode(Node node, bool noUpdate, QueryFactory qf,
        IDbTransaction? trans = null, bool noClasses = false)
    {
        var d = new
        {
            id = node.Id,
            is_class = node.IsClass,
            label = node.Label,
            tag = node.Tag,
            source_type = node.SourceType,
            sid = node.Sid
        };
        if (trans == null)
        {
            if (qf.Query("node").Where("id", node.Id).Exists())
            {
                if (noUpdate) return;
                qf.Query("node").Where("id", node.Id).Update(d);
            }
            else qf.Query("node").Insert(d);
        }
        else
        {
            if (qf.Query("node").Where("id", node.Id).Exists(trans))
            {
                if (noUpdate) return;
                qf.Query("node").Where("id", node.Id).Update(d, trans);
            }
            else qf.Query("node").Insert(d, trans);
        }

        if (!noClasses)
        {
            var asIds = GetASubIds(qf, trans);
            UpdateNodeClasses(node.Id, asIds.Item1, asIds.Item2, qf, trans);
        }
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

        using QueryFactory qf = GetQueryFactory();
        AddNode(node, noUpdate, qf);
    }

    private static void ImportNode(UriNode node, QueryFactory qf,
        IDbTransaction trans, int aId, int subId)
    {
        var t = AddUri(node.Uri!, qf, trans);
        if (t.Item2)
        {
            var d = new
            {
                Id = t.Item1,
                is_class = node.IsClass,
                label = node.Label,
                tag = node.Tag,
                source_type = node.SourceType,
                sid = node.Sid
            };
            qf.Query("node").Insert(d, trans);
            UpdateNodeClasses(node.Id, aId, subId, qf, trans);
        }
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

        using QueryFactory qf = GetQueryFactory();
        IDbTransaction trans = qf.Connection.BeginTransaction();
        try
        {
            var asIds = GetASubIds(qf, trans);

            foreach (UriNode node in nodes)
            {
                if (node.Uri != null)
                    ImportNode(node, qf, trans, asIds.Item1, asIds.Item2);
            }

            trans.Commit();
        }
        catch (Exception)
        {
            trans.Rollback();
            throw;
        }
    }

    private static void DeleteNode(int id, QueryFactory qf) =>
        qf.Query("node").Where("id", id).Delete();

    /// <summary>
    /// Deletes the node with the specified ID.
    /// </summary>
    /// <param name="id">The node identifier.</param>
    public void DeleteNode(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        DeleteNode(id, qf);
    }

    private static UriNode GetUriNode(dynamic d)
    {
        return new UriNode
        {
            Id = d.id,
            IsClass = Convert.ToBoolean(d.is_class),
            Tag = d.tag,
            Label = d.label,
            SourceType = d.source_type,
            Sid = d.sid,
            Uri = d.uri
        };
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

        using QueryFactory qf = GetQueryFactory();
        // nodes filtered by triple
        var query = qf.Query("node")
                      .Where("triple.p_id", filter.PredicateId)
                      .Where(filter.IsObject
                        ? "triple.s_id" : "triple.o_id",
                        filter.OtherNodeId)
                      .Join("triple", filter.IsObject
                        ? "triple.o_id"
                        : "triple.s_id",
                        "node.id");

        // plus additional filters
        ApplyNodeFilterBase(filter, query);

        // get count and ret if no result
        int total = query.Clone().Select("node.id")
                         .Count<int>(new[] { "node.id" });
        if (total == 0)
        {
            return new DataPage<UriNode>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<UriNode>());
        }

        // complete query and get page
        query.Join("uri_lookup AS ul", "node.id", "ul.id")
             .Select("node.id", "node.is_class", "node.tag", "node.label",
                     "node.source_type", "node.sid", "ul.uri")
             .OrderBy("node.label", "ul.uri", "node.id")
             .Skip(filter.GetSkipCount()).Limit(filter.PageSize);

        List<UriNode> nodes = new(filter.PageSize);
        foreach (var d in query.Get()) nodes.Add(GetUriNode(d));
        return new DataPage<UriNode>(filter.PageNumber, filter.PageSize,
            total, nodes);
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

        using QueryFactory qf = GetQueryFactory();

        // literals from specified predicate
        var query = qf.Query("triple")
            .Where("s_id", filter.SubjectId)
            .Where("p_id", filter.PredicateId)
            .WhereNull("o_id");

        // plus additional filters
        ApplyLiteralFilter(filter, query);

        // get count and ret if no result
        int total = query.Clone().Select("id").Count<int>(new[] { "id" });
        if (total == 0)
        {
            return new DataPage<UriTriple>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<UriTriple>());
        }

        // complete query and get page
        query.Join("uri_lookup AS uls", "triple.s_id", "uls.id")
             .Join("uri_lookup AS ulp", "triple.p_id", "ulp.id")
             .Select("triple.id", "triple.s_id", "triple.p_id", "triple.o_id",
                     "triple.o_lit", "triple.o_lit_type", "triple.o_lit_lang",
                     "triple.o_lit_ix", "triple.o_lit_n", "triple.sid",
                     "triple.tag",
                     "uls.uri AS s_uri", "ulp.uri AS p_uri")
             .OrderBy("triple.o_lit_ix", "triple.id")
             .Skip(filter.GetSkipCount()).Limit(filter.PageSize);

        List<UriTriple> triples = new(filter.PageSize);
        foreach (var d in query.Get()) triples.Add(GetUriTriple(d));
        return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize,
            total, triples);
    }
    #endregion

    #region Property
    private static void ApplyPropertyFilter(PropertyFilter filter, Query query)
    {
        if (!string.IsNullOrEmpty(filter.Uid))
            query.WhereLike("ul.uri", "%" + filter.Uid + "%");

        if (!string.IsNullOrEmpty(filter.DataType))
            query.Where("data_type", filter.DataType);

        if (!string.IsNullOrEmpty(filter.LiteralEditor))
            query.Where("lit_editor", filter.LiteralEditor);
    }

    /// <summary>
    /// Gets the specified page of properties.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    public DataPage<UriProperty> GetProperties(PropertyFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        using QueryFactory qf = GetQueryFactory();
        Query query = qf.Query("property")
                    .Join("uri_lookup AS ul", "ul.id", "property.id");
        ApplyPropertyFilter(filter, query);

        // get total
        int total = query.Clone().Count<int>(new[] { "property.id" });
        if (total == 0)
        {
            return new DataPage<UriProperty>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<UriProperty>());
        }

        // complete query and get page
        query.Select("property.id", "property.data_type",
            "property.lit_editor", "property.description", "ul.uri")
             .OrderBy("ul.uri")
             .Skip(filter.GetSkipCount()).Limit(filter.PageSize);
        List<UriProperty> props = new();
        foreach (var d in query.Get())
        {
            props.Add(new UriProperty
            {
                Id = d.id,
                DataType = d.data_type,
                LiteralEditor = d.lit_editor,
                Description = d.description,
                Uri = d.uri
            });
        }

        return new DataPage<UriProperty>(filter.PageNumber,
            filter.PageSize, total, props);
    }

    /// <summary>
    /// Gets the property with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The property or null if not found.</returns>
    public UriProperty? GetProperty(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        var d = qf.Query("property")
            .Join("uri_lookup AS ul", "property.id", "ul.id")
            .Where("property.id", id)
            .Select("property.data_type", "property.lit_editor",
                "property.description", "ul.uri")
            .Get().FirstOrDefault();
        return d == null ? null : new UriProperty
        {
            Id = id,
            DataType = d.data_type,
            LiteralEditor = d.lit_editor,
            Description = d.description,
            Uri = d.uri
        };
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

        using QueryFactory qf = GetQueryFactory();
        var d = qf.Query("property")
            .Join("uri_lookup AS ul", "property.id", "ul.id")
            .Where("ul.uri", uri)
            .Select("property.id", "property.data_type",
                "property.lit_editor", "property.description")
            .Get().FirstOrDefault();
        return d == null ? null : new UriProperty
        {
            Id = d.id,
            DataType = d.data_type,
            LiteralEditor = d.lit_editor,
            Description = d.description,
            Uri = uri
        };
    }

    /// <summary>
    /// Adds or updates the specified property.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <exception cref="ArgumentNullException">property</exception>
    public void AddProperty(Property property)
    {
        if (property == null)
            throw new ArgumentNullException(nameof(property));

        using QueryFactory qf = GetQueryFactory();
        var d = new
        {
            id = property.Id,
            data_type = property.DataType,
            lit_editor = property.LiteralEditor,
            description = property.Description
        };
        if (qf.Query("property").Where("id", property.Id).Exists())
            qf.Query("property").Where("id", property.Id).Update(d);
        else
            qf.Query("property").Insert(d);
    }

    /// <summary>
    /// Deletes the property with the specified ID.
    /// </summary>
    /// <param name="id">The property identifier.</param>
    public void DeleteProperty(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        qf.Query("property").Where("id", id).Delete();
    }
    #endregion

    #region Node Mapping
    private void ApplyNodeMappingFilter(NodeMappingFilter filter, Query query)
    {
        if (filter.ParentId != null)
            query.Where("parent_id", filter.ParentId.Value);

        if (filter.SourceType.HasValue)
            query.Where("source_type", filter.SourceType);

        if (!string.IsNullOrEmpty(filter.Name))
            query.WhereLike("name", "%" + filter.Name + "%");

        if (!string.IsNullOrEmpty(filter.Facet))
            query.Where("facet_filter", filter.Facet);

        if (!string.IsNullOrEmpty(filter.Group))
        {
            query.WhereRaw(SqlHelper.BuildRegexMatch("group_filter",
                SqlHelper.SqlEncode(filter.Group, false, true, false)));
        }

        if (filter.Flags.HasValue)
        {
            query.WhereNotNull("flags_filter")
                .WhereRaw(
                $"(flags_filter & {filter.Flags.Value})={filter.Flags.Value}");
        }

        if (!string.IsNullOrEmpty(filter.Title))
        {
            query.WhereRaw(SqlHelper.BuildRegexMatch("title_filter",
                SqlHelper.SqlEncode(filter.Title, false, true, false)));
        }

        if (!string.IsNullOrEmpty(filter.PartType))
            query.Where("part_type_filter", filter.PartType);

        if (!string.IsNullOrEmpty(filter.PartRole))
            query.Where("part_role_filter", filter.PartRole);
    }

    private static NodeMapping? DataToMapping(dynamic? d)
    {
        if (d == null) return null;
        return new NodeMapping()
        {
            Id = d.id,
            ParentId = d.parent_id ?? 0,
            Ordinal = d.ordinal,
            SourceType = d.source_type,
            Name = d.name,
            FacetFilter = d.facet_filter,
            GroupFilter = d.group_filter,
            FlagsFilter = d.flags_filter,
            TitleFilter = d.title_filter,
            PartTypeFilter = d.part_type_filter,
            PartRoleFilter = d.part_role_filter,
            Description = d.description,
            Source = d.source,
            Sid = d.sid,
        };
    }

    private static void PopulateMappingOutput(NodeMapping mapping,
        QueryFactory qf)
    {
        mapping.Output = new();

        // nodes
        Query query = qf.Query("mapping_out_node")
            .Select("ordinal", "name", "uid", "label", "tag")
            .Where("mapping_id", mapping.Id)
            .OrderBy("ordinal");
        foreach (var d in query.Get())
        {
            mapping.Output.Nodes[d.name] = new MappedNode
            {
                Uid = d.uid,
                Label = d.label,
                Tag = d.tag
            };
        }

        // triples
        query = qf.Query("mapping_out_triple")
            .Select("ordinal", "s", "p", "o", "ol")
            .Where("mapping_id", mapping.Id)
            .OrderBy("ordinal");
        foreach (var d in query.Get())
        {
            mapping.Output.Triples.Add(new MappedTriple
            {
                S = d.s,
                P = d.p,
                O = d.o,
                OL = d.ol
            });
        }

        // metadata
        query = qf.Query("mapping_out_meta")
            .Select("ordinal", "name", "value")
            .Where("mapping_id", mapping.Id)
            .OrderBy("ordinal");
        foreach (var d in query.Get())
            mapping.Output.Metadata[d.name] = d.value;
    }

    private NodeMapping GetPopulatedMapping(NodeMapping mapping,
        QueryFactory qf, bool descendants)
    {
        // use the cached mapping if any
        if (Cache != null &&
            Cache.TryGetValue($"nm-{mapping.Id}", out NodeMapping m))
        {
            return m;
        }

        // else populate and cache for later (only if complete)
        PopulateMappingOutput(mapping, qf);
        if (descendants)
        {
            PopulateMappingDescendants(mapping, qf);
            Cache?.Set($"nm-{mapping.Id}", mapping);
        }

        return mapping;
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

        using QueryFactory qf = GetQueryFactory();
        Query query = qf.Query("mapping");
        ApplyNodeMappingFilter(filter, query);

        // get total
        int total = query.Clone().Count<int>(new[] { "id" });
        if (total == 0)
        {
            return new DataPage<NodeMapping>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<NodeMapping>());
        }

        // complete query and get page
        query.Select("id", "parent_id", "ordinal", "name", "source_type",
            "facet_filter", "group_filter", "flags_filter", "title_filter",
            "part_type_filter", "part_role_filter", "description",
            "source", "sid")
            .Skip(filter.GetSkipCount())
            .OrderBy("name", "id");
        if (filter.PageSize > 0) query.Limit(filter.PageSize);

        List<NodeMapping> mappings = new();
        foreach (var d in query.Get())
        {
            NodeMapping mapping = DataToMapping(d);
            mapping = GetPopulatedMapping(mapping, qf, true);
            mappings.Add(mapping);
        }

        return new DataPage<NodeMapping>(filter.PageNumber,
            filter.PageSize, total, mappings);
    }

    private void PopulateMappingDescendants(NodeMapping mapping, QueryFactory qf)
    {
        Query query = qf.Query("mapping").Where("parent_id", mapping.Id);
        foreach (var d in query.Get())
        {
            NodeMapping child = DataToMapping(d);
            mapping.Children.Add(child);
            PopulateMappingOutput(child, qf);
            PopulateMappingDescendants(child, qf);
        }
    }

    /// <summary>
    /// Gets the node mapping with the specified ID, including all its
    /// descendant mappings.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The mapping or null if not found.</returns>
    public NodeMapping? GetMapping(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        Query query = qf.Query("mapping").Where("id", id);
        var d = query.Get().FirstOrDefault();
        if (d == null) return null;

        NodeMapping mapping = DataToMapping(d);
        return GetPopulatedMapping(mapping, qf, true);
    }

    private static void DeleteMappingOutput(int id, QueryFactory qf,
        IDbTransaction trans)
    {
        qf.Query("mapping_out_node").Where("mapping_id", id).Delete(trans);
        qf.Query("mapping_out_triple").Where("mapping_id", id).Delete(trans);
        qf.Query("mapping_out_meta").Where("mapping_id", id).Delete(trans);
    }

    private static void AddMappingOutput(NodeMapping mapping, QueryFactory qf,
        IDbTransaction trans)
    {
        List<object?[]> data = new();

        if (mapping.Output?.HasNodes == true)
        {
            int n = 0;
            foreach (var p in mapping.Output.Nodes)
            {
                MappedNode node = p.Value;
                data.Add(new object?[]
                {
                    mapping.Id, ++n, p.Key, node.Uid, node.Label, node.Tag
                });
            }
            qf.Query("mapping_out_node")
              .Insert(new[] { "mapping_id", "ordinal", "name", "uid", "label", "tag" },
                      data, trans);
        }

        if (mapping.Output?.HasTriples == true)
        {
            data.Clear();
            int n = 0;
            foreach (MappedTriple triple in mapping.Output.Triples)
            {
                data.Add(new object?[]
                {
                    mapping.Id, ++n, triple.S, triple.P, triple.O, triple.OL
                });
            }
            qf.Query("mapping_out_triple")
              .Insert(new[] { "mapping_id", "ordinal", "s", "p", "o", "ol" },
                      data, trans);
        }

        if (mapping.Output?.HasMetadata == true)
        {
            data.Clear();
            int n = 0;
            foreach (var p in mapping.Output.Metadata)
            {
                data.Add(new object?[]
                {
                    mapping.Id, ++n, p.Key, p.Value
                });
            }
            qf.Query("mapping_out_meta")
              .Insert(new[] { "mapping_id", "ordinal", "name", "value" },
                      data, trans);
        }
    }

    private void AddMapping(NodeMapping mapping, QueryFactory qf,
        IDbTransaction trans)
    {
        // insert or update the mapping
        var newMapping = new
        {
            id = mapping.Id,
            parent_id = mapping.ParentId == 0
                    ? null : (int?)mapping.ParentId,
            ordinal = mapping.Ordinal,
            name = mapping.Name,
            source_type = mapping.SourceType,
            facet_filter = mapping.FacetFilter,
            group_filter = mapping.GroupFilter,
            flags_filter = mapping.FlagsFilter,
            title_filter = mapping.TitleFilter,
            part_type_filter = mapping.PartTypeFilter,
            part_role_filter = mapping.PartRoleFilter,
            description = mapping.Description,
            source = mapping.Source,
            sid = mapping.Sid
        };

        if (mapping.Id > 0
            && qf.Query("mapping").Where("id", mapping.Id).Exists())
        {
            qf.Query("mapping").Where("id", mapping.Id)
                .Update(newMapping, trans);
            DeleteMappingOutput(mapping.Id, qf, trans);
        }
        else
        {
            mapping.Id = qf.Query("mapping")
                .InsertGetId<int>(newMapping, trans);
            if (mapping.HasChildren)
            {
                foreach (NodeMapping child in mapping.Children)
                    child.ParentId = mapping.Id;
            }
        }

        // add its output
        if (mapping.Output != null) AddMappingOutput(mapping, qf, trans);

        // add its children
        if (mapping.HasChildren)
        {
            foreach (NodeMapping child in mapping.Children)
                AddMapping(child, qf, trans);
        }
    }

    /// <summary>
    /// Adds the specified node mapping with all its descendants.
    /// When <paramref name="mapping"/> has ID=0 (=new mapping), its
    /// <see cref="NodeMapping.Id"/> property gets updated by this method
    /// after insertion.
    /// </summary>
    /// <param name="mapping">The mapping.</param>
    /// <exception cref="ArgumentNullException">mapping</exception>
    public int AddMapping(NodeMapping mapping)
    {
        if (mapping == null) throw new ArgumentNullException(nameof(mapping));

        using QueryFactory qf = GetQueryFactory();
        IDbTransaction trans = qf.Connection.BeginTransaction();

        try
        {
            AddMapping(mapping, qf, trans);
            trans.Commit();
            return mapping.Id;
        }
        catch (Exception)
        {
            trans.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Deletes the specified node mapping.
    /// </summary>
    /// <param name="id">The mapping identifier.</param>
    public void DeleteMapping(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        qf.Query("mapping").Where("id", id).Delete();
    }

    private void ApplyRunNodeMappingsFilter(RunNodeMappingFilter filter,
        Query query)
    {
        // top-level mappings only, with the specified source type
        query.WhereNull("parent_id").Where("source_type", filter.SourceType);

        // optional facet
        if (!string.IsNullOrEmpty(filter.Facet))
        {
            query.Where(q =>
                q.Where("facet_filter", filter.Facet)
                 .OrWhereNull("facet_filter"));
        }

        // optional group
        if (!string.IsNullOrEmpty(filter.Group))
        {
            query.Where(q =>
                q.WhereRaw(SqlHelper.BuildRegexMatch("group_filter",
                    SqlHelper.SqlEncode(filter.Group, false, true, false)))
                 .OrWhereNull("group_filter"));
        }

        // optional flags (all the flags specified must be present)
        if (filter.Flags.HasValue)
        {
            query.Where(q =>
                q.WhereNull("flags_filter")
                 .OrWhereRaw($"(flags_filter & {filter.Flags})={filter.Flags}"));
        }

        // optional title
        if (!string.IsNullOrEmpty(filter.Title))
        {
            query.Where(q =>
                q.Where("title_filter", filter.Title)
                 .OrWhereNull("title_filter"));
        }

        // optional part type
        if (!string.IsNullOrEmpty(filter.PartType))
        {
            query.Where(q =>
                q.Where("part_type_filter", filter.PartType)
                 .OrWhereNull("part_type_filter"));
        }

        // optional part role
        if (!string.IsNullOrEmpty(filter.PartRole))
        {
            query.Where(q =>
                q.Where("part_role_filter", filter.PartRole)
                 .OrWhereNull("part_role_filter"));
        }
    }

    /// <summary>
    /// Finds all the applicable mappings.
    /// </summary>
    /// <param name="filter">The filter to match.</param>
    /// <returns>List of mappings.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public IList<NodeMapping> FindMappings(RunNodeMappingFilter filter)
    {
        if (filter is null)
            throw new ArgumentNullException(nameof(filter));

        using QueryFactory qf = GetQueryFactory();
        Query query = qf.Query("mapping")
            .Select("id", "parent_id", "ordinal", "name", "source_type",
            "facet_filter", "group_filter", "flags_filter", "title_filter",
            "part_type_filter", "part_role_filter", "description",
            "source", "sid");
        ApplyRunNodeMappingsFilter(filter, query);
        query.OrderBy("ordinal");

        List<NodeMapping> mappings = new();
        foreach (var d in query.Get())
        {
            var mapping = DataToMapping(d);
            mapping = GetPopulatedMapping(mapping, qf, true);
            mappings.Add(mapping);
        }

        return mappings;
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
    private void ApplyLiteralFilter(LiteralFilter filter, Query query)
    {
        if (!string.IsNullOrEmpty(filter.LiteralPattern))
        {
            query.WhereRaw(SqlHelper.BuildRegexMatch("o_lit",
                SqlHelper.SqlEncode(filter.LiteralPattern, false, true, false)));
        }

        if (!string.IsNullOrEmpty(filter.LiteralType))
            query.Where("o_lit_type", filter.LiteralType);

        if (!string.IsNullOrEmpty(filter.LiteralLanguage))
            query.Where("o_lit_lang", filter.LiteralLanguage);

        if (filter.MinLiteralNumber.HasValue)
        {
            query.WhereNotNull("o_lit_n")
                 .Where("o_lit_n", ">=", filter.MinLiteralNumber.Value);
        }

        if (filter.MaxLiteralNumber.HasValue)
        {
            query.WhereNotNull("o_lit_n")
                 .Where("o_lit_n", "<=", filter.MaxLiteralNumber.Value);
        }
    }

    private void ApplyTripleFilter(TripleFilter filter, Query query)
    {
        // subject
        if (filter.SubjectId > 0)
            query.Where("s_id", filter.SubjectId);

        // predicate
        if (filter.PredicateIds?.Count > 0)
            query.WhereIn("p_id", filter.PredicateIds);
        if (filter.NotPredicateIds?.Count > 0)
            query.WhereNotIn("p_id", filter.PredicateIds);

        // has literal and object ID
        if (filter.HasLiteralObject != null)
        {
            if (filter.HasLiteralObject.Value)
                query.WhereNull("o_id");
            else
                query.WhereNotNull("o_id");
        }
        if (filter.ObjectId > 0)
            query.Where("o_id", filter.ObjectId);

        // literal
        ApplyLiteralFilter(filter, query);

        // sid
        if (!string.IsNullOrEmpty(filter.Sid))
        {
            if (filter.IsSidPrefix) query.WhereLike("sid", filter.Sid + "%");
            else query.Where("sid", filter.Sid);
        }

        // tag
        if (filter.Tag != null)
        {
            if (filter.Tag.Length == 0) query.WhereNull("tag");
            else query.Where("tag", filter.Tag);
        }
    }

    private static UriTriple GetUriTriple(dynamic d)
    {
        return new UriTriple
        {
            Id = d.id,
            SubjectId = d.s_id,
            PredicateId = d.p_id,
            ObjectId = d.o_id ?? 0,
            ObjectLiteral = d.o_lit,
            ObjectLiteralIx = d.o_lit_ix,
            LiteralType = d.o_lit_type,
            LiteralLanguage = d.o_lit_lang,
            LiteralNumber = d.o_lit_n,
            Sid = d.sid,
            Tag = d.tag,
            SubjectUri = d.s_uri,
            PredicateUri = d.p_uri,
            ObjectUri = d.o_uri
        };
    }

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

        using QueryFactory qf = GetQueryFactory();
        Query query = qf.Query("triple")
            .Join("uri_lookup AS uls", "triple.s_id", "uls.id")
            .Join("uri_lookup AS ulp", "triple.p_id", "ulp.id")
            .LeftJoin("uri_lookup AS ulo", "triple.o_id", "ulo.id");
        ApplyTripleFilter(filter, query);

        // get total
        int total = query.Clone().Count<int>(new[] { "triple.id" });
        if (total == 0)
        {
            return new DataPage<UriTriple>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<UriTriple>());
        }

        // complete query and get page
        query.Select("triple.id", "triple.s_id", "triple.p_id",
            "triple.o_id", "triple.o_lit", "triple.sid", "triple.tag",
            "triple.o_lit_ix",
            "triple.o_lit_type", "triple.o_lit_lang", "triple.o_lit_n",
            "uls.uri AS s_uri", "ulp.uri AS p_uri", "ulo.uri AS o_uri")
             .OrderBy("s_uri", "p_uri", "triple.id")
             .Skip(filter.GetSkipCount());
        if (filter.PageSize > 0) query.Limit(filter.PageSize);

        List<UriTriple> triples = new();
        foreach (var d in query.Get()) triples.Add(GetUriTriple(d));
        return new DataPage<UriTriple>(filter.PageNumber,
            filter.PageSize, total, triples);
    }

    /// <summary>
    /// Gets the triple with the specified ID.
    /// </summary>
    /// <param name="id">The triple's ID.</param>
    public UriTriple? GetTriple(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        var d = qf.Query("triple")
            .Join("uri_lookup AS uls", "triple.s_id", "uls.id")
            .Join("uri_lookup AS ulp", "triple.p_id", "ulp.id")
            .LeftJoin("uri_lookup AS ulo", "triple.o_id", "ulo.id")
            .Where("triple.id", id)
            .Select("triple.id", "triple.s_id", "triple.p_id", "triple.o_id",
            "triple.o_lit", "triple.sid", "triple.tag",
            "triple.o_lit_ix",
            "triple.o_lit_type", "triple.o_lit_lang", "triple.o_lit_n",
            "uls.uri AS s_uri",
            "ulp.uri AS p_uri", "ulo.uri AS o_uri")
            .Get().FirstOrDefault();
        return d == null ? null : GetUriTriple(d);
    }

    private static int FindTripleByValue(Triple triple, QueryFactory qf)
    {
        var query = qf.Query("triple")
            .Where("s_id", triple.SubjectId)
            .Where("p_id", triple.PredicateId)
            .Select("id");

        if (triple.ObjectId == 0) query.WhereNull("o_id");
        else query.Where("o_id", triple.ObjectId);

        if (triple.ObjectLiteral == null) query.WhereNull("o_lit");
        else
        {
            query.Where("o_lit", triple.ObjectLiteral)
                 .Where("o_lit_type", triple.LiteralType)
                 .Where("o_lit_lang", triple.LiteralLanguage);
        }

        if (triple.Sid == null) query.WhereNull("sid");
        else query.Where("sid", triple.Sid);

        if (triple.Tag == null) query.WhereNull("tag");
        else query.Where("tag", triple.Tag);

        var d = query.Get().FirstOrDefault();
        return d == null ? 0 : d.id;
    }

    private static void AddTriple(Triple triple, QueryFactory qf)
    {
        // do not insert if exactly the same triple already exists.
        // In this case, update the triple's ID to ensure it's valid
        int existingId = FindTripleByValue(triple, qf);
        if (existingId > 0)
        {
            triple.Id = existingId;
            return;
        }

        // else update/insert
        if (triple.Id > 0 &&
            qf.Query("triple").Where("id", triple.Id).Exists())
        {
            qf.Query("triple").Update(new
            {
                id = triple.Id,
                s_id = triple.SubjectId,
                p_id = triple.PredicateId,
                o_id = triple.ObjectId == 0 ? null : (int?)triple.ObjectId,
                o_lit = triple.ObjectLiteral,
                o_lit_ix = triple.ObjectLiteralIx,
                o_lit_type = triple.LiteralType,
                o_lit_lang = triple.LiteralLanguage,
                o_lit_n = triple.LiteralNumber,
                sid = triple.Sid,
                tag = triple.Tag,
            });
        }
        else
        {
            triple.Id = qf.Query("triple").InsertGetId<int>(new
            {
                s_id = triple.SubjectId,
                p_id = triple.PredicateId,
                o_id = triple.ObjectId == 0 ? null : (int?)triple.ObjectId,
                o_lit = triple.ObjectLiteral,
                o_lit_ix = triple.ObjectLiteralIx,
                o_lit_type = triple.LiteralType,
                o_lit_lang = triple.LiteralLanguage,
                o_lit_n = triple.LiteralNumber,
                sid = triple.Sid,
                tag = triple.Tag,
            });
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

        using QueryFactory qf = GetQueryFactory();
        AddTriple(triple, qf);
    }

    /// <summary>
    /// Bulk imports the specified triples. Note that it is assumed that
    /// all the triple's non-literal terms have a URI; if it is missing,
    /// the triple will not be imported. The URI is used to generate the
    /// corresponding numeric IDs used internally.
    /// </summary>
    /// <param name="triples">The triples.</param>
    public void ImportTriples(IEnumerable<UriTriple> triples)
    {
        if (triples is null) throw new ArgumentNullException(nameof(triples));

        using QueryFactory qf = GetQueryFactory();
        var trans = qf.Connection.BeginTransaction();
        try
        {
            foreach (UriTriple triple in triples)
            {
                if (triple.SubjectUri == null || triple.PredicateUri == null ||
                    (triple.ObjectLiteral == null && triple.ObjectUri == null))
                {
                    continue;
                }

                // add subject - the node is added if not present
                var t = AddUri(triple.SubjectUri, qf, trans);
                triple.SubjectId = t.Item1;
                if (t.Item2)
                {
                    AddNode(new Node
                    {
                        Id = t.Item1,
                        Label = triple.SubjectUri,
                        Sid = triple.Sid,
                    }, false, qf, trans);
                }

                // add predicate - the node is added if not present
                t = AddUri(triple.PredicateUri, qf, trans);
                triple.PredicateId = t.Item1;
                if (t.Item2)
                {
                    AddNode(new Node
                    {
                        Id = t.Item1,
                        Label = triple.PredicateUri,
                        Sid = triple.Sid,
                        Tag = Node.TAG_PROPERTY
                    }, false, qf, trans);
                }

                // add object if it's a node
                if (triple.ObjectUri != null)
                {
                    t = AddUri(triple.ObjectUri, qf, trans);
                    triple.ObjectId = t.Item1;
                    if (t.Item2)
                    {
                        AddNode(new Node
                        {
                            Id = t.Item1,
                            Label = triple.ObjectUri,
                            Sid = triple.Sid
                        }, false, qf, trans);
                    }
                }

                triple.Id = qf.Query("triple").InsertGetId<int>(new
                {
                    s_id = triple.SubjectId,
                    p_id = triple.PredicateId,
                    o_id = triple.ObjectId == 0 ? null : (int?)triple.ObjectId,
                    o_lit = triple.ObjectLiteral,
                    o_lit_ix = triple.ObjectLiteralIx,
                    o_lit_type = triple.LiteralType,
                    o_lit_lang = triple.LiteralLanguage,
                    o_lit_n = triple.LiteralNumber,
                    sid = triple.Sid,
                    tag = triple.Tag,
                }, trans);
            }
            trans.Commit();
        }
        catch (Exception)
        {
            trans.Rollback();
            throw;
        }
    }

    private void DeleteTriple(int id, QueryFactory qf)
    {
        // get the triple to delete as its deletion might affect
        // the classes assigned to its subject node
        var d = qf.Query("triple")
            .Where("id", id)
            .Select("s_id", "o_id")
            .Get().FirstOrDefault();

        // delete
        if (d != null)
        {
            qf.Query("triple").Where("id", id).Delete();

            // update classes if required
            if (d.o_id != null && d.o_id > 0)
            {
                var asIds = GetASubIds(qf);
                UpdateNodeClasses(d.s_id, asIds.Item1, asIds.Item2, qf);
            }
        }
    }

    /// <summary>
    /// Deletes the triple with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public void DeleteTriple(int id)
    {
        using QueryFactory qf = GetQueryFactory();
        DeleteTriple(id, qf);
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

        // something like:
        // select t.p_id, ul.uri, count(t.p_id) as cnt
        // from triple t
        // inner join uri_lookup ul on t.p_id = ul.id
        // group by t.p_id order by cnt desc, uri
        using QueryFactory qf = GetQueryFactory();
        var query = qf.Query("triple").Select("p_id");
        ApplyTripleFilter(filter, query);
        query.GroupBy("triple.p_id");

        // get count and ret if no result
        int total = query.Clone().Count<int>(new[] { "p_id" });
        if (total == 0)
        {
            return new DataPage<TripleGroup>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<TripleGroup>());
        }

        // complete query and get page
        query.Select("ul.uri")
             .SelectRaw("COUNT(p_id) AS cnt")
             .Join("uri_lookup AS ul", "p_id", "ul.id");
        foreach (char c in sort)
        {
            switch (c)
            {
                case 'c':
                    query.OrderBy("cnt");
                    break;
                case 'u':
                    query.OrderBy("uri");
                    break;
                case 'C':
                    query.OrderByDesc("cnt");
                    break;
                case 'U':
                    query.OrderByDesc("uri");
                    break;
            }
        }
        query.Skip(filter.GetSkipCount()).Limit(filter.PageSize);
        List<TripleGroup> groups = new(filter.PageSize);
        foreach (var d in query.Get())
        {
            groups.Add(new TripleGroup
            {
                PredicateId = d.p_id,
                PredicateUri = d.uri,
                Count = (int)d.cnt
            });
        }
        return new DataPage<TripleGroup>(filter.PageNumber, filter.PageSize,
            total, groups);
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
        if (thesaurus is null)
            throw new ArgumentNullException(nameof(thesaurus));

        // nothing to do for aliases
        if (thesaurus.TargetId != null) return;

        using QueryFactory qf = GetQueryFactory();
        IDbTransaction trans = qf.Connection.BeginTransaction();

        try
        {
            // ensure that we have rdfs:subClassOf
            Node? sub = GetNodeByUri("rdfs:subClassOf", qf);
            if (sub == null)
            {
                sub = new Node
                {
                    Id = AddUri("rdfs:subClassOf", qf),
                    Label = "subclass-of",
                    IsClass = true
                };
                AddNode(sub, true, qf);
            }

            // include root if requested
            Node? root = null;
            if (includeRoot)
            {
                int atIndex = thesaurus.Id.LastIndexOf('@');
                string id = atIndex > -1
                    ? thesaurus.Id[..atIndex]
                    : thesaurus.Id;
                string uri = string.IsNullOrEmpty(prefix)
                    ? id : prefix + id;

                root = new Node
                {
                    Id = AddUri(uri, qf),
                    IsClass = true,
                    Label = id,
                    SourceType = Node.SOURCE_THESAURUS,
                    Tag = "thesaurus",
                    Sid = thesaurus.Id
                };
                AddNode(root, true, qf);
            }

            Dictionary<string, int> ids = new();
            thesaurus.VisitByLevel(entry =>
            {
                string uri = string.IsNullOrEmpty(prefix)
                    ? entry.Id : prefix + entry.Id;
                Node node = new()
                {
                    Id = AddUri(uri, qf),
                    IsClass = true,
                    Label = entry.Id,
                    SourceType = Node.SOURCE_THESAURUS,
                    Tag = "thesaurus",
                    Sid = thesaurus.Id
                };
                AddNode(node, true, qf);
                ids[entry.Id] = node.Id;

                // triple
                if (entry.Parent != null)
                {
                    AddTriple(new Triple
                    {
                        SubjectId = node.Id,
                        PredicateId = sub.Id,
                        ObjectId = ids[entry.Parent.Id]
                    }, qf);
                }
                else if (root != null)
                {
                    AddTriple(new Triple
                    {
                        SubjectId = node.Id,
                        PredicateId = sub.Id,
                        ObjectId = root.Id
                    }, qf);
                }

                return true;
            });

            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }
    #endregion

    #region Node Classes
    private Tuple<int, int> GetASubIds(QueryFactory qf,
        IDbTransaction? trans = null)
    {
        // rdf:type and rdfs:subClassOf must exist
        Node? a = GetNodeByUri("rdf:type", qf, trans);
        if (a == null)
        {
            a = new Node
            {
                Id = AddUri("rdf:type", qf),
                Label = "is-a",
                Tag = "property"
            };
            AddNode(a, true, qf, trans, true);
        }

        Node? sub = GetNodeByUri("rdfs:subClassOf", qf, trans);
        if (sub == null)
        {
            sub = new Node
            {
                Id = AddUri("rdfs:subClassOf", qf),
                Label = "rdfs:subClassOf",
                Tag = "property"
            };
            AddNode(sub, true, qf, trans, true);
        }
        return Tuple.Create(a.Id, sub.Id);
    }

    private static void UpdateNodeClasses(int nodeId, int aId, int subId,
        QueryFactory qf, IDbTransaction? trans = null)
    {
        if (trans == null)
            qf.Statement($"CALL populate_node_class({nodeId},{aId},{subId});");
        else
            qf.Statement($"CALL populate_node_class({nodeId},{aId},{subId});",
                trans);
    }

    /// <summary>
    /// Adds the specified parameter to <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The name.</param>
    /// <param name="type">The type.</param>
    /// <param name="value">The optional value.</param>
    protected static void AddParameter(IDbCommand command, string name,
        DbType type, object? value = null)
    {
        IDbDataParameter p = command.CreateParameter();
        p.ParameterName = name;
        p.DbType = type;
        if (value != null) p.Value = value;
        command.Parameters.Add(p);
    }

    /// <summary>
    /// Updates the classes for all the nodes belonging to any class.
    /// </summary>
    /// <param name="cancel">The cancel.</param>
    /// <param name="progress">The progress.</param>
    public Task UpdateNodeClassesAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        using QueryFactory qf = GetQueryFactory();
        // rdf:type and rdfs:subClassOf must exist
        var asIds = GetASubIds(qf);

        // get total nodes to go
        int total = qf.Query("node").Where("is_class", false)
                      .Count<int>(new[] { "id" });

        IDbCommand cmd = qf.Connection.CreateCommand();
        cmd.CommandText = "SELECT node.id FROM node " +
            "WHERE node.is_class=0;";

        using (IDataReader reader = cmd.ExecuteReader())
        using (IDbConnection updaterConn = GetConnection())
        {
            updaterConn.Open();
            ProgressReport? report =
                progress != null ? new ProgressReport() : null;
            int oldPercent = 0;

            IDbCommand updCmd = updaterConn.CreateCommand();
            // we need another connection to update while reading
            updCmd.Connection = updaterConn;
            updCmd.CommandType = CommandType.StoredProcedure;
            updCmd.CommandText = "populate_node_class";
            AddParameter(updCmd, "instance_id", DbType.Int32);
            AddParameter(updCmd, "a_id", DbType.Int32, asIds.Item1);
            AddParameter(updCmd, "sub_id", DbType.Int32, asIds.Item2);

            while (reader.Read())
            {
                ((DbParameter)updCmd.Parameters[0]!).Value
                    = reader.GetInt32(0);
                updCmd.ExecuteNonQuery();

                if (report != null && ++report.Count % 10 == 0)
                {
                    report.Percent = report.Count * 100 / total;
                    if (report.Percent != oldPercent)
                    {
                        progress!.Report(report);
                        oldPercent = report.Percent;
                    }
                }
                if (cancel.IsCancellationRequested)
                    return Task.CompletedTask;
            }

            if (report != null)
            {
                report.Percent = 100;
                report.Count = total;
                progress!.Report(report);
            }
        }
        return Task.CompletedTask;
    }
    #endregion

    #region Graph
    /// <summary>
    /// Gets the set of graph's nodes and triples whose SID starts with
    /// the specified GUID. This identifies all the nodes and triples
    /// generated from a single source item or part.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">sourceId</exception>
    public GraphSet GetGraphSet(string sourceId)
    {
        if (sourceId is null)
            throw new ArgumentNullException(nameof(sourceId));

        using QueryFactory qf = GetQueryFactory();
        // nodes
        Query query = qf.Query("node")
          .Join("uri_lookup AS ul", "node.id", "ul.id")
          .WhereLike("sid", sourceId + "%")
          .Select("node.id", "node.is_class", "node.tag", "node.label",
            "node.source_type", "node.sid", "ul.uri");

        List<UriNode> nodes = new();
        foreach (var d in query.Get())
        {
            nodes.Add(new UriNode
            {
                Id = d.id,
                IsClass = Convert.ToBoolean(d.is_class),
                Tag = d.tag,
                Label = d.label,
                SourceType = d.source_type,
                Sid = d.sid,
                Uri = d.uri
            });
        }

        // triples
        DataPage<UriTriple> page = GetTriples(new TripleFilter
        {
            PageNumber = 1,
            PageSize = 0,
            Sid = sourceId,
            IsSidPrefix = true
        });

        return new GraphSet(nodes, page.Items);
    }

    /// <summary>
    /// Deletes the set of graph's nodes and triples whose SID starts with
    /// the specified GUID. This identifies all the nodes and triples
    /// generated from a single source item or part.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <exception cref="ArgumentNullException">sourceId</exception>
    public void DeleteGraphSet(string sourceId)
    {
        if (sourceId == null) throw new ArgumentNullException(nameof(sourceId));

        using QueryFactory qf = GetQueryFactory();
        IDbTransaction trans = qf.Connection.BeginTransaction();

        try
        {
            qf.Query("triple").WhereLike("sid", sourceId + "%").Delete();
            qf.Query("node").WhereLike("sid", sourceId + "%").Delete();

            trans.Commit();
        }
        catch (Exception)
        {
            trans.Rollback();
            throw;
        }
    }

    private void UpdateGraph(string? sourceId, IList<UriNode> nodes,
        IList<UriTriple> triples, QueryFactory qf)
    {
        // corner case: sourceId = null/empty:
        // this happens only for nodes generated as the objects of a
        // generated triple, and in this case we must only ensure that
        // such nodes exist, without updating them.
        if (string.IsNullOrEmpty(sourceId))
        {
            foreach (UriNode node in nodes) AddNode(node, true);
            return;
        }

        GraphSet oldSet = GetGraphSet(sourceId);

        // compare sets
        CrudGrouper<UriNode> nodeGrouper = new();
        nodeGrouper.Group(nodes, oldSet.Nodes,
            (UriNode a, UriNode b) => a.Id == b.Id);

        CrudGrouper<UriTriple> tripleGrouper = new();
        tripleGrouper.Group(triples, oldSet.Triples,
            (UriTriple a, UriTriple b) =>
            {
                return a.SubjectId == b.SubjectId &&
                    a.PredicateId == b.PredicateId &&
                    a.ObjectId == b.ObjectId &&
                    a.ObjectLiteral == b.ObjectLiteral &&
                    a.Sid == b.Sid;
            });

        // filter deleted nodes to ensure that no property/class gets deleted
        nodeGrouper.FilterDeleted(n => !n.IsClass && n.Tag != Node.TAG_PROPERTY);

        // nodes
        foreach (UriNode node in nodeGrouper.Deleted)
            DeleteNode(node.Id, qf);
        foreach (UriNode node in nodeGrouper.Added)
        {
            node.Id = AddUri(node.Uri!, qf);
            AddNode(node, true, qf);
        }
        foreach (UriNode node in nodeGrouper.Updated)
            AddNode(node, node.Sid == null, qf);

        // triples
        foreach (UriTriple triple in tripleGrouper.Deleted)
            DeleteTriple(triple.Id, qf);
        foreach (UriTriple triple in tripleGrouper.Added)
            AddTriple(triple, qf);
        foreach (UriTriple triple in tripleGrouper.Updated)
            AddTriple(triple, qf);
    }

    /// <summary>
    /// Updates the graph with the specified nodes and triples.
    /// </summary>
    /// <param name="set">The new set of nodes and triples.</param>
    /// <exception cref="ArgumentNullException">set</exception>
    public void UpdateGraph(GraphSet set)
    {
        if (set == null) throw new ArgumentNullException(nameof(set));

        // ensure to save each new node's URI, thus getting its corresponding ID
        HashSet<string> nodeUris = new();
        foreach (UriNode node in set.Nodes)
        {
            nodeUris.Add(node.Uri!);
            if (node.Id == 0) node.Id = AddUri(node.Uri!);
        }

        foreach (UriTriple triple in set.Triples)
        {
            if (triple.SubjectId == 0)
            {
                triple.SubjectId = AddUri(triple.SubjectUri!);
                if (!nodeUris.Contains(triple.SubjectUri!))
                {
                    // add node implicit in triple
                    set.Nodes.Add(new UriNode
                    {
                        Id = triple.SubjectId,
                        Uri = triple.SubjectUri,
                        Label = triple.SubjectUri,
                        Sid = triple.Sid,
                        SourceType = Node.SOURCE_IMPLICIT
                    });
                }
            }

            if (triple.PredicateId== 0)
            {
                triple.PredicateId = AddUri(triple.PredicateUri!);
                if (!nodeUris.Contains(triple.PredicateUri!))
                {
                    // add node implicit in triple,
                    // but this shold not happen for predicates
                    set.Nodes.Add(new UriNode
                    {
                        Id = triple.PredicateId,
                        Uri = triple.PredicateUri,
                        Label = triple.PredicateUri,
                        Sid = triple.Sid,
                        SourceType = Node.SOURCE_IMPLICIT
                    });
                }
            }

            if (triple.ObjectId == 0 && !string.IsNullOrEmpty(triple.ObjectUri))
            {
                triple.ObjectId = AddUri(triple.ObjectUri);
                if (!nodeUris.Contains(triple.ObjectUri!))
                {
                    // add node implicit in triple
                    set.Nodes.Add(new UriNode
                    {
                        Id = triple.ObjectId,
                        Uri = triple.ObjectUri,
                        Label = triple.ObjectUri,
                        Sid = triple.Sid,
                        SourceType = Node.SOURCE_IMPLICIT
                    });
                }
            }
        }

        // get nodes and triples grouped by their SID's GUID
        var nodeGroups = set.GetNodesByGuid();
        var tripleGroups = set.GetTriplesByGuid();

        using QueryFactory qf = GetQueryFactory();
        IDbTransaction trans = qf.Connection.BeginTransaction();

        try
        {
            // order by key so that empty (=null SID) keys come before
            foreach (string key in nodeGroups.Keys.OrderBy(s => s))
            {
                UpdateGraph(key,
                    nodeGroups[key],
                    tripleGroups.ContainsKey(key)
                        ? tripleGroups[key]
                        : Array.Empty<UriTriple>(), qf);
            }
            trans.Commit();
        }
        catch (Exception)
        {
            trans.Rollback();
            throw;
        }
    }
    #endregion
}
