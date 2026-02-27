# Vector.Search

Vector.Search is a .NET 10 solution for indexing source code into a vector store and exposing HTTP + SignalR endpoints that power semantic code search and related features. It scans a repository, chunks supported files, generates embeddings via [Ollama](https://github.com/ollama/ollama), and persists those embeddings using the Semantic Kernel PgVector connector on PostgreSQL. Clients can connect over SignalR to receive real-time progress as files are chunked and embedded.

## Features

- **Code-aware chunking pipeline**
  - Pluggable `IChunk` implementation (`CodeChunking`) that walks a repo and splits code files (e.g., C#) into semantically aligned chunks.
  - Configurable minimum chunk size and file extensions.
  - Stable hashing and GUID generation per chunk for idempotent indexing.

- **Embedding service**
  - Uses Ollama for both embedding and chat models, configured via `appsettings` / environment.
  - Processes chunks in parallel with backpressure via `ParallelOptions`.
  - Sends structured progress notifications (`ChunkProcessed`, `EmbeddingCompleted`, `EmbeddingError`) over SignalR.

- **Vector storage**
  - Integrates with `Microsoft.SemanticKernel.Connectors.PgVector` and `Microsoft.Extensions.VectorData.Abstractions`.
  - Uses `VectorStoreCollection<Guid, CodeChunkRecord>` (or equivalent) for upsert and search.
  - Designed to work with a PostgreSQL database with the `pgvector` extension enabled.

- **Diagnostics & Observability**
  - Minimal APIs for configuration and connectivity: `/debug/config`, `/debug/connection`, `/health/ollama`.
  - Structured logging via Serilog.
  - Optional OpenTelemetry integration for traces and metrics.
  - Background cleanup of temporary chunk files.

## Architecture Overview

At a high level:

1. A client calls the `POST /api/embed` endpoint with an `EmbedRequest` that includes a SignalR connection ID.
2. The service:
   - Notifies the client that embedding has started.
   - Kicks off a background task that:
     - Scans the configured repository.
     - Chunks supported files to a temporary directory.
     - Embeds each chunk using Ollama.
     - Upserts embedding records into the vector store.
     - Streams progress updates over SignalR.
3. On completion or failure, the client receives a final SignalR message.
4. Temporary chunk files can be deleted automatically based on configuration.

## HTTP Endpoints

- `GET /`  
  Health-style text endpoint: returns a simple “Vector search is running...” message.

- `GET /api/antiforgery/token`  
  Returns antiforgery tokens and sets an `XSRF-TOKEN` cookie.

- `GET /debug/config`  
  Returns high-level runtime configuration (Ollama URL, selected models, timeout).

- `GET /debug/connection`  
  Uses `IHttpClientFactory` to call `Ollama /api/tags` and returns connectivity details.

- `GET /health/ollama`  
  Uses the `OllamaApiClient` to list models and report Ollama health.

- `POST /api/embed`  
  Accepts an `EmbedRequest`, starts the background embedding process, and responds with `202 Accepted` and an operation ID. Progress is streamed via SignalR.

- `POST /api/code`  
  Simple echo endpoint to verify file upload handling; returns uploaded file name and length.

## Integration Tests

The solution includes integration tests to validate the system end-to-end:

- **Host startup**  
  Tests spin up the full ASP.NET Core host (using the same `Program`/`Startup` as production) with test configuration for:
  - Repository root
  - File extensions
  - Ollama endpoints
  - Database / vector store settings

- **Embedding pipeline tests**
  - Issue a `POST /api/embed` with a test `EmbedRequest`.
  - Confirm the API returns `202 Accepted` with an operation ID.
  - Connect a test SignalR client to capture:
    - `ChunkProcessed` events (per-chunk progress, file path, and count).
    - `EmbeddingCompleted` when the operation finishes.
    - `EmbeddingError` when there is a failure.
  - Optionally assert against the backing vector store (e.g., that records were persisted).

- **Diagnostics tests**
  - `GET /debug/config`: verifies configuration values are surfaced correctly.
  - `GET /debug/connection`: confirms connectivity to the configured Ollama instance and checks the response payload.
  - `GET /health/ollama`: ensures Ollama responds and the endpoint returns expected health data.

- **Failure paths**
  - Use misconfigured or unavailable dependencies in test configuration to ensure:
    - Errors are logged.
    - `EmbeddingError` messages are emitted appropriately.
    - The API still returns meaningful HTTP status codes.

Integration tests are run with:

They assume test infrastructure (PostgreSQL + pgvector, Ollama) is reachable with connection details defined in `appsettings.Test.json` or environment variables.

Ensure the test configuration points to a safe test database and test Ollama instance to avoid overwriting production data.
