using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Cadmus.Graph.MySql.Test;

internal static class TestHelper
{
    public static string LoadResourceText(string name)
    {
        using StreamReader reader = new(
            typeof(TestHelper).Assembly.GetManifestResourceStream(
            "Cadmus.Graph.MySql.Test.Assets." + name)!, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static void ImportMappings(string name, IGraphRepository repository)
    {
        string json = LoadResourceText(name);
        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new NodeMappingOutputJsonConverter());

        NodeMappingDocument doc =
            JsonSerializer.Deserialize<NodeMappingDocument>(json, options)!;

        // source id : graph id
        Dictionary<int, int> ids = new();
        foreach (NodeMapping mapping in doc.GetMappings())
        {
            // adjust IDs
            int sourceId = mapping.Id;
            mapping.Id = 0;
            if (mapping.ParentId != 0)
                mapping.ParentId = ids[mapping.ParentId];
            repository.AddMapping(mapping);
            ids[sourceId] = mapping.Id;
        }
    }

    public static void ImportNodes(string name, IGraphRepository repository)
    {
        using Stream source = typeof(TestHelper).Assembly.GetManifestResourceStream(
            "Cadmus.Graph.MySql.Test.Assets." + name)!;
        JsonGraphPresetReader reader = new();

        foreach (UriNode node in reader.ReadNodes(source))
        {
            node.Id = repository.AddUri(node.Uri!);
            Console.WriteLine(node);
            repository.AddNode(node);
        }
    }

    private static void HydrateTriple(UriTriple triple,
        IGraphRepository repository)
    {
        // subject
        if (triple.SubjectId == 0)
        {
            if (triple.SubjectUri == null)
                throw new ArgumentNullException("No subject for triple: " + triple);
            triple.SubjectId = repository.LookupId(triple.SubjectUri);
            if (triple.SubjectId == 0)
                throw new ArgumentNullException("Missing URI " + triple.SubjectUri);
        }

        // predicate
        if (triple.PredicateId == 0)
        {
            if (triple.PredicateUri == null)
                throw new ArgumentNullException("No predicate for triple: " + triple);
            triple.PredicateId = repository.LookupId(triple.PredicateUri);
            if (triple.PredicateId == 0)
                throw new ArgumentNullException("Missing URI " + triple.PredicateUri);
        }

        // object
        if (triple.ObjectLiteral == null && triple.ObjectId == 0)
        {
            if (triple.ObjectUri == null)
                throw new ArgumentNullException("No object for triple: " + triple);
            triple.ObjectId = repository.LookupId(triple.ObjectUri);
            if (triple.ObjectId == 0)
                throw new ArgumentNullException("Missing URI " + triple.ObjectUri);
        }
    }

    public static void ImportTriples(string name, IGraphRepository repository)
    {
        using Stream source = typeof(TestHelper).Assembly.GetManifestResourceStream(
            "Cadmus.Graph.MySql.Test.Assets." + name)!;

        JsonGraphPresetReader reader = new();
        foreach (UriTriple triple in reader.ReadTriples(source))
        {
            HydrateTriple(triple, repository);
            Console.WriteLine(triple);
            repository.AddTriple(triple);
        }
    }
}
