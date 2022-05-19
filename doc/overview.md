# Cadmus Graph

This library provides core components for mapping Cadmus data into RDF-like graphs.

## Mappings

The mappings are stored in the RDBMS index database, but for easier definition you can list them in a JSON file, and then let Cadmus tool import them. The JSON file contains an array of mapping objects, with these properties:

- `id`: a human-friendly ID for the mapping. This is scoped to the definitions source, where it can be used to recall a mapping from another one (as its child).
- `sourceType`: the source object type: `item`, `part` or `thesaurus`.
- `facetFilter`: the optional item's facet filter.
- `groupFilter`: the optional item's group filter.
- `flagsFilter`: the optional item's flags filter.
- `titleFilter`: the optional item's title filter.
- `partTypeFilter`: the optional part's type ID filter.
- `partRoleFilter`: the optional part's role filter.
- `description`: an optional description for the mapping.
- `source`: the source expression to be matched against the source object for the mapping to apply. In this implementation, this is a [JMES Path](https://jmespath.org/).
- `output`: the output of the mapping. This is an object with two properties, `nodes` and `triples`:
  - `nodes`: an object with nodes definitions, working as a dictionary: each node has a key, scoped to the definition file, and used to refer to it in templates; the value of the key is a string representing the node's URI, eventually followed by its label and/or tag (the tag being wrapped in `[]`). Among these 3 components, only the URI is required; the components are separated by space.
  - `triples`: an array with triples definitions, each being a string with subject, predicate and object separated by spaces, like in Turtle.

## Flow

The mapping flow includes these main steps:

1. a source object is provided to the mapper. This can be any type, but the current implementation relies on objects serialized into JSON. Usually, these come from MongoDB directly, so JSON is already at hand. Source object are items, parts, or thesauri. Whatever their type, ultimately from the point of view of the mapper they are just JSON code.

2. the SID for the source object gets built.

3. the mapper finds all the mappings matching the source object, and applies each of them, collecting the results (nodes and triples) into a graph set.

4. the graph set is merged into the graph store.
