using Cadmus.Graph.Adapters;
using Cadmus.Index.Sql;
using Fusi.DbManager;
using Fusi.DbManager.MySql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Cadmus.General.Parts;
using Cadmus.Refs.Bricks;
using Fusi.Antiquity.Chronology;
using Cadmus.Core;
using Cadmus.Graph.Sql.Test;

namespace Cadmus.Graph.MySql.Test;

[Collection(nameof(NonParallelResourceCollection))]
public sealed class GraphUpdaterTest
{
    private const string CST = "Server=localhost;Database={0};Uid=root;Pwd=mysql;";
    private const string DB_NAME = "cadmus-index-test";
    static private readonly string CS = string.Format(CST, DB_NAME);

    private const string PART_ID = "bdd152f1-2ae2-4189-8a4a-e3d68c6a9d7e";
    private const string PETRARCH_URI = "x:guys/francesco_petrarca";

    private static void ResetIndex()
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
        MySqlGraphRepository repository = new();
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

    private GraphSet BuildSampleGraph(IGraphRepository repository)
    {
        // load events data
        string data = LoadResourceText("PetrarchEvents.json");

        // load mappings
        IGraphPresetReader reader = new JsonGraphPresetReader();
        IList<NodeMapping> mappings =
            reader.LoadMappings(GetResourceStream("PetrarchMappings.json"));

        // setup mapper
        INodeMapper mapper = new JsonNodeMapper
        {
            UidBuilder = repository
        };
        // mock metadata from item
        mapper.Data[ItemGraphSourceAdapter.M_ITEM_ID]
            = Guid.NewGuid().ToString();
        mapper.Data["item-uri"] = PETRARCH_URI;
        mapper.Data[ItemGraphSourceAdapter.M_ITEM_TITLE] = "Francesco Petrarca";
        mapper.Data[ItemGraphSourceAdapter.M_ITEM_FACET] = "person";
        // mock metada from part
        mapper.Data[PartGraphSourceAdapter.M_PART_ID] = PART_ID;
        mapper.Data[PartGraphSourceAdapter.M_PART_TYPE_ID] =
            "it.vedph.historical-events";

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
        ResetIndex();
        IGraphRepository repository = GetRepository();
        GraphSet set = BuildSampleGraph(repository);
        Assert.Equal(5 + 3, set.Nodes.Count);
        Assert.Equal(9 + 7, set.Triples.Count);

        // 1) store initial set
        repository.UpdateGraph(set);

        var nodePage = repository.GetNodes(new NodeFilter());
        Assert.Equal(21, nodePage.Total);

        var triplePage = repository.GetTriples(new TripleFilter());
        Assert.Equal(9 + 7, triplePage.Total);

        // 2) user-edits: we emulate them by directly changing the graph
        // (in real world, we would regenerate the full graph from a changed
        // data source, here an events part)
        // - petrarca a person (added)
        UriNode petrarca = repository.GetNodeByUri(PETRARCH_URI)!;
        Assert.NotNull(petrarca);
        set.AddTriples(new[]{
            new UriTriple
            {
                SubjectUri = PETRARCH_URI,
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
        Assert.Equal(18, nodePage.Total);

        triplePage = repository.GetTriples(new TripleFilter());
        Assert.Equal(9, triplePage.Total);
    }

    [Fact]
    public void Delete_NotExisting_Ok()
    {
        ResetIndex();
        IGraphRepository repository = GetRepository();

        repository.DeleteGraphSet("not-existing");
        Assert.True(true);
    }

    [Fact]
    public void Delete_Existing_Ok()
    {
        ResetIndex();
        IGraphRepository repository = GetRepository();
        GraphSet set = BuildSampleGraph(repository);
        repository.UpdateGraph(set);

        repository.DeleteGraphSet($"{PART_ID}/death");

        var nodePage = repository.GetNodes(new NodeFilter());
        Assert.Equal(17, nodePage.Total);

        var triplePage = repository.GetTriples(new TripleFilter());
        Assert.Equal(9, triplePage.Total);
    }

    // itinera events

    private static IItem GetMockWorkItem()
    {
        return new Item
        {
            FacetId = "work",
            Title = "Alpha work",
            Description = "The alpha work.",
            CreatorId = "zeus",
            UserId = "zeus",
        };
    }

    [Fact]
    public void Update_TextSend_Ok()
    {
        ResetIndex();
        IGraphRepository repository = GetRepository();
        TestHelper.ImportNodes("ItineraNodes.json", repository);
        TestHelper.ImportMappings("ItineraMappings.json", repository);

        // event
        IItem item = GetMockWorkItem();
        MetadataPart metadataPart = new()
        {
            ItemId = item.Id,
            CreatorId = "zeus",
            UserId = "zeus",
        };
        metadataPart.Metadata.Add(new()
        {
            Name = "eid",
            Value = "alpha"
        });
        item.Parts.Add(metadataPart);

        HistoricalEventsPart eventsPart = new()
        {
            ItemId = item.Id,
            CreatorId = "zeus",
            UserId = "zeus",
        };
        item.Parts.Add(eventsPart);
        HistoricalEvent sent = new()
        {
            Eid = "alpha-sent",
            Type = "text.send",
        };
        sent.Chronotopes.Add(new AssertedChronotope
        {
            Place = new AssertedPlace
            {
                Value = "Arezzo"
            },
            Date = new AssertedDate
            {
                A = Datation.Parse("1234")!
            }
        });
        sent.RelatedEntities.Add(new RelatedEntity
        {
            Relation = "text:send:recipient",
            Id = new AssertedCompositeId
            {
                Target = new PinTarget
                {
                    Gid = "itn:persons/arezzo_bishop",
                    Label = "arezzo_bishop"
                }
            }
        });
        sent.Description = "Alpha work was sent to the bishop of Arezzo in 1234.";
        sent.Note = "Editorial note.";
        eventsPart.Events.Add(sent);

        GraphUpdater updater = new(repository)
        {
            MetadataSupplier = new MetadataSupplier().AddMockItemEid(
                metadataPart.Id, "alpha")
        };
        updater.Update(item, eventsPart);

        // nodes
        UriNode? alphaSent = repository.GetNodeByUri(
            $"itn:events/{eventsPart.Id}/alpha-sent");
        Assert.NotNull(alphaSent);

        UriNode? arezzo = repository.GetNodeByUri("itn:places/arezzo");
        Assert.NotNull(arezzo);

        UriNode? timespan = repository.GetNodeByUri("itn:timespans/ts");
        Assert.NotNull(timespan);

        UriNode? bishop = repository.GetNodeByUri("itn:persons/arezzo_bishop");
        Assert.NotNull(bishop);

        // triples
        IList<UriTriple> triples = repository.GetTriples(new TripleFilter
        {
            PageSize = 100
        }).Items;

        // event
        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "rdf:type"
                 && t.ObjectUri == "crm:e7_activity");

        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "crm:p2_has_type"
                 && t.ObjectUri == "itn:event-types/text.send");

        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "crm:p16_used_specific_object"
                 && t.ObjectUri == $"itn:works/{item.Id}/alpha");

        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "crm:p3_has_note"
                 && t.ObjectLiteral!.StartsWith("Alpha"));

        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "crm:p3_has_note"
                 && t.ObjectLiteral!.StartsWith("Editorial"));

        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "crm:p7_took_place_at"
                 && t.ObjectUri == arezzo.Uri);

        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "crm:p4_has_time-span"
                 && t.ObjectUri == timespan.Uri);

        // place
        Assert.Contains(triples,
            t => t.SubjectUri == arezzo.Uri
                 && t.PredicateUri == "rdf:type"
                 && t.ObjectUri == "crm:e53_place");

        // time
        UriTriple? triple = triples.FirstOrDefault(
            t => t.SubjectUri == timespan.Uri
                 && t.PredicateUri == "crm:p82_at_some_time_within"
                 && t.ObjectLiteral == "1234");
        Assert.NotNull(triple);
        Assert.Equal(1234, triple.LiteralNumber);
        Assert.Equal("xs:float", triple.LiteralType);

        triple = triples.FirstOrDefault(
            t => t.SubjectUri == timespan.Uri
                 && t.PredicateUri == "crm:p87_is_identified_by"
                 && t.ObjectLiteral == "1234 AD");
        Assert.NotNull(triple);
        Assert.Equal("en", triple.LiteralLanguage);

        // related
        Assert.Contains(triples,
            t => t.SubjectUri == alphaSent.Uri
                 && t.PredicateUri == "crm:p11_has_participant"
                 && t.ObjectUri == bishop.Uri);
    }
}
