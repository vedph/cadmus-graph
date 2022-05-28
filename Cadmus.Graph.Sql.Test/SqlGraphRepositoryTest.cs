using Fusi.DbManager;
using Fusi.Tools.Data;
using System;
using Xunit;

namespace Cadmus.Graph.Sql.Test
{
    /// <summary>
    /// Base class for SQL-based graph repositories tests.
    /// </summary>
    public abstract class SqlGraphRepositoryTest
    {
        private const string DB_NAME = "cadmus-index-test";

        /// <summary>
        /// Gets the connection string template.
        /// </summary>
        public abstract string ConnectionStringTemplate { get; }

        /// <summary>
        /// Gets the database manager.
        /// </summary>
        public abstract IDbManager DbManager { get; }

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        public string ConnectionString =>
            string.Format(ConnectionStringTemplate, DB_NAME);

        /// <summary>
        /// Gets the database schema DDL SQL.
        /// </summary>
        /// <returns>SQL code.</returns>
        protected abstract string GetSchema();

        /// <summary>
        /// Gets the repository.
        /// </summary>
        /// <returns>Repository.</returns>
        protected abstract IGraphRepository GetRepository();

        /// <summary>
        /// Resets the database.
        /// </summary>
        protected void Reset()
        {
            if (DbManager.Exists(DB_NAME))
            {
                DbManager.ClearDatabase(DB_NAME);
            }
            else
            {
                DbManager.CreateDatabase(DB_NAME, GetSchema(), null);
            }
        }

        #region Namespace
        private static void AddNamespaces(int count, IGraphRepository repository)
        {
            for (int n = 1; n <= count; n++)
                repository.AddNamespace("p" + n, $"http://www.ns{n}.org");
        }

        protected void DoGetNamespaces_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(3, repository);

            DataPage<NamespaceEntry> page = repository.GetNamespaces(
                new NamespaceFilter
                {
                    PageNumber = 1,
                    PageSize = 10,
                });

            Assert.Equal(3, page.Total);
            Assert.Equal(3, page.Items.Count);
        }

        protected void DoGetNamespaces_Prefix_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(3, repository);

            DataPage<NamespaceEntry> page = repository.GetNamespaces(
                new NamespaceFilter
                {
                    PageNumber = 1,
                    PageSize = 10,
                    Prefix = "p1"
                });

            Assert.Equal(1, page.Total);
            Assert.Single(page.Items);
        }

        protected void DoGetNamespaces_Uri_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(3, repository);

            DataPage<NamespaceEntry> page = repository.GetNamespaces(
                new NamespaceFilter
                {
                    PageNumber = 1,
                    PageSize = 10,
                    Uri = "ns2"
                });

            Assert.Equal(1, page.Total);
            Assert.Single(page.Items);
        }

        protected void DoLookupNamespace_NotExisting_Null()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(2, repository);

            string? uri = repository.LookupNamespace("not-existing");

            Assert.Null(uri);
        }

        protected void DoLookupNamespace_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(2, repository);

            string? uri = repository.LookupNamespace("p1");

            Assert.Equal("http://www.ns1.org", uri);
        }

        protected void DoDeleteNamespaceByPrefix_NotExisting_Nope()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(2, repository);

            repository.DeleteNamespaceByPrefix("not-existing");

            Assert.Equal(2, repository.GetNamespaces(new NamespaceFilter()).Total);
        }

        protected void DoDeleteNamespaceByPrefix_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(2, repository);

            repository.DeleteNamespaceByPrefix("p2");

            Assert.NotNull(repository.LookupNamespace("p1"));
            Assert.Null(repository.LookupNamespace("p2"));
        }

        protected void DoDeleteNamespaceByUri_NotExisting_Nope()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(2, repository);

            repository.DeleteNamespaceByUri("not-existing");

            Assert.Equal(2, repository.GetNamespaces(new NamespaceFilter()).Total);
        }

        protected void DoDeleteNamespaceByUri_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNamespaces(2, repository);
            repository.AddNamespace("p3", "http://www.ns1.org");

            repository.DeleteNamespaceByUri("http://www.ns1.org");

            Assert.Null(repository.LookupNamespace("p1"));
            Assert.Null(repository.LookupNamespace("p3"));
            Assert.NotNull(repository.LookupNamespace("p2"));
        }
        #endregion

        #region Uid
        [Fact]
        protected void DoAddUid_NoClash_AddedNoSuffix()
        {
            Reset();
            IGraphRepository repository = GetRepository();

            string uid = repository.AddUid("x:persons/john_doe",
                Guid.NewGuid().ToString());

            Assert.Equal("x:persons/john_doe", uid);
        }

        [Fact]
        protected void DoAddUid_Clash_AddedWithSuffix()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            string sid = Guid.NewGuid().ToString();
            string uid1 = repository.AddUid("x:persons/john_doe", sid);

            string uid2 = repository.AddUid("x:persons/john_doe",
                Guid.NewGuid().ToString());

            Assert.NotEqual(uid1, uid2);
        }

        [Fact]
        protected void DoAddUid_ClashButSameSid_ReusedWithSuffix()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            string sid = Guid.NewGuid().ToString();
            string uid1 = repository.AddUid("x:persons/john_doe", sid);

            string uid2 = repository.AddUid("x:persons/john_doe", sid);

            Assert.Equal(uid1, uid2);
        }
        #endregion

        #region Uri
        [Fact]
        protected void DoAddUri_NotExisting_Added()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            const string uri = "http://www.sample.com";

            int id = repository.AddUri(uri);

            string? uri2 = repository.LookupUri(id);
            Assert.Equal(uri, uri2);
        }

        [Fact]
        protected void DoAddUri_Existing_Nope()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            const string uri = "http://www.sample.com";
            int id = repository.AddUri(uri);

            int id2 = repository.AddUri(uri);

            Assert.Equal(id, id2);
        }
        #endregion

        #region Node
        private static void AddNodes(int count, IGraphRepository repository,
            bool tag = false)
        {
            const string itemId = "e0cd9166-d005-404d-8f18-65be1f17b48f";
            const string partId = "f321f320-26da-4164-890b-e3974e9272ba";

            for (int i = 0; i < count; i++)
            {
                string uid = $"x:node{i + 1}";
                int id = repository.AddUri(uid);

                Node node = new()
                {
                    Id = id,
                    Label = $"Node {i + 1:00}",
                    SourceType = i == 0
                        ? NodeMapping.SOURCE_TYPE_ITEM
                        : NodeMapping.SOURCE_TYPE_PART,
                    Sid = i == 0 ? itemId : partId,
                    Tag = tag ? ((i + 1) % 2 == 0 ? "even" : "odd") : null
                };
                repository.AddNode(node);
            }
        }

        protected void DoGetNodes_NoFilter_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(10, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter());

            Assert.Equal(10 + 2, page.Total);
            Assert.Equal(10 + 2, page.Items.Count);
        }

        protected void DoGetNodes_NoFilterPage2_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(10, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                PageNumber = 2,
                PageSize = 5
            });

            Assert.Equal(10 + 2, page.Total);
            Assert.Equal(5, page.Items.Count);
            Assert.Equal("Node 05", page.Items[0].Label);
            Assert.Equal("Node 09", page.Items[4].Label);
        }

        protected void DoGetNodes_ByLabel_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(10, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                Label = "05"
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
            Assert.Equal("Node 05", page.Items[0].Label);
        }

        protected void DoGetNodes_ByLabelAndClass_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(10, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                Label = "05",
                IsClass = true
            });

            Assert.Equal(0, page.Total);
            Assert.Empty(page.Items);
        }

        protected void DoGetNodes_BySourceType_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(10, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                SourceType = NodeMapping.SOURCE_TYPE_ITEM
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
            Assert.Equal("Node 01", page.Items[0].Label);
        }

        protected void DoGetNodes_BySidExact_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(10, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                Sid = "e0cd9166-d005-404d-8f18-65be1f17b48f"
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
            Assert.Equal("Node 01", page.Items[0].Label);
        }

        protected void DoGetNodes_BySidPrefix_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(10, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                Sid = "f321f320-26da-4164-890b-e3974e9272ba/",
                IsSidPrefix = true
            });

            Assert.Equal(9, page.Total);
            Assert.Equal(9, page.Items.Count);
            Assert.Equal("Node 02", page.Items[0].Label);
        }

        protected void DoGetNodes_ByNoTag_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(3, repository);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                Tag = ""
            });

            Assert.Equal(3, page.Total);
            Assert.Equal(3, page.Items.Count);
        }

        protected void DoGetNodes_ByTag_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddNodes(3, repository, true);

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                Tag = "odd"
            });

            Assert.Equal(2, page.Total);
            Assert.Equal(2, page.Items.Count);
        }

        protected void DoGetNodes_ByLinkedNode_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            // nodes
            Node argos = new()
            {
                Id = repository.AddUri("x:dogs/argos"),
                Label = "Argos"
            };
            repository.AddNode(argos);
            Node a = new()
            {
                Id = repository.AddUri("rdf:type"),
                Label = "a"
            };
            repository.AddNode(a);
            Node animal = new()
            {
                Id = repository.AddUri("x:animal"),
                Label = "animal",
                IsClass = true
            };
            repository.AddNode(animal);
            // triple
            repository.AddTriple(new Triple
            {
                SubjectId = argos.Id,
                PredicateId = a.Id,
                ObjectId = animal.Id
            });

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                LinkedNodeId = animal.Id
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
            Assert.Equal("Argos", page.Items[0].Label);
        }

        protected void DoGetNodes_ByLinkedNodeWithMatchingRole_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            // nodes
            Node argos = new()
            {
                Id = repository.AddUri("x:dogs/argos"),
                Label = "Argos"
            };
            repository.AddNode(argos);
            Node a = new()
            {
                Id = repository.AddUri("rdf:type"),
                Label = "a"
            };
            repository.AddNode(a);
            Node animal = new()
            {
                Id = repository.AddUri("x:animal"),
                Label = "animal",
                IsClass = true
            };
            repository.AddNode(animal);
            // triple
            repository.AddTriple(new Triple
            {
                SubjectId = argos.Id,
                PredicateId = a.Id,
                ObjectId = animal.Id
            });

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                LinkedNodeId = animal.Id,
                LinkedNodeRole = 'O'
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
            Assert.Equal("Argos", page.Items[0].Label);
        }

        protected void DoGetNodes_ByLinkedNodeWithNonMatchingRole_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            // nodes
            Node argos = new()
            {
                Id = repository.AddUri("x:dogs/argos"),
                Label = "Argos"
            };
            repository.AddNode(argos);
            Node a = new()
            {
                Id = repository.AddUri("rdf:type"),
                Label = "a"
            };
            repository.AddNode(a);
            Node animal = new()
            {
                Id = repository.AddUri("x:animal"),
                Label = "animal",
                IsClass = true
            };
            repository.AddNode(animal);
            // triple
            repository.AddTriple(new Triple
            {
                SubjectId = argos.Id,
                PredicateId = a.Id,
                ObjectId = animal.Id
            });

            DataPage<UriNode> page = repository.GetNodes(new NodeFilter
            {
                LinkedNodeId = animal.Id,
                LinkedNodeRole = 'S'
            });

            Assert.Equal(0, page.Total);
            Assert.Empty(page.Items);
        }

        protected void DoDeleteNode_NotExisting_Nope()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            Node argos = new()
            {
                Id = repository.AddUri("x:dogs/argos"),
                Label = "Argos"
            };
            repository.AddNode(argos);

            repository.DeleteNode(argos.Id + 10);

            Assert.NotNull(repository.GetNode(argos.Id));
        }

        protected void DoDeleteNode_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            Node argos = new()
            {
                Id = repository.AddUri("x:dogs/argos"),
                Label = "Argos"
            };
            repository.AddNode(argos);

            repository.DeleteNode(argos.Id);

            Assert.Null(repository.GetNode(argos.Id));
        }
        #endregion

        #region Property
        private static void AddProperties(IGraphRepository repository)
        {
            Node comment = new()
            {
                Id = repository.AddUri("rdfs:comment"),
                Label = "comment"
            };
            repository.AddNode(comment);
            Node date = new()
            {
                Id = repository.AddUri("x:date"),
                Label = "Date"
            };
            repository.AddNode(date);

            repository.AddProperty(new Property
            {
                Id = comment.Id,
                DataType = "xsd:string",
                Description = "A comment.",
                LiteralEditor = "qed.md"
            });
            repository.AddProperty(new Property
            {
                Id = date.Id,
                DataType = "xs:date",
                Description = "A year-based date.",
                LiteralEditor = "qed.date"
            });
        }

        protected void DoGetProperties_NoFilter_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            var page = repository.GetProperties(new PropertyFilter());

            Assert.Equal(2, page.Total);
            Assert.Equal(2, page.Items.Count);
        }

        protected void DoGetProperties_ByUid_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            var page = repository.GetProperties(new PropertyFilter
            {
                Uid = "rdfs"
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
        }

        protected void DoGetProperties_ByDataType_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            var page = repository.GetProperties(new PropertyFilter
            {
                DataType = "xsd:string"
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
        }

        protected void DoGetProperties_ByLiteralEditor_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            var page = repository.GetProperties(new PropertyFilter
            {
                LiteralEditor = "qed.date"
            });

            Assert.Equal(1, page.Total);
            Assert.Equal(1, page.Items.Count);
        }

        protected void DoGetProperty_NotExisting_Null()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            Assert.Null(repository.GetProperty(123));
        }

        protected void DoGetProperty_Existing_NotNull()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            Assert.NotNull(repository.GetProperty(1));
        }

        protected void DoGetPropertyByUri_NotExisting_Null()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            Assert.Null(repository.GetPropertyByUri("not-existing"));
        }

        protected void DoGetPropertyByUri_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            Assert.NotNull(repository.GetPropertyByUri("rdfs:comment"));
        }

        protected void DoDeleteProperty_NotExisting_Nope()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            repository.DeleteProperty(123);

            Assert.Equal(2, repository.GetProperties(new PropertyFilter()).Total);
        }

        protected void DoDeleteProperty_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            AddProperties(repository);

            repository.DeleteProperty(1);

            Assert.Equal(1, repository.GetProperties(new PropertyFilter()).Total);
        }
        #endregion

    }
}
