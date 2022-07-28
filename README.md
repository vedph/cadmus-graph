# Cadmus.Graph

- [Cadmus.Graph](#cadmusgraph)
  - [Docker](#docker)
    - [Demo](#demo)
    - [API](#api)
  - [History](#history)
    - [API History](#api-history)
    - [Demo History](#demo-history)
      - [0.0.4](#004)
      - [0.0.1](#001)

This library represents a enhanced graph mapping tool for Cadmus, bypassing pins and directly using JSON-encoded objects, whatever their type.

For more information see the [documentation](docs/index.md) (still incomplete).

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
docker build -f Dockerfile-api -t vedph2020/cadmus-graph-api:0.0.1 -t vedph2020/cadmus-graph-api:latest
```

To **run** the API, use the `docker-compose-api.yml` file as explained above. Please notice that you either have to rename it as `docker-compose.yml`, or use the `-f FileName` option like this:

```bash
docker compose -f docker-compose-api.yml up
```

## History

- 2022-07-28: added get nodes endpoint to API.
- 2022-07-11: fix missing URI in get linked literals.

### API History

### Demo History

#### 0.0.4

- 2022-05-31: refactored adapters and updated dependencies from `Cadmus` 4.0.0.

#### 0.0.1

- 2022-05-29: first release of libraries still dependent on legacy `Cadmus.Core`.
