import { createFileRoute } from "@tanstack/react-router";
import { AxiosError } from "axios";
import { useEffect, useMemo, useState } from "react";
import apiClient, { fetchCsrfToken } from "../services/api";
import { ApiError, ApiResponse } from "../types/api-response-types";
import {
  AskRequest,
  AskResponse,
  CodeChunk,
  SearchResponse,
} from "../types/ask-types";

export const Route = createFileRoute("/code-search")({
  component: RouteComponent,
});

type SubmitState = "idle" | "loading" | "success" | "error";

function asRecord(value: unknown): Record<string, unknown> | null {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    return value as Record<string, unknown>;
  }
  return null;
}

function asString(value: unknown, fallback = ""): string {
  return typeof value === "string" ? value : fallback;
}

function asNullableNumber(value: unknown): number | null {
  if (typeof value === "number") {
    return Number.isFinite(value) ? value : null;
  }

  if (typeof value === "string") {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function normalizeChunk(value: unknown): CodeChunk {
  const chunk = asRecord(value) ?? {};

  return {
    id: asString(chunk.id ?? chunk.Id, "unknown"),
    filename: asString(chunk.filename ?? chunk.Filename, "unknown"),
    language: asString(chunk.language ?? chunk.Language, "unknown"),
    content: asString(chunk.content ?? chunk.Content),
    hash: asString(chunk.hash ?? chunk.Hash),
    embedding: null,
  };
}

function normalizeSearchResponse(value: unknown): SearchResponse {
  const response = asRecord(value) ?? {};

  return {
    chunk: normalizeChunk(response.chunk ?? response.Chunk),
    score: asNullableNumber(response.score ?? response.Score),
  };
}

function normalizeAskResponse(value: unknown): AskResponse {
  const response = asRecord(value) ?? {};
  const searchResponsesRaw =
    response.searchResonses ??
    response.searchResponses ??
    response.SearchResonses ??
    response.SearchResponses;
  const searchResonses = Array.isArray(searchResponsesRaw)
    ? searchResponsesRaw.map((item) => normalizeSearchResponse(item))
    : [];

  return {
    answer: asString(response.answer ?? response.Answer),
    searchResonses,
  };
}

function getErrorMessage(error: unknown): string {
  const axiosError = error as AxiosError<ApiError>;

  if (axiosError.response) {
    const data = axiosError.response.data;

    if (data?.errors) {
      const fieldErrors = Object.values(data.errors).flat();
      if (fieldErrors.length > 0) {
        return fieldErrors.join(" ");
      }
    }

    if (data?.detail) {
      return data.detail;
    }

    if (data?.title) {
      return data.title;
    }

    return `Request failed with status ${axiosError.response.status}.`;
  }

  if (axiosError.request) {
    return "No response from server. Please verify the API is running.";
  }

  return axiosError.message || "An unexpected error occurred.";
}

function RouteComponent() {
  const [question, setQuestion] = useState("");
  const [topK, setTopK] = useState(8);
  const [submitState, setSubmitState] = useState<SubmitState>("idle");
  const [errorMessage, setErrorMessage] = useState("");
  const [result, setResult] = useState<AskResponse | null>(null);

  useEffect(() => {
    void fetchCsrfToken();
  }, []);

  const isSubmitting = submitState === "loading";
  const canSubmit = useMemo(
    () =>
      question.trim().length > 0 && topK >= 1 && topK <= 50 && !isSubmitting,
    [question, topK, isSubmitting],
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!question.trim()) {
      setSubmitState("error");
      setErrorMessage("Please enter a question before searching.");
      return;
    }

    const payload: AskRequest = {
      question: question.trim(),
      topK,
    };

    setSubmitState("loading");
    setErrorMessage("");

    try {
      const response = (await apiClient.post(
        "/api/ask",
        payload,
      )) as ApiResponse<unknown>;
      const askResponse = normalizeAskResponse(response.data);

      setResult(askResponse);
      setSubmitState("success");
    } catch (error) {
      setSubmitState("error");
      setResult(null);
      setErrorMessage(getErrorMessage(error));
    }
  }

  return (
    <section className="mx-auto flex h-full w-full max-w-6xl flex-col gap-6">
      <header>
        <h2 className="text-3xl font-bold text-white">Code Search</h2>
        <p className="mt-2 text-sm text-gray-300">
          Ask a question about your codebase and review the most relevant
          chunks.
        </p>
      </header>

      <form
        onSubmit={handleSubmit}
        className="rounded-lg border border-gray-700 bg-gray-800/80 p-6"
      >
        <div className="grid gap-4 md:grid-cols-[1fr_120px_auto] md:items-end">
          <label className="flex flex-col gap-2">
            <span className="text-sm font-medium text-gray-200">Question</span>
            <textarea
              value={question}
              onChange={(event) => setQuestion(event.target.value)}
              rows={3}
              placeholder="Example: Where is authentication middleware configured?"
              className="w-full rounded-md border border-gray-600 bg-gray-900 px-3 py-2 text-sm text-gray-100 outline-none transition-colors focus:border-blue-500"
            />
          </label>

          <label className="flex flex-col gap-2">
            <span className="text-sm font-medium text-gray-200">Top K</span>
            <input
              type="number"
              min={1}
              max={50}
              value={topK}
              onChange={(event) => {
                const parsed = Number(event.target.value);
                if (Number.isFinite(parsed)) {
                  setTopK(parsed);
                }
              }}
              className="rounded-md border border-gray-600 bg-gray-900 px-3 py-2 text-sm text-gray-100 outline-none transition-colors focus:border-blue-500"
            />
          </label>

          <button
            type="submit"
            disabled={!canSubmit}
            className={`h-10.5 rounded-md px-4 py-2 text-sm font-semibold text-white transition-colors ${
              canSubmit
                ? "bg-blue-600 hover:bg-blue-700"
                : "cursor-not-allowed bg-gray-600"
            }`}
          >
            {isSubmitting ? "Searching..." : "Search"}
          </button>
        </div>
      </form>

      {submitState === "error" && (
        <div className="rounded-lg border border-red-500/50 bg-red-900/20 p-4 text-sm text-red-200">
          {errorMessage}
        </div>
      )}

      {submitState === "success" && result && (
        <section className="flex min-h-0 flex-1 flex-col gap-4 overflow-hidden">
          <div className="rounded-lg border border-gray-700 bg-gray-800 p-4">
            <h3 className="mb-2 text-lg font-semibold text-white">Answer</h3>
            <p className="whitespace-pre-wrap text-sm text-gray-200">
              {result.answer}
            </p>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto rounded-lg border border-gray-700 bg-gray-800 p-4">
            <h3 className="mb-4 text-lg font-semibold text-white">
              Matched Chunks
            </h3>

            {result.searchResonses.length === 0 ? (
              <p className="text-sm text-gray-300">
                No chunks were returned for this query.
              </p>
            ) : (
              <ul className="space-y-3">
                {result.searchResonses.map((searchResponse, index) => (
                  <li
                    key={`${searchResponse.chunk.id}-${index}`}
                    className="rounded-md border border-gray-700 bg-gray-900/70 p-3"
                  >
                    <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                      <span className="font-mono text-xs text-blue-300">
                        {searchResponse.chunk.filename}
                      </span>
                      <div className="flex items-center gap-2 text-xs text-gray-300">
                        <span className="rounded bg-gray-700 px-2 py-1">
                          {searchResponse.chunk.language}
                        </span>
                        <span>
                          score:{" "}
                          {searchResponse.score === null
                            ? "n/a"
                            : searchResponse.score?.toFixed(4)}
                        </span>
                      </div>
                    </div>
                    <pre className="max-h-48 overflow-auto whitespace-pre-wrap rounded bg-black/30 p-2 text-xs text-gray-200">
                      {searchResponse.chunk.content}
                    </pre>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </section>
      )}
    </section>
  );
}
