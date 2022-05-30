# Cadmus Graph

- [adapters](adapters.md)
- [mappings](mappings.md)
- [updating](updating.md)
- [database](database.md)

This library provides core components for mapping Cadmus data into RDF-like graphs.

The mappings are stored in the RDBMS index database, but for easier definition you can list them in a JSON file, and then let Cadmus tool import them.

The mapping flow includes these main steps:

(1) a **source object** is provided to the mapper. This can be any type, but the current implementation relies on objects serialized into JSON. Usually, these come from MongoDB directly, so JSON is already at hand. Source object are items or parts (thesauri can be imported as nodes, but this does not happen via mapping as it's a single procedure, whatever the thesaurus). At any rate, ultimately from the point of view of the mapper any source object is just JSON code representing it.

>Note: between the source object and the mappings there is an intermediate layer represented by [adapter components](adapters.md), whose task is adapting that object to the mappings and providing additional information from it.

(2) the mapper finds all the **mappings** matching the source object, and applies each of them, collecting the results (nodes and triples) into a graph set.

(3) the graph set is **[merged](updating.md)** into the graph store.
