using Cadmus.Core;
using Cadmus.Index.Graph;
using Cadmus.Index.Sql;
using Fusi.DbManager;
using Fusi.DbManager.MySql;
using Fusi.Tools.Data;
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

        [Fact]
        public void Update_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();

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
            mapper.Data["item-uri"] = "x:francesco_petrarca";
            mapper.Data["item-label"] = "Petrarch";
            mapper.Data["group-id"] = "group";
            mapper.Data["facet-id"] = "facet";
            mapper.Data["flags"] = "3";
            // mock metada from part
            mapper.Data["part-id"] = Guid.NewGuid().ToString();

            // run mappings
            GraphSet set = new();
            foreach (NodeMapping mapping in mappings)
            {
                mapper.Map(data, mapping, set);
            }

            // 1) store initial set: 5 nodes, 9 triples
            repository.UpdateGraph(set);

            var nodePage = repository.GetNodes(new NodeFilter());
            Assert.Equal(5, nodePage.Total);

            var triplePage = repository.GetTriples(new TripleFilter());
            Assert.Equal(9, triplePage.Total);

            // 2) user-edits:
            // - petrarca a person (added)
            UriNode petrarca = repository.GetNodeByUri("x:francesco_petrarca")!;
            Assert.NotNull(petrarca);
            set.AddTriples(new[]{
                new UriTriple
                {
                    SubjectUri = "x:francesco_petrarca",
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
            // TODO

            repository.UpdateGraph(set);

            // TODO
        }

        [Fact]
        public void Delete_NotExisting_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();

            repository.DeleteGraphSet("not-existing");
        }

        [Fact]
        public void Delete_Existing_Ok()
        {
            Reset();
            IGraphRepository repository = GetRepository();

            // TODO

            // delete set
            // repository.DeleteGraphSet(part.Id);
        }
    }
}
