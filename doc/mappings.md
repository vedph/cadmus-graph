# Mappings

The mapping between Cadmus source data (items and parts) and nodes is defined by a number of node mappings.

It should be stressed that the nodes produced from mapping are not intended to fully represent all the data from each Cadmus part. This is not the purpose of the relationships editing system, but rather a publishing task. The nodes mapped here are only those which need to be connected by users while editing, together with all the linked nodes considered useful for this purpose.

## Sources

At the input side of mappings, there are these types of sources:

- **item**: an item. You can have mappings for the item; its group; its facet.
- **part**: a part.
- **thesaurus**: a thesaurus (not an alias, but a true thesaurus with entries).

Note that for _item titles_ a couple of conventions dictate that:

- if the title ends with `[#...]`, then the text between `[#` and `]` is assumed as the UID. The only processing is prepending the prefix defined in the mapping, if any.
- if the title ends with `[@...]`, then the text between `[@` and `]` is prefixed to the generated UID. If the mapping already defines a prefix, it gets prepended to this one.

## Identifiers

Before illustrating these scenarios in more details, we must discuss the different identifiers used in the mapping process. In this document, these are referred to with SID (source ID), UID (entity's URI-based ID), and EID (entry's ID).

### Source ID (SID)

The entity source ID (SID for short) is calculated so that _the same sources always point to the same entities_. The SID is essential for connecting Cadmus data to the entities and keeping them in synch, as it provides the path by which data get added and updated.

The algorithm building the SID is idempotent, so you can run it any time being confident that the same input will always produce the same output. This is ensured by the fact that GUIDs are unique by definitions.

A SID is built with these components:

a) for **items**:

1. the _GUID_ of the source (item).
2. if the node comes from a group or a facet, the suffix `|group` or `|facet`. On passage, note that the group ID can be composite (using slash, e.g. `alpha/beta`); in this case, a mapping producing nodes for groups emits several nodes, one for each component. The top component is the first in the group ID, followed by its children (in the above sample, `beta` is child of `alpha`). Each of these nodes has an additional suffix for the component ordinal, preceded by `|`.

Examples:

- `76066733-6f81-48dd-a653-284d5be54cfb`: an entity derived from an item.
- `76066733-6f81-48dd-a653-284d5be54cfb|group`: an entity derived from an item's group.
- `76066733-6f81-48dd-a653-284d5be54cfb|group|2`: an entity derived from the 2nd component of an item's composite group.

b) for **parts**:

1. the _GUID_ of the source (part).
2. if the part has a role ID, the _role ID_ preceded by `:`.

Examples:

- `76066733-6f81-48dd-a653-284d5be54cfb`: an entity derived from a part.
- `76066733-6f81-48dd-a653-284d5be54cfb:some-role`: an entity derived from a part with a role.

### Entity ID (UID)

The entity ID is a _shortened URI_ where a conventional prefix replaces the namespace, calculated as defined by the entity mapping.

To get relatively human-friendly UIDs, the UID is essentially derived from a template defined in the mapping rule generating a node.

Yet, as we have to ensure that each UID is unique, whenever the template provides a result which happens to be already present (i.e. technically it is found in `sid_lookup`.`unsuffixed`), the UID gets a numeric suffix preceded by `#`. This suffix gets generated from the DB so we can be sure it is unique in our data.

Once the UID gets generated, a SID/UID pair is saved in `sid_lookup`. This links the source identified by SID (essentially, an item or a part's pin) to the entity identified by UID.

For instance, say an item representing a person emits an entity, whose label is mapped to its title; the title is `Barbato da Sulmona [@correspondents]`, with a `person` facet ID. Say our item's mapping has:

- `source_type` = 0 (item).
- `prefix` = `x:persons/{title-prefix}/`, which gets resolved into `x:persons/correspondents/`.
- `label_template` = `{title}`, resulting in `barbato_da_sulmona` (while the final prefix `correspondents`, as defined by convention, was moved into `title-prefix`).

According to this mapping, we would then get the UID `x:persons/correspondents/barbato_da_sulmona`.

### Entry ID (EID)

A third type of identifiers is represented by the "entry" ID, which is a convention followed in multi-entity parts.

A Cadmus part corresponding to a single entity is a single "entry". In this case, its ID is simply provided by the part's item. For instance, a person-information part inside a person item just adds data to the unique entity represented by the item (=the person). So, the target entity is just the one derived from the item.

Conversely, a manuscript's decorations part is a collection of decorations, each corresponding to an entry, eventually having its EID (exposed via an `eid` pin). All the entries with EIDs get mapped into entities.

Thus, here we call EIDs the identifiers provided by users for entries in a Cadmus collection-part. When present, such EIDs are used to build node identifiers.

## Templates

Templates are used in mappings to build node identifiers and triple values.

A template has any number of placeholders conventionally delimited by `{}` and prefixed by a single character representing the placeholder type:

- `@{...}` = expression: this represents the expression used to select some source data for the mapping.
- `?{...}` = node key: the key for a previously emitted node, eventually suffixed.
- `${...}` = metadata: any metadata set during the mapping process.
- `!{...}` = macro: the output of a custom function, receiving the current data context from the source, and returning a string or null.

### Expressions (@)

Expressions select data from the source. The syntax of an expression depends on the mapper's implementation.

Currently the only implementation is JSON-based, so expressions are [JMES paths](https://jmespath.org/). For instance, say you are mapping an event object having an `eid` property equal to some string: you can select the value of this string with the placeholder `@{eid}`.

### Node Keys (?)

During the mapping process, nodes emitted in the context of each mapping (including all its descendant mappings) are stored in a dictionary with the keys specified in the mapping itself for each node.

For instance, say your event object emits a node corresponding to each of its events. The mapping output for each node specifies an arbitrary key used to refer to this node from other templates in the root mapping's context.

As a sample, consider this mapping fragment:

```json
{
  "id": "events.type=birth",
  "sourceType": "part",
  "partTypeFilter": "it.vedph.historical-events",
  "source": "events[?type=='person.birth']",
  "children": [
    {
      "source": "eid",
      "output": {
        "nodes": {
          "event": "x:events/${.}"
        },
      }
    }
  ]
}
```

Here we map each birth event (`source`). For each of them, a child mapping matches the event's `eid` property, and outputs a node under the key "event", whose template is `x:events/${.}`.

As a node is a complex object, in a template placeholder you can pick different properties from it. These are specified by adding a **suffix** preceded by `:` to the node's key. Available suffixes are:

- `:uri` = the node's generated URI. This is the default property; so when there is no suffix specified, the URI is picked.
- `:label` = the node's label.
- `:sid` = the node's SID.
- `:src_type` = the node's source type.

### Metadata ($)

The mapping process can set some metadata, which get stored under arbitrary keys, and are available to any template in the context of its root mapping.

Metadata can be emitted by the mapping process itself, or be defined in a mapping's output under the `metadata` property. This is an object where each property is a metadatum with its string value.

Currently the mapping process emits these metadata:

- `item-id`: the item ID (GUID).
- `part-id`: the part ID (GUID).
- `group-id`: the item's group ID.
- `facet-id`: the item's facet ID.
- `flags`: the item's flags.
- `.`: the value of the current leaf node in the source JSON data. For instance, if the mapping is selecting a string property from `events/event[0].eid`, this is the value of `eid`.
- `index`: the index of the element being processed from a source array. When the source expression used by the mapping points to an array, every item of the array gets processed separately from that mapping onwards. At each iteration, the `index` metadatum is set to the current index.

### Macros (!)

Macros are a modular way for customizing the mapping process when more complex logic is required. A macro is just an object implementing an interface (`INodeMappingMacro`), requiring:

- the macro `Id` (an arbitrary string). This is used to call the macro from the template.
- the `Run` method, which runs the macro receiving the current data context, the placeholder position in the template and the template itself, and any arguments following the macro's ID; and returning a string or null.

The macro syntax in the placeholder is very simple: it consists of the macro ID, optionally followed by any number of arguments, separated by space, included in brackets. For instance:

```txt
!{some_macro}
!{_smart-sep(/)}
```

Some macros are built-in and conventionally their ID start with an underscore.

- `_smart-sep(separator)`: return the specified text to be used as a separator, or an empty string if the placeholder being processed is immediately preceded by that separator. This can be used to concatenate components delimited by some separator in a smart way, avoiding double separators for empty components.

### Filters

Whenever a template represents a URI, i.e. in all the cases except for triple's object literals, once the template has been filled the result gets filtered as follows:

- whitespaces are normalized and replaced with underscores;
- the result is trimmed;
- only letters, digits, and characters `/#_-` are preserved;
- letters are all lowercased;
- diacritics are removed.
