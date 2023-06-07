# Cadmus.Graph

- [Cadmus.Graph](#cadmusgraph)
  - [Docker](#docker)
    - [Demo](#demo)
    - [API](#api)
  - [History](#history)
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
