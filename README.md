# Cadmus.Graph

- [Cadmus.Graph](#cadmusgraph)
  - [Docker](#docker)
    - [Demo](#demo)
    - [API](#api)
  - [History](#history)
    - [3.0.1](#301)
    - [2.4.0](#240)
    - [2.3.5](#235)
    - [2.3.4](#234)
    - [2.3.3](#233)
    - [2.3.2](#232)
    - [2.3.1](#231)
    - [2.3.0](#230)
    - [2.2.20](#2220)
    - [2.2.19](#2219)
    - [2.2.18](#2218)
    - [2.2.17](#2217)
    - [2.2.16](#2216)
    - [2.2.15](#2215)
    - [2.2.14](#2214)
    - [2.2.13](#2213)
    - [2.2.12](#2212)
    - [2.2.10](#2210)
    - [2.2.9](#229)
    - [2.2.8](#228)
    - [2.2.7](#227)
    - [2.2.5](#225)
    - [2.2.4](#224)
    - [2.2.3](#223)
    - [2.2.2](#222)
    - [2.2.0](#220)
    - [2.1.12](#2112)
    - [2.1.11](#2111)
    - [2.1.10](#2110)
    - [2.1.5](#215)
    - [2.1.4](#214)
    - [2.1.3](#213)
    - [2.1.0](#210)
    - [2.0.11](#2011)
    - [2.0.6](#206)
    - [2.0.5](#205)
    - [2.0.4](#204)
    - [2.0.3](#203)
    - [2.0.2](#202)
    - [1.0.1](#101)
    - [1.0.0](#100)
    - [0.1.1](#011)
    - [0.1.0](#010)
    - [0.0.9](#009)
    - [0.0.8](#008)
    - [API History](#api-history)
      - [0.0.1](#001)
    - [Demo History](#demo-history)
      - [0.0.5](#005)
      - [0.0.4](#004)
      - [0.0.1](#001-1)

This library represents a enhanced graph mapping tool for Cadmus, bypassing pins and directly using JSON-encoded objects, whatever their type.

For more information see the [documentation](docs/index.md) (still incomplete).

👀 [Cadmus Page](https://myrmex.github.io/overview/cadmus/)

## Docker

Both the API project and the demo Blazor app are development tools, not intended for production. The API project provides a backend to frontend apps, while the Blazor demo provides a UI to test the mapping engine.

As interactive test tools, both these projects can be containerized and run in other system.

### Demo

To **build** the image:

```bash
docker build . -t vedph2020/cadmus-graph-demo:0.0.1 -t vedph2020/cadmus-graph-demo:latest
```

To **run** a container:

```bash
docker run -p 8080:80 --name graphdemo vedph2020/cadmus-graph-demo:latest
```

or just use the `docker-compose.yml` file from this solution, saving it in some folder, entering it from a terminal window, and running:

```bash
docker compose up
```

(or `docker-compose` -mind the dash- if using the old composer).

### API

To **build** the image:

```bash
docker build . -f Dockerfile-api -t vedph2020/cadmus-graph-api:0.1.0 -t vedph2020/cadmus-graph-api:latest
```

To **run** the API, use the `docker-compose-api.yml` file as explained above. Please notice that you either have to rename it as `docker-compose.yml`, or use the `-f FileName` option like this:

```bash
docker compose -f docker-compose-api.yml up
```

## History

- 2023-07-26: updated packages.

### 3.0.1

- 2023-07-23: **BREAKING CHANGE**: added `ScalarPattern` property to `Mapping` and updated repositories and SQL schema accordingly. This defines the optional regular expression pattern which should match against a scalar value defined by the mapping's source expression for the mapping to be applied. When this is defined and does not match, the mapping will not be applied. This can be used to overcome the limitations of the source expression in languages like JMESPath, where e.g. `.[?lost==true]` is always evaluated as a match, even when the value of the scalar property `lost` is `false`.

Example usage: this is a child mapping of a work info mapping, which is applied only when the `isLost` property of the source object is `true`:

```json
{
  "name": "work-info/isLost",
  "source": "isLost",
  "scalarPattern": "true",
  "output": {
    "nodes": {
      "destruction": "itn:events/destruction## [itn:events/destruction]"
    },
    "triples": [
      "{?destruction} a crm:E6_Destruction",
      "{?destruction} crm:P13_destroyed {?work}"
    ]
  }
}
```

### 2.4.0

- 2023-07-22: minor breaking change in **mapped node parsing**: instead of `uri`, `uri label`, `uri [tag]`, `uri label [tag]`, the node is now represented as `uri`, `uri [label]`, `uri [|tag]`, `uri [label|tag]`. This way, parsing no more relies on space to define the label, which conflicted with spaces in the URI definition as used by macros like `{!_substring(. & 1)}`. Square brackets before the ending label/tag are correctly allowed.

### 2.3.5

- 2023-07-19: fixes to SID building in JSON node mapper. The inherited SID is overwritten by a child mapping when this has its SID template specified. Also, any inherited SID including the `index` metadatum (`{$index}`) is recalculated at each item iteration.

### 2.3.4

- 2023-07-12: fixes to graph updater: in adding triples, not only ensure that the URI exists, but also that the corresponding node exists.

### 2.3.3

- 2023-07-11: added `AddMappingByName` to mappings repository.

### 2.3.2

- 2023-07-09: added builtin `_substring` macro.

### 2.3.1

- 2023-07-04: fixes to graph updater.

### 2.3.0

- 2023-07-01: changed `IUidBuilder` implementations so that:
  - SID is no more used in matching UIDs, as the same UID might be generated in the context of different SIDs (e.g. a link to an entity in another part);
  - added convention by which a generated UID which should always be unique (by eventually receiving a numeric suffix; e.g. a timespan) should end with `##`. These `##` will then be removed or replaced with `#` plus a unique number.
- 2023-06-30: fix to `EfGraphRepository.UpdateGraph` to avoid duplicate nodes.

### 2.2.20

- 2023-06-29: fix to populate node classes SQL function (check for null class ID).

### 2.2.19

- 2023-06-29:
  - updated packages.
  - moved update classes outside of transaction when updating graph.

### 2.2.18

- 2023-06-23: added `TripleObjectSupplier` utility class.

### 2.2.17

- 2023-06-23: fix to triple editing: object ID with value=0 must be treated as null.

### 2.2.16

- 2023-06-23: fixes to casing in EF-based comparisons.

### 2.2.15

- 2023-06-21: updated packages.

### 2.2.14

- 2023-06-21: updated packages.

### 2.2.13

- 2023-06-16: updated dependencies of `Cadmus.Graph.Sql`.

### 2.2.12

- 2023-06-15:
  - added `CreateStore` method to graph repository.
  - updated packages.

### 2.2.10

- 2023-06-12: added EF-based PgSql and MySql repositories and tests.
- 2023-06-10: adding EF support, minor fixes in `Cadmus.Graph` and `Cadmus.Graph.MySql`.
- 2023-06-07: adding PostgreSql support.

### 2.2.9

- 2023-05-31: fixed culture info in `LiteralHelper` and added tests.

### 2.2.8

- 2023-05-29: added `metadata-pid` to the metadata provided by `ItemEidMetadataSource` (`Cadmus.Graph.Extras`).

### 2.2.7

- 2023-05-29: fixed missing URI in `SqlGraphRepository.GetNodeByUri` and added tests.

### 2.2.5

- 2023-05-26: updated packages.

### 2.2.4

- 2023-05-23: updated packages (added asserted composite ID in bricks).

### 2.2.3

- 2023-05-16: updated packages for index.

### 2.2.2

- 2023-05-15:
  - fixes and tests for graph updater.

### 2.2.0

- 2023-05-15:
  - replaced mapper metadata with a dictionary of objects rather than of strings, thus matching the underlying components implementations.
  - added applied metadata tracing to node mapper and graph updater.

### 2.1.12

- 2023-05-15: fixes to mappings output population.

### 2.1.11

- 2023-05-15:
  - updated packages.
  - fixes to `NodeMapping.ToString`.

### 2.1.10

- 2023-05-13:
  - added `GraphUpdate.Explain`.
  - fixed missing metadata for mapper in graph updater.
  - fixed case in JSON adapter.

### 2.1.5

- 2023-05-13: fix to JSON mapping output converter.

### 2.1.4

- 2023-05-09: fixes to `MappedTriple` to string/parser to ensure that a literal is always wrapped in `""` eventually followed by `@lang` or `^^type`.

### 2.1.3

- 2023-05-07:
  - moved expression computation before SID calculation in node mapper.
  - handle corner cases in `ResolveDataExpression`. Results may be a single primitive; a single primitive in a 1-item array; or an array or object, to be evaluated against a JMES expression.
  - fixed culture in `_hdate` macro.
- 2023-05-05: added some comments.

### 2.1.0

- 2023-04-29:
  - added `IMappingRepository` and slightly adapted the graph repository to implement it.
  - added `RamMappingRepository`.

### 2.0.11

- 2023-04-28: fixes to `NodeMappingDocument` and its JSON serialization.

### 2.0.6

- 2023-04-26:
  - added `MetadataSupplier` to allow for additional metadata in graph updating.
  - added `Cadmus.Graph.Extras` project including a metadata supplier for `item-eid` relying on `MetadataPart`.

### 2.0.5

- 2023-04-13:
  - added `NodeMappingDocument`.
  - updated packages.

### 2.0.4

- 2023-04-10: updated packages.

### 2.0.3

- 2023-03-28: updated packages.

### 2.0.2

- 2023-02-01: migrated to new components factory. This is a breaking change for backend components, please see [this page](https://myrmex.github.io/overview/cadmus/dev/history/#2023-02-01---backend-infrastructure-upgrade). Anyway, in the end you just have to update your libraries and a single namespace reference. Benefits include:
  - more streamlined component instantiation.
  - more functionality in components factory, including DI.
  - dropped third party dependencies.
  - adopted standard MS technologies for DI.

### 1.0.1

-2023-02-01: updated packages.

### 1.0.0

- 2022-11-10: upgraded to NET 7.

### 0.1.1

- 2022-11-04: updated packages.

### 0.1.0

- 2022-11-04: updated packages (nullability enabled in Cadmus core).

### 0.0.9

- 2022-11-03: updated packages.
- 2022-10-14: updated packages.

### 0.0.8

- 2022-10-10: updated packages.
- 2022-10-03: updated packages.
- 2022-09-17: updated packages.
- 2022-07-28: added get nodes endpoint to API.
- 2022-07-11: fix missing URI in get linked literals.

### API History

- 2022-08-04: updated packages.

#### 0.0.1

- 2022-07-29: first image for testing.

### Demo History

- 2022-08-04: updated packages.

#### 0.0.5

- 2022-08-14: updated packages.

#### 0.0.4

- 2022-05-31: refactored adapters and updated dependencies from `Cadmus` 4.0.0.

#### 0.0.1

- 2022-05-29: first release of libraries still dependent on legacy `Cadmus.Core`.
