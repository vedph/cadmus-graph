using Cadmus.Index.Sql;
using Fusi.DbManager;
using Fusi.DbManager.MySql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Cadmus.Graph.MySql.Test
{
    [Collection(nameof(NonParallelResourceCollection))]
    public sealed class GraphUpdaterTest
    {
        private const string CST = "Server=localhost;Database={0};Uid=root;Pwd=mysql;";
        private const string DB_NAME = "cadmus-index-test";
        static private readonly string CS = string.Format(CST, DB_NAME);
        private const string PART_ID = "bdd152f1-2ae2-4189-8a4a-e3d68c6a9d7e";

        private static void Reset()
        {
            IDbManager manager = new MySqlDbManager(CST);
            if (manager.Exists(DB_NAME))
            {
                manager.ClearDatabase(DB_NAME);
            }
            else
            {
                manager.CreateDatabase(DB_NAME,
                    MySqlGraphRepository.GetSchema(), null);
            }
        }

        private static IGraphRepository GetRepository()
        {
            MySqlGraphRepository repository = new MySqlGraphRepository();
            repository.Configure(new SqlOptions
            {
                ConnectionString = CS
            });
            return repository;
        }

        private Stream GetResourceStream(string name)
        {
            return GetType().Assembly.GetManifestResourceStream(
                "Cadmus.Graph.MySql.Test.Assets." + name)!;
        }

        private string LoadResourceText(string name)
        {
            using StreamReader reader = new(GetResourceStream(name),
                Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static void RemoveFromGraph(GraphSet set, string sidPrefix)
        {
            for (int i = set.Nodes.Count - 1; i > -1; i--)
            {
                if (set.Nodes[i].Sid?.StartsWith(
                    sidPrefix, StringComparison.Ordinal) == true)
                {
                    set.Nodes.RemoveAt(i);
                }
            }

            for (int i = set.Triples.Count - 1; i > -1; i--)
            {
                if (set.Triples[i].Sid?.StartsWith(
                    sidPrefix, StringComparison.Ordinal) == true)
                {
                    set.Triples.RemoveAt(i);
                }
            }
        }

        private GraphSet BuildSampleGraph()
        {
            // load events data
            string data = LoadResourceText("Events.json");

            // load mappings
            IGraphPresetReader reader = new JsonGraphPresetReader();
            IList<NodeMapping> mappings =
                reader.LoadMappings(GetResourceStream("Mappings.json"));

            // setup mapper
            INodeMapper mapper = new JsonNodeMapper();
            // mock metadata from item
            mapper.Data["item-id"] = Guid.NewGuid().ToString();
            mapper.Data["item-uri"] = "x:guys/francesco_petrarca";
            mapper.Data["item-label"] = "Petrarch";
            mapper.Data["group-id"] = "group";
            mapper.Data["facet-id"] = "facet";
            mapper.Data["flags"] = "3";
            // mock metada from part
            mapper.Data["part-id"] = PART_ID;

            // run mappings
            GraphSet set = new();
            foreach (NodeMapping mapping in mappings)
            {
                mapper.Map(data, mapping, set);
            }
            return set;
        }

        [Fact]
        public void Update_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            GraphSet set = BuildSampleGraph();

            // 1) store initial set
            repository.UpdateGraph(set);

            var nodePage = repository.GetNodes(new NodeFilter());
            Assert.Equal(5 + 3, nodePage.Total);

            var triplePage = repository.GetTriples(new TripleFilter());
            Assert.Equal(9 + 7, triplePage.Total);

            // 2) user-edits: we emulate them by directly changing the graph
            // (in real world, we would regenerate the full graph from a changed
            // data source, here an events part)
            // - petrarca a person (added)
            UriNode petrarca = repository.GetNodeByUri("x:francesco_petrarca")!;
            Assert.NotNull(petrarca);
            set.AddTriples(new[]{
                new UriTriple
                {
                    SubjectUri = "x:guys/francesco_petrarca",
                    PredicateUri = "rdf:type",
                    ObjectUri = "foaf:Person"
                }
            });

            // - birth date 1304 AD @it (edited)
            UriTriple? date = set.Triples.FirstOrDefault(
                t => t.SubjectUri == "x:timespans/ts"
                && t.PredicateUri == "crm:p87_is_identified_by");
            Assert.NotNull(date);
            date!.ObjectLiteral = "\"1304 AD\"@it";

            // - remove death (deleted)
            string deathPrefix = $"{PART_ID}/death";
            RemoveFromGraph(set, deathPrefix);

            // update
            repository.UpdateGraph(set);

            nodePage = repository.GetNodes(new NodeFilter());
            Assert.Equal(5, nodePage.Total);

            triplePage = repository.GetTriples(new TripleFilter());
            Assert.Equal(9, triplePage.Total);
        }

        [Fact]
        public void Delete_NotExisting_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();

            repository.DeleteGraphSet("not-existing");
            Assert.True(true);
        }

        [Fact]
        public void Delete_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();
            GraphSet set = BuildSampleGraph();
            repository.UpdateGraph(set);

            repository.DeleteGraphSet($"{PART_ID}/death");

            var nodePage = repository.GetNodes(new NodeFilter());
            Assert.Equal(5 + 3, nodePage.Total);

            var triplePage = repository.GetTriples(new TripleFilter());
            Assert.Equal(9 + 7, triplePage.Total);
        }
    }
}
