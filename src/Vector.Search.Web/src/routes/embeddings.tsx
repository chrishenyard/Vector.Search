import { createFileRoute } from "@tanstack/react-router";
import { useState, useCallback, useRef, useEffect } from "react";
import * as signalR from "@microsoft/signalr";
import useHubConnection from "../common/use-hub-connection";
import { ConsoleMessage } from "../types/chunk-message-types";
import apiClient from "../services/api";
import { ApiResponse } from "../types/api-response-types";

export const Route = createFileRoute("/embeddings")({
  component: EmbeddingsRoute,
});

function EmbeddingsRoute() {
  const [status, setStatus] = useState("disconnected");
  const [messages, setMessages] = useState<ConsoleMessage[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [globalErrors, setGlobalErrors] = useState<string[]>([]);
  const [retryAttempt, setRetryAttempt] = useState(0);
  const [retryMessage, setRetryMessage] = useState("");
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const attemptsRef = useRef(0);
  const maxRetries = 5;

  // Maximum number of messages to keep in the console buffer
  const MAX_CONSOLE_MESSAGES = 150;
  // Maximum number of errors to keep in the global errors buffer
  const MAX_GLOBAL_ERRORS = 50;

  const addMessage = useCallback(
    (message: Omit<ConsoleMessage, "id" | "timestamp">) => {
      const newMessage: ConsoleMessage = {
        ...message,
        id: Date.now().toString() + Math.random().toString(36).substr(2, 9),
        timestamp: new Date(),
      };
      setMessages((prev) => {
        const newMessages = [...prev, newMessage];
        // Keep only the most recent messages, removing old ones if we exceed the limit
        return newMessages.length > MAX_CONSOLE_MESSAGES
          ? newMessages.slice(-MAX_CONSOLE_MESSAGES)
          : newMessages;
      });
    },
    [],
  );

  const addError = useCallback(
    (error: string) => {
      const timestampedError = `${new Date().toLocaleTimeString()}: ${error}`;
      setGlobalErrors((prev) => {
        const newErrors = [...prev, timestampedError];
        // Keep only the most recent errors, removing old ones if we exceed the limit
        return newErrors.length > MAX_GLOBAL_ERRORS
          ? newErrors.slice(-MAX_GLOBAL_ERRORS)
          : newErrors;
      });
      addMessage({
        type: "error",
        message: error,
      });
    },
    [addMessage],
  );

  const scrollToBottom = () => {
    const container = messagesContainerRef.current;
    if (!container) return;

    container.scrollTop = container.scrollHeight;
  };

  useEffect(scrollToBottom, [messages]);

  const handleChunkMessage = useCallback(
    (msg: { operationId: string; filePath: string }) => {
      if (!msg) {
        addMessage({
          type: "signalr",
          message: "Received empty chunk message from SignalR.",
          data: msg,
        });
        return;
      }

      addMessage({
        type: "signalr",
        message: `${msg.operationId}: ${msg.filePath}.`,
        data: msg,
      });
    },
    [],
  );

  const handleEmbeddingCompleteMessage = useCallback(
    async (msg: { operationId: string; indexed: number }) => {
      if (!msg) {
        addMessage({
          type: "signalr",
          message: "Received empty complete message from SignalR.",
          data: msg,
        });
        return;
      }

      try {
        await disconnectFromHub();
        addMessage({
          type: "signalr",
          message: `${msg.operationId}: - Completed. Indexed ${msg.indexed} chunks.`,
          data: msg,
        });
      } catch (err) {
        const errorMsg = err instanceof Error ? err.message : String(err);
        addError(
          `Error disconnecting from hub after embedding completion: ${errorMsg}`,
        );
      }
    },
    [],
  );

  const handleEmbeddingErrorMessage = useCallback(
    async (msg: { operationId: string; error: string }) => {
      if (!msg) {
        addMessage({
          type: "signalr",
          message: "Received empty error message from SignalR.",
          data: msg,
        });
        return;
      }

      try {
        await disconnectFromHub();
        addMessage({
          type: "signalr",
          message: `${msg.operationId}: - Error: ${msg.error}`,
          data: msg,
        });
      } catch (err) {
        const errorMsg = err instanceof Error ? err.message : String(err);
        addError(
          `Error disconnecting from hub after embedding error: ${errorMsg}`,
        );
      }
    },
    [],
  );

  const handleRetryAttempt = useCallback((attempt: number, message: string) => {
    attemptsRef.current = attempt;
    setRetryAttempt(attempt);
    setRetryMessage(message);
    addMessage({
      type: "system",
      message: message,
    });
  }, []);

  const cleanUp = useCallback(async () => {
    const currentConnection = connectionRef.current;

    if (!currentConnection) return;

    window.removeEventListener("beforeunload", handleBeforeUnload);

    try {
      await disconnectFromHub();
      addMessage({
        type: "system",
        message: "Cleaned up SignalR connection and event listeners.",
      });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      addError(`Error during cleanup: ${errorMsg}`);
    }
  }, []);

  const handleBeforeUnload = (event: BeforeUnloadEvent) => {
    event.preventDefault();
    cleanUp();
  };

  const reconnectToHub = useCallback(async () => {
    try {
      addMessage({
        type: "system",
        message: "Reconnecting to SignalR hub...",
      });

      await disconnectFromHub();

      // Wait a moment before reconnecting to ensure cleanup is complete
      await new Promise((resolve) => setTimeout(resolve, 1000));
      await connectToHub();

      addMessage({
        type: "system",
        message: "Reconnected to SignalR hub successfully.",
      });
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      addError(`Failed to reconnect to SignalR hub: ${errorMsg}`);
    }
  }, []);

  const disconnectFromHub = useCallback(async () => {
    addMessage({
      type: "system",
      message: "Disconnecting from SignalR hub...",
    });

    connection.off("ChunkProcessed");
    connection.off("EmbeddingCompleted");
    connection.off("EmbeddingError");
    await stopConnection();

    addMessage({
      type: "system",
      message: "Disconnected from SignalR hub.",
    });
  }, []);

  const connectToHub = useCallback(async () => {
    await disconnectFromHub();

    if (connection.state === signalR.HubConnectionState.Disconnected) {
      connection.on("ChunkProcessed", (m) => handleChunkMessage(m));
      connection.on("EmbeddingCompleted", (m) =>
        handleEmbeddingCompleteMessage(m),
      );
      connection.on("EmbeddingError", (m) => handleEmbeddingErrorMessage(m));

      await startConnection();

      const msg: string = await connection.invoke("Start");
      console.log("Start method invoked on hub, response:", msg);

      addMessage({
        type: "system",
        message: "Connected to SignalR hub. Ready to monitor embeddings.",
      });
    }
  }, []);

  const startEmbedding = useCallback(async () => {
    if (isProcessing) return;

    setIsProcessing(true);
    addMessage({
      type: "api",
      message: "Starting embedding process...",
    });

    try {
      const healthy = (await apiClient.get("/health")) as ApiResponse<any>;

      if (!healthy.ok) {
        const errorMsg = `API health check failed: ${healthy.statusText}`;
        addError(errorMsg);
        setIsProcessing(false);
        return;
      }

      await connectToHub();
      const connectionId = connection.connectionId;

      const response = (await apiClient.post("/api/embed", {
        connectionId,
      })) as ApiResponse<any>;

      if (response.ok) {
        addMessage({
          type: "api",
          message: `Embedding process accepted and started (HTTP ${response.status})`,
        });
      } else {
        const errorMsg = `HTTP ${response.status}: ${response.statusText}`;
        addError(`Failed to start embedding process: ${errorMsg}`);
      }
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      addError(`API call failed: ${errorMsg}`);
    } finally {
      setIsProcessing(false);
    }
  }, []);

  const clearConsole = () => {
    setMessages([]);
  };

  const clearErrors = () => {
    setGlobalErrors([]);
  };

  const formatTimestamp = (timestamp: Date) => {
    return timestamp.toLocaleTimeString("en-US", {
      hour12: false,
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      fractionalSecondDigits: 3,
    });
  };

  const { connection, connectionRef, startConnection, stopConnection } =
    useHubConnection({
      hubUrl: "/embeddings-hub",
      maxRetries: maxRetries,
      keepAliveInterval: 15000, // 15 seconds - more frequent keep-alive
      serverTimeout: 120000, // 120 seconds - increased timeout
      onConnectionStateChange: setStatus,
      onRetryAttempt: handleRetryAttempt,
      onCleanUp: cleanUp,
    });

  window.addEventListener("beforeunload", handleBeforeUnload);

  const getMessageColor = (type: ConsoleMessage["type"]) => {
    switch (type) {
      case "system":
        return "text-blue-400";
      case "api":
        return "text-green-400";
      case "signalr":
        return "text-yellow-400";
      case "error":
        return "text-red-400";
      default:
        return "text-gray-300";
    }
  };

  const getTypePrefix = (type: ConsoleMessage["type"]) => {
    switch (type) {
      case "system":
        return "[SYS]";
      case "api":
        return "[API]";
      case "signalr":
        return "[HUB]";
      case "error":
        return "[ERR]";
      default:
        return "[LOG]";
    }
  };

  return (
    <div className="w-full h-full flex flex-col shadow-2xl rounded-lg overflow-hidden bg-gray-900 text-gray-100">
      <div className="bg-gray-800 p-4 border-b border-gray-700 flex justify-between items-center">
        <div className="flex items-center space-x-4">
          <h2 className="text-xl font-bold text-white">Embeddings Console</h2>
          <div className="flex items-center space-x-2">
            <div
              className={`w-3 h-3 rounded-full ${
                status === "connected"
                  ? "bg-green-500"
                  : status === "connecting" || status === "reconnecting"
                    ? "bg-yellow-500"
                    : "bg-red-500"
              }`}
            ></div>
            <span className="text-sm text-gray-300">
              {status === "connected"
                ? "Connected"
                : status === "connecting"
                  ? "Connecting..."
                  : status === "reconnecting"
                    ? `Reconnecting (${retryMessage} ${retryAttempt}/${maxRetries})...`
                    : "Disconnected"}
            </span>
          </div>
        </div>
        <div className="flex space-x-2">
          <button
            onClick={startEmbedding}
            disabled={isProcessing}
            className={`px-4 py-2 rounded font-medium ${
              isProcessing
                ? "bg-gray-600 text-gray-400 cursor-not-allowed"
                : "bg-blue-600 hover:bg-blue-700 text-white"
            }`}
          >
            {isProcessing ? "Processing..." : "Start Embedding"}
          </button>
          <button
            onClick={reconnectToHub}
            disabled={
              isProcessing ||
              status === "connecting" ||
              status === "reconnecting"
            }
            className={`px-3 py-2 rounded font-medium ${
              isProcessing ||
              status === "connecting" ||
              status === "reconnecting"
                ? "bg-gray-600 text-gray-400 cursor-not-allowed"
                : "bg-green-600 hover:bg-green-700 text-white"
            }`}
          >
            Reconnect Hub
          </button>
          <button
            onClick={disconnectFromHub}
            disabled={isProcessing || status === "disconnected"}
            className={`px-3 py-2 rounded font-medium ${
              isProcessing || status === "disconnected"
                ? "bg-gray-600 text-gray-400 cursor-not-allowed"
                : "bg-red-600 hover:bg-red-700 text-white"
            }`}
          >
            Disconnect Hub
          </button>
          <button
            onClick={clearConsole}
            className="px-3 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded font-medium"
          >
            Clear
          </button>
        </div>
      </div>

      {/* Global Errors Section */}
      {globalErrors.length > 0 && (
        <div className="bg-red-900 border-b border-red-700 p-3">
          <div className="flex justify-between items-center">
            <h3 className="text-red-200 font-medium">
              Global Errors ({globalErrors.length})
            </h3>
            <button
              onClick={clearErrors}
              className="text-red-300 hover:text-red-100 underline text-sm"
            >
              Clear Errors
            </button>
          </div>
          <div className="mt-2 max-h-20 overflow-y-auto">
            {globalErrors.map((error, index) => (
              <div key={index} className="text-red-300 text-sm font-mono">
                {error}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Console Messages */}
      <div
        ref={messagesContainerRef}
        className="flex-1 min-h-0 min-w-0 p-4 bg-black font-mono text-sm overflow-y-auto overflow-x-hidden terminal-scroll"
      >
        {messages.length === 0 ? (
          <div className="text-gray-500 italic">
            Console ready. Click "Start Embedding" to begin processing and
            connect to SignalR.
          </div>
        ) : (
          messages.map((msg) => (
            <div key={msg.id} className="mb-1 flex min-w-0 items-start w-full">
              <span className="text-gray-500 mr-2 shrink-0">
                {formatTimestamp(msg.timestamp)}
              </span>
              <span
                className={`mr-2 shrink-0 font-bold ${getMessageColor(
                  msg.type,
                )}`}
              >
                {getTypePrefix(msg.type)}
              </span>
              <span
                className={`flex-1 min-w-0 ${getMessageColor(
                  msg.type,
                )} whitespace-pre-wrap`}
                style={{ overflowWrap: "anywhere" }}
              >
                {msg.message}
              </span>
            </div>
          ))
        )}
      </div>

      {/* Status Bar */}
      <div className="bg-gray-800 p-2 border-t border-gray-700 text-xs text-gray-400 flex justify-between">
        <span>
          Messages: {messages.length}
          {messages.length >= MAX_CONSOLE_MESSAGES
            ? ` (showing last ${MAX_CONSOLE_MESSAGES})`
            : ""}
        </span>
        <span>
          Errors: {globalErrors.length}
          {globalErrors.length >= MAX_GLOBAL_ERRORS
            ? ` (showing last ${MAX_GLOBAL_ERRORS})`
            : ""}
        </span>
        <span>Hub: /embeddings-hub</span>
      </div>
    </div>
  );
}
