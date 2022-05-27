using Cadmus.Index.Sql;
using Fusi.Tools;
using Fusi.Tools.Config;
using Fusi.Tools.Data;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Graph.Sql
{
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
        public string AddUid(string uid, string sid)
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
        private static void ApplyNodeFilter(NodeFilter filter, Query query)
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
            if (filter.SourceType != null)
                query.Where("source_type", filter.SourceType);

            // sid
            if (!string.IsNullOrEmpty(filter.Sid))
            {
                if (filter.IsSidPrefix) query.WhereLike("sid", filter.Sid + "%");
                else query.Where("sid", filter.Sid);
            }

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

            // class IDs
            if (filter.ClassIds?.Count > 0)
            {
                query.Join("node_class AS nc", "node.id", "nc.node_id")
                     .WhereIn("nc.class_id", filter.ClassIds);
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
            return new DataPage<UriNode>(filter.PageNumber, filter.PageSize,
                total, nodes);
        }

        /// <summary>
        /// Gets the node with the specified ID.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>The node or null if not found.</returns>
        public UriNode? GetNode(int id)
        {
            using QueryFactory qf = GetQueryFactory();
            var d = qf.Query("node")
              .Join("uri_lookup AS ul", "node.id", "ul.id")
              .Where("node.id", id)
              .Select("node.is_class", "node.tag", "node.label",
                      "node.source_type", "node.sid", "ul.uri")
              .Get().FirstOrDefault();
            return d == null
                ? null
                : new UriNode
                {
                    Id = id,
                    IsClass = Convert.ToBoolean(d.is_class),
                    Tag = d.tag,
                    Label = d.label,
                    SourceType = d.source_type,
                    Sid = d.sid,
                    Uri = d.uri
                };
        }

        private static UriNode? GetNodeByUri(string uri, QueryFactory qf)
        {
            var d = qf.Query("node")
              .Join("uri_lookup AS ul", "node.id", "ul.id")
              .Where("uri", uri)
              .Select("node.id", "node.is_class", "node.tag", "node.label",
                      "node.source_type", "node.sid")
              .Get().FirstOrDefault();
            return d == null
                ? null
                : new UriNode
                {
                    Id = d.id,
                    IsClass = Convert.ToBoolean(d.is_class),
                    Tag = d.tag,
                    Label = d.label,
                    SourceType = d.source_type,
                    Sid = d.sid,
                    Uri = uri
                };
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

        private void AddNode(Node node, bool noUpdate, QueryFactory qf)
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
            if (qf.Query("node").Where("id", node.Id).Exists())
            {
                if (noUpdate) return;
                qf.Query("node").Where("id", node.Id).Update(d);
            }
            else
            {
                qf.Query("node").Insert(d);
            }

            var asIds = GetASubIds(qf);
            UpdateNodeClasses(node.Id, asIds.Item1, asIds.Item2, qf);
        }

        /// <summary>
        /// Adds or updates the specified node.
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
        #endregion

        #region Property
        private static void ApplyFilter(PropertyFilter filter, Query query)
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
            ApplyFilter(filter, query);

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

            if (filter.SourceTypes?.Count > 0)
                query.WhereIn("source_type", filter.SourceTypes);

            if (!string.IsNullOrEmpty(filter.Name))
                query.WhereLike("name", "%" + filter.Name + "%");

            if (!string.IsNullOrEmpty(filter.Facet))
                query.Where("facet", filter.Facet);

            if (!string.IsNullOrEmpty(filter.Group))
            {
                query.WhereRaw(SqlHelper.BuildRegexMatch("group",
                    SqlHelper.SqlEncode(filter.Group, false, true, false)));
            }

            if (filter.Flags > 0)
                query.WhereRaw($"(flags & {filter.Flags})={filter.Flags}");

            if (!string.IsNullOrEmpty(filter.Title))
                query.Where("title", filter.Title);

            if (!string.IsNullOrEmpty(filter.PartType))
                query.Where("part_type", filter.PartType);

            if (!string.IsNullOrEmpty(filter.PartRole))
                query.Where("part_role", filter.PartRole);
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

        /// <summary>
        /// Gets the specified page of node mappings.
        /// </summary>
        /// <param name="filter">The filter. Set page size=0 to get all
        /// the mappings at once.</param>
        /// <param name="descendants">True to populate all the mappings with their
        /// descendants.
        /// </param>
        /// <returns>The page.</returns>
        /// <exception cref="ArgumentNullException">filter</exception>
        public DataPage<NodeMapping> GetMappings(NodeMappingFilter filter,
            bool descendants)
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
                var mapping = DataToMapping(d);
                PopulateMappingOutput(mapping, qf);
                if (descendants) PopulateMappingDescendants(mapping, qf);
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
            PopulateMappingOutput(mapping, qf);
            PopulateMappingDescendants(mapping, qf);

            return mapping;
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
            }

            // add its output
            if (mapping.Output != null) AddMappingOutput(mapping, qf, trans);

            // add its children
            foreach (NodeMapping child in mapping.Children)
                AddMapping(child, qf, trans);
        }

        /// <summary>
        /// Adds the specified node mapping with all its descendants.
        /// When <paramref name="mapping"/> has ID=0 (=new mapping), its
        /// <see cref="NodeMapping.Id"/> property gets updated by this method
        /// after insertion.
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <exception cref="ArgumentNullException">mapping</exception>
        public void AddMapping(NodeMapping mapping)
        {
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));

            using QueryFactory qf = GetQueryFactory();
            IDbTransaction trans = qf.Connection.BeginTransaction();

            try
            {
                AddMapping(mapping, qf, trans);
                trans.Commit();
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
        #endregion

        #region Triples
        private void ApplyFilter(TripleFilter filter, Query query)
        {
            if (filter.SubjectId > 0)
                query.Where("s_id", filter.SubjectId);

            if (filter.PredicateId > 0)
                query.Where("p_id", filter.PredicateId);

            if (filter.ObjectId > 0)
                query.Where("o_id", filter.ObjectId);

            if (!string.IsNullOrEmpty(filter.ObjectLiteral))
            {
                query.WhereRaw(SqlHelper.BuildRegexMatch("o_lit",
                    SqlHelper.SqlEncode(filter.ObjectLiteral, false, true, false)));
            }

            // sid
            if (!string.IsNullOrEmpty(filter.Sid))
            {
                if (filter.IsSidPrefix) query.WhereLike("sid", filter.Sid + "%");
                else query.Where("sid", filter.Sid);
            }

            if (filter.Tag != null)
            {
                if (filter.Tag.Length == 0) query.WhereNull("tag");
                else query.Where("tag", filter.Tag);
            }
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
            ApplyFilter(filter, query);

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
                "uls.uri AS s_uri", "ulp.uri AS p_uri", "ulo.uri AS o_uri")
                 .OrderBy("s_uri", "p_uri", "triple.id")
                 .Skip(filter.GetSkipCount());
            if (filter.PageSize > 0) query.Limit(filter.PageSize);

            List<UriTriple> triples = new();
            foreach (var d in query.Get())
            {
                triples.Add(new UriTriple
                {
                    Id = d.id,
                    SubjectId = d.s_id,
                    PredicateId = d.p_id,
                    ObjectId = d.o_id ?? 0,
                    ObjectLiteral = d.o_lit,
                    Sid = d.sid,
                    Tag = d.tag,
                    SubjectUri = d.s_uri,
                    PredicateUri = d.p_uri,
                    ObjectUri = d.o_uri
                });
            }
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
                "triple.o_lit", "triple.sid", "triple.tag", "uls.uri AS s_uri",
                "ulp.uri AS p_uri", "ulo.uri AS o_uri")
                .Get().FirstOrDefault();
            return d == null ? null : new UriTriple
            {
                Id = id,
                SubjectId = d.s_id,
                PredicateId = d.p_id,
                ObjectId = d.o_id ?? 0,
                ObjectLiteral = d.o_lit,
                Sid = d.sid,
                Tag = d.tag,
                SubjectUri = d.s_uri,
                PredicateUri = d.p_uri,
                ObjectUri = d.o_uri
            };
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
            else query.Where("o_lit", triple.ObjectLiteral);

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
        #endregion

        #region Node Classes
        private Tuple<int, int> GetASubIds(QueryFactory qf)
        {
            // rdf:type and rdfs:subClassOf must exist
            Node? a = GetNodeByUri("rdf:type", qf);
            if (a == null)
            {
                AddNode(a = new Node
                {
                    Id = AddUri("rdf:type", qf),
                    Label = "is-a",
                    Tag = "property"
                }, true, qf);
            }

            Node? sub = GetNodeByUri("rdfs:subClassOf", qf);
            if (sub == null)
            {
                AddNode(sub = new Node
                {
                    Id = AddUri("rdfs:subClassOf", qf),
                    Label = "rdfs:subClassOf",
                    Tag = "property"
                }, true, qf);
            }
            return Tuple.Create(a.Id, sub.Id);
        }

        private static void UpdateNodeClasses(int nodeId, int aId, int subId,
            QueryFactory qf) =>
            qf.Statement($"CALL populate_node_class({nodeId},{aId},{subId});");

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

        private void UpdateGraph(string sourceId, IList<UriNode> nodes,
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
                AddNode(node, true, qf);
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
}
