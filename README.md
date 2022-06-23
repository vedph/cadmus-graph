# Cadmus.Graph

This library represents a enhanced graph mapping tool for Cadmus, bypassing pins and directly using JSON-encoded objects, whatever their type.

For more information see the [documentation](docs/index.md) (still incomplete).

## Demo Docker Image

Both the API project and the demo Blazor app are development tools, not intended for production. The API project provides a backend to frontend apps, while the Blazor demo provides a UI to test the mapping engine.

As an interactive test tool, the Blazor demo can be containerized and run in other system. To this end, open a terminal window at the root of this solution, and run:

```bash
docker build . -t vedph2020/cadmus-graph-demo:0.0.1 -t vedph2020/cadmus-graph-demo:latest
```

To run:

```bash
docker run -p 8080:80 --name graphdemo vedph2020/cadmus-graph-demo:latest
```

or just use the `docker-compose.yml` file from this solution, saving it in some folder, entering it from a terminal window, and running:

```bash
docker compose up
```

(or `docker-compose` -mind the dash- if using the old composer).

## History

### 0.0.4

- 2022-05-31: refactored adapters and updated dependencies from `Cadmus` 4.0.0.

### 0.0.1

- 2022-05-29: first release of libraries still dependent on legacy `Cadmus.Core`.
