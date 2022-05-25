# Cadmus Graph

- [mappings](mappings.md)
- [database](database.md)

This library provides core components for mapping Cadmus data into RDF-like graphs.

The mappings are stored in the RDBMS index database, but for easier definition you can list them in a JSON file, and then let Cadmus tool import them.

The mapping flow includes these main steps:

1. a source object is provided to the mapper. This can be any type, but the current implementation relies on objects serialized into JSON. Usually, these come from MongoDB directly, so JSON is already at hand. Source object are items, parts, or thesauri. Whatever their type, ultimately from the point of view of the mapper they are just JSON code.

2. the SID for the source object gets built.

3. the mapper finds all the mappings matching the source object, and applies each of them, collecting the results (nodes and triples) into a graph set.

4. the graph set is merged into the graph store.
