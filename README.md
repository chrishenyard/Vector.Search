# Vector.Search

Vector.Search is a .NET 10 solution for indexing source code into a vector store and exposing HTTP + SignalR endpoints that power semantic code search and related features. It scans a repository, chunks supported files, generates embeddings via [Ollama](https://github.com/ollama/ollama), and persists those embeddings using the Semantic Kernel PgVector connector on PostgreSQL. Clients can connect over SignalR to receive real-time progress as files are chunked and embedded.

---

## Table of Contents

- [Features](#features)
- [Solution Structure](#solution-structure)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Configuration](#configuration)
  - [Running the Service](#running-the-service)
- [HTTP & SignalR APIs](#http--signalr-apis)
- [Integration Tests](#integration-tests)
- [Development Guide](#development-guide)
- [Contributing](#contributing)
- [Troubleshooting](#troubleshooting)

---

## Features

- **Code-aware chunking pipeline**
  - Pluggable `IChunk` implementation (e.g., `CodeChunking`) that walks a repository and splits code files (C# and potentially others) into semantically aligned chunks.
  - Configurable minimum chunk size and set of file extensions.
  - Stable hashing and GUID generation per chunk for idempotent indexing and deduplication.

- **Embedding service**
  - Uses Ollama for both embedding and chat models, configured via `appsettings.*.json` and/or environment variables.
  - Processes chunks in parallel with configurable `ParallelOptions`.
  - Streams structured progress notifications (`ChunkProcessed`, `EmbeddingCompleted`, `EmbeddingError`) over SignalR.

- **Vector storage**
  - Integrates with `Microsoft.SemanticKernel.Connectors.PgVector` and `Microsoft.Extensions.VectorData.Abstractions`.
  - Uses a `VectorStoreCollection<Guid, CodeChunkRecord>`-style abstraction for upsert and search operations.
  - Targets PostgreSQL with the `pgvector` extension enabled for efficient similarity search.

- **Diagnostics & observability**
  - Minimal APIs for configuration and connectivity: `/debug/config`, `/debug/connection`, `/health/ollama`.
  - Structured logging via Serilog.
  - Optional OpenTelemetry-based tracing/metrics export.
  - Background cleanup of temporary chunk files (configurable).

---

## Solution Structure

The solution is organized roughly as follows (names may vary slightly depending on the repo):

- `src/Vector.Search`
  - ASP.NET Core minimal API host.
  - HTTP endpoints, SignalR hubs, and background processing orchestration.
  - Wiring for Ollama, vector store, and chunking services.
- `src/Vector.Store` / `src/Vector.Files` (or similar)
  - Chunking pipeline implementation (e.g., `CodeChunking`).
  - Models that represent chunks (`CodeChunkRecord` / `CodeChunk`).
  - Vector store integration (`CodeVectorStore`), which wraps `VectorStoreCollection`.
- `tests/*`
  - Integration and (optionally) unit tests targeting the public HTTP/SignalR surface.

Check the actual folder names in your repository for the exact layout.

---

## Getting Started

### Prerequisites

- .NET SDK 10
- PostgreSQL with the `pgvector` extension installed and enabled.
- A running [Ollama](https://github.com/ollama/ollama) instance with:
  - At least one embedding model (e.g., `nomic-embed-text`).
  - At least one chat model (e.g., `llama3.1`).
- (Optional) A logging/observability backend such as [Seq](https://datalust.co/seq) or an OpenTelemetry-compatible target.

### Configuration

Configuration is provided via `appsettings.json` / `appsettings.Development.json` and environment variables. Important settings include:

- **Repository & chunking**
  - `REPO_ROOT`  
    Absolute path to the repository to index.
  - `FILE_EXTENSIONS`  
    Comma-separated list of file extensions to include (e.g., `.cs,.csproj`).
  - `DeleteTemporaryFiles` (in vector store/chunking settings)  
    Whether to delete temporary chunk files after processing.

- **Vector store**
  - `COLLECTION_NAME`  
    Name of the vector store collection.
  - Connection string / settings for PostgreSQL with `pgvector`.
  - `Microsoft.SemanticKernel.Connectors.PgVector` and `Microsoft.Extensions.VectorData.*` versions must be compatible (managed centrally in `Directory.Packages.props`).

- **Ollama / models**
  - `OllamaSettings:Url`  
    Base URL for the Ollama instance (e.g., `http://ollama:11434`).
  - `EMBEDDING_MODEL`  
    Embedding model name (e.g., `nomic-embed-text`).
  - `CHAT_MODEL`  
    Chat model name (e.g., `llama3.1`).
  - `OllamaSettings:TimeoutFromMinutes`  
    Timeout used for Ollama operations.

### Running the Service

From the repository root:

```
dotnet run --project src/Vector.Search
```

Then you can:

- Browse to `https://localhost:<port>/` to confirm it’s running.
- Call `GET /debug/config` to inspect runtime configuration.
- Call `GET /health/ollama` to verify Ollama connectivity.
- Use `POST /api/embed` with a JSON `EmbedRequest` and a valid SignalR connection ID to start indexing a repo.

---

## HTTP & SignalR APIs

### HTTP Endpoints

- `GET /`  
  Basic text response: confirms that Vector.Search is running.

- `GET /api/antiforgery/token`  
  Returns antiforgery tokens and sets an `XSRF-TOKEN` cookie for SPA/browser clients.

- `GET /debug/config`  
  Returns high-level runtime configuration, including Ollama URL, selected models, and timeout values.

- `GET /debug/connection`  
  Uses `IHttpClientFactory` to call `Ollama /api/tags` and returns connectivity details and raw response content.

- `GET /health/ollama`  
  Uses `OllamaApiClient` to list models and returns a simple `{ status = "healthy", models = [...] }` payload if Ollama is reachable.

- `POST /api/embed`  
  Starts the embedding pipeline for the configured repository.
  - Request body: an `EmbedRequest` that includes at least a SignalR connection ID.
  - Response: `202 Accepted` with an operation ID.
  - Background processing:
    - Scans `REPO_ROOT` for files with `FILE_EXTENSIONS`.
    - Chunks them into temporary files.
    - Embeds each chunk via Ollama.
    - Upserts to the vector store.
    - Streams progress events over SignalR.

- `POST /api/code`  
  Demo endpoint that echoes the uploaded file’s name and length, used to verify file upload handling.

### SignalR Events

Clients connect to a hub (e.g., `EmbeddingHub`) and listen for:

- `ChunkProcessed`  
  - Payload includes `OperationId`, `FilePath`, and `Indexed` count.
- `EmbeddingCompleted`  
  - Payload includes `OperationId` and total `Indexed` count.
- `EmbeddingError`  
  - Payload includes `OperationId` and an error message.

---

## Integration Tests

Integration tests validate the system end-to-end using the real HTTP and SignalR surfaces:

- **Host startup**
  - Spins up the ASP.NET Core host using the same `Program` as production.
  - Uses test-specific configuration (test repo path, file extensions, Ollama and DB endpoints).

- **Embedding pipeline**
  - Issues a `POST /api/embed` with a test `EmbedRequest`.
  - Asserts the API returns `202 Accepted` and an operation ID.
  - Connects a SignalR client to capture:
    - `ChunkProcessed` events for each processed chunk.
    - `EmbeddingCompleted` when the process finishes successfully.
    - `EmbeddingError` if an error occurs.
  - (Optionally) Verifies records in the backing vector store (e.g., count, properties).

- **Diagnostics**
  - `GET /debug/config`: verifies that configured values are exposed correctly.
  - `GET /debug/connection`: ensures Ollama is reachable and returns expected metadata.
  - `GET /health/ollama`: ensures ‘healthy’ status given a working Ollama instance.

- **Failure paths**
  - Runs with misconfigured or unavailable dependencies to ensure:
    - Errors are logged.
    - `EmbeddingError` events are emitted.
    - HTTP responses remain meaningful (e.g., proper error codes where applicable).

---

## Development Guide

### Package Management

This solution uses [central package management](https://learn.microsoft.com/en-us/nuget/consume-packages/Central-Package-Management) via `Directory.Packages.props`. Keep versions aligned, especially for:

- `Microsoft.SemanticKernel.Connectors.PgVector`
- `Microsoft.Extensions.VectorData.Abstractions` (and related `Microsoft.Extensions.AI.*` packages)

Misaligned versions can cause runtime `TypeLoadException` errors.

### Local Development Workflow

1. Restore dependencies and build:
2. Run the API:
3. Tail logs using your configured Serilog sinks (console, Seq, etc.).
4. Use your preferred HTTP client (curl, Postman, browser) and a SignalR client to exercise the endpoints.

---

## Contributing

Contributions are welcome.

1. **Fork** the repository and create a feature branch.
2. **Develop**:
- Make changes in `src/*` projects.
- Add or update tests in `tests/*`.
3. **Test**:
4. **Submit a Pull Request**:
- Describe the change clearly.
- Include any new configuration options or breaking changes.
- Reference any related issues.

Please keep changes focused and small where possible and ensure the build and tests pass before submitting.

---

## Troubleshooting

- **TypeLoadException from PgVector / Semantic Kernel**  
Ensure `Microsoft.SemanticKernel.Connectors.PgVector` and `Microsoft.Extensions.VectorData.*` share a compatible version line configured in `Directory.Packages.props`. Mismatches can lead to methods missing implementations at runtime.

- **“Hash collision detected” warnings** 
These usually mean *identical chunk content* was seen more than once, not a true SHA-256 collision. If you require uniqueness per physical chunk (even with identical content), include file path or a chunk identifier in the hash input or adjust the collision check logic.

- **Slow embedding performance**  
Tune:
- `ParallelOptions.MaxDegreeOfParallelism` in the embedding/processing loops.
- Ollama concurrency and resource limits.

- **Temporary directory not deleted** 
Check:
- `DeleteTemporaryFiles` configuration.
- Whether any process is still holding open file handles (especially on Windows).

If you encounter issues not covered here, consider opening an issue with logs, configuration snippets (minus secrets), and repro steps.
