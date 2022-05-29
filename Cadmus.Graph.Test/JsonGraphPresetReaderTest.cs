using Cadmus.Core.Config;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Cadmus.Graph.Test
{
    public sealed class JsonGraphPresetReaderTest
    {
        private Stream GetResourceStream(string name)
        {
            return GetType().Assembly.GetManifestResourceStream(
                "Cadmus.Graph.Test.Assets." + name)!;
        }

        [Fact]
        public void ReadNodes_Ok()
        {
            JsonGraphPresetReader reader = new();

            IList<UriNode> nodes = reader.ReadNodes(
                GetResourceStream("Nodes.json")).ToList();

            Assert.Equal(10, nodes.Count);
            Assert.Equal("is-a", nodes[0].Label);
            Assert.Equal("lemma", nodes[9].Label);
        }

        [Fact]
        public void ReadNodeMappings_Ok()
        {
            JsonGraphPresetReader reader = new();

            IList<NodeMapping> mappings = reader.LoadMappings(
                GetResourceStream("Mappings.json"));

            Assert.Equal(10, mappings.Count);
            Assert.Equal("Lemma item", mappings[0].Name);
            Assert.Equal("Pin variant@* x:hasIxVariantForm ...", mappings[9].Name);
            for (int i = 0; i < 10; i++)
                Assert.Equal(i + 1, mappings[i].Id);
        }

        [Fact]
        public void ReadThesauri_Ok()
        {
            JsonGraphPresetReader reader = new();

            IList<Thesaurus> thesauri = reader.ReadThesauri(
                GetResourceStream("Thesauri.json")).ToList();

            Assert.Equal(2, thesauri.Count);
            Assert.Equal("colors@en", thesauri[0].Id);
            Assert.Equal("shapes@en", thesauri[1].Id);
        }
    }
}
