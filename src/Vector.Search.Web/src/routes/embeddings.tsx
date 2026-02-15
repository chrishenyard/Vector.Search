import { createFileRoute } from "@tanstack/react-router";
import { useState, useCallback, useRef, useEffect } from "react";
import * as signalR from "@microsoft/signalr";
import useHubConnection from "../common/use-hub-connection";
import { ConsoleMessage } from "../types/chunk-message-types";
import apiClient from "../services/api";
import { ApiResponse } from "../types/api-response-types";

export const Route = createFileRoute("/embeddings")({
  component: RouteComponent,
});

function RouteComponent() {
  const [status, setStatus] = useState("disconnected");
  const [messages, setMessages] = useState<ConsoleMessage[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [globalErrors, setGlobalErrors] = useState<string[]>([]);
  const [retryAttempt, setRetryAttempt] = useState(0);
  const [retryMessage, setRetryMessage] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const attemptsRef = useRef(0);
  const maxRetries = 5;

  const addMessage = useCallback(
    (message: Omit<ConsoleMessage, "id" | "timestamp">) => {
      const newMessage: ConsoleMessage = {
        ...message,
        id: Date.now().toString() + Math.random().toString(36).substr(2, 9),
        timestamp: new Date(),
      };
      setMessages((prev) => [...prev, newMessage]);
    },
    [],
  );

  const addError = useCallback(
    (error: string) => {
      setGlobalErrors((prev) => [
        ...prev,
        `${new Date().toLocaleTimeString()}: ${error}`,
      ]);
      addMessage({
        type: "error",
        message: error,
      });
    },
    [addMessage],
  );

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
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
    (msg: { operationId: string; indexed: number }) => {
      if (!msg) {
        addMessage({
          type: "signalr",
          message: "Received empty complete message from SignalR.",
          data: msg,
        });
        return;
      }

      addMessage({
        type: "signalr",
        message: `${msg.operationId}: - Completed. Indexed ${msg.indexed} chunks.`,
        data: msg,
      });

      disconnectFromHub();
    },
    [],
  );

  const handleEmbeddingErrorMessage = useCallback(
    (msg: { operationId: string; error: string }) => {
      if (!msg) {
        addMessage({
          type: "signalr",
          message: "Received empty error message from SignalR.",
          data: msg,
        });
        return;
      }

      addMessage({
        type: "signalr",
        message: `${msg.operationId}: - Error: ${msg.error}`,
        data: msg,
      });

      disconnectFromHub();
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
    const attempts = attemptsRef.current;

    if (!currentConnection) return;

    try {
      if (
        currentConnection.state === signalR.HubConnectionState.Connected ||
        (currentConnection.state === signalR.HubConnectionState.Reconnecting &&
          attempts >= maxRetries)
      ) {
        // Remove all event handlers before stopping
        currentConnection.off("ChunkProcessed");
        currentConnection.off("EmbedCompleted");
        await currentConnection.stop();
        console.log("Connection cleaned up successfully");
      }
    } catch (err) {
      addError(
        `Connection cleanup failed: ${err instanceof Error ? err.message : String(err)}`,
      );
    }
  }, [addError]);

  const { connection, connectionRef, startConnection, stopConnection } =
    useHubConnection({
      hubUrl: "/embedhub",
      maxRetries: maxRetries,
      keepAliveInterval: 15000, // 15 seconds - more frequent keep-alive
      serverTimeout: 30000, // 30 seconds - reduced timeout
      onConnectionStateChange: setStatus,
      onRetryAttempt: handleRetryAttempt,
      onCleanUp: cleanUp,
    });

  // Clean up connection on unmount
  useEffect(() => {
    return () => {
      cleanUp();
    };
  }, [cleanUp]);

  const connectToHub = async () => {
    try {
      if (connection.state === signalR.HubConnectionState.Disconnected) {
        addMessage({
          type: "system",
          message: "Connecting to SignalR hub...",
        });

        connection.off("ChunkProcessed");
        connection.off("EmbeddingCompleted");
        connection.off("EmbeddingError");
        connection.on("ChunkProcessed", (m) => handleChunkMessage(m));
        connection.on("EmbeddingCompleted", (m) =>
          handleEmbeddingCompleteMessage(m),
        );
        connection.on("EmbeddingError", (m) => handleEmbeddingErrorMessage(m));
        await startConnection();

        addMessage({
          type: "system",
          message: "Connected to SignalR hub. Ready to monitor embeddings.",
        });
      } else {
        addMessage({
          type: "system",
          message: `SignalR hub already in state: ${connection.state}`,
        });
      }
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      addError(`Failed to connect to SignalR hub: ${errorMsg}`);
      throw err;
    }
  };

  const disconnectFromHub = async () => {
    try {
      if (connection.state !== signalR.HubConnectionState.Disconnected) {
        addMessage({
          type: "system",
          message: "Disconnecting from SignalR hub...",
        });

        connection.off("ChunkProcessed");
        await stopConnection();

        addMessage({
          type: "system",
          message: "Disconnected from SignalR hub.",
        });
      }
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      addError(`Failed to disconnect from SignalR hub: ${errorMsg}`);
    }
  };

  const reconnectToHub = async () => {
    try {
      addMessage({
        type: "system",
        message: "Reconnecting to SignalR hub...",
      });

      await disconnectFromHub();

      // Wait a moment before reconnecting to ensure cleanup is complete
      await new Promise((resolve) => setTimeout(resolve, 1000));

      await connectToHub();
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      addError(`Failed to reconnect to SignalR hub: ${errorMsg}`);
    }
  };

  const startEmbedding = async () => {
    if (isProcessing) return;

    setIsProcessing(true);
    addMessage({
      type: "api",
      message: "Starting embedding process...",
    });

    try {
      // Connect to SignalR after successful API call
      if (connection.state === signalR.HubConnectionState.Disconnected) {
        await connectToHub();
      }

      const response = (await apiClient.post("/api/embed")) as ApiResponse<any>;

      if (response.ok) {
        addMessage({
          type: "api",
          message: `Embedding process accepted and started (HTTP ${response.status})`,
        });
      } else {
        const errorMsg = `HTTP ${response.status}: ${response.statusText}`;
        addError(`Failed to start embedding process: ${errorMsg}`);
        await disconnectFromHub();
      }
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      addError(`API call failed: ${errorMsg}`);
    }

    setIsProcessing(false);
  };

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
    <div className="h-screen flex flex-col bg-gray-900 text-gray-100">
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
            {globalErrors.slice(-5).map((error, index) => (
              <div key={index} className="text-red-300 text-sm font-mono">
                {error}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Console Messages */}
      <div className="flex-1 overflow-y-auto p-4 bg-black font-mono text-sm">
        {messages.length === 0 ? (
          <div className="text-gray-500 italic">
            Console ready. Click "Start Embedding" to begin processing and
            connect to SignalR.
          </div>
        ) : (
          messages.map((msg) => (
            <div key={msg.id} className="mb-1 flex">
              <span className="text-gray-500 mr-2 flex-shrink-0">
                {formatTimestamp(msg.timestamp)}
              </span>
              <span
                className={`mr-2 flex-shrink-0 font-bold ${getMessageColor(msg.type)}`}
              >
                {getTypePrefix(msg.type)}
              </span>
              <span className={getMessageColor(msg.type)}>{msg.message}</span>
            </div>
          ))
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Status Bar */}
      <div className="bg-gray-800 p-2 border-t border-gray-700 text-xs text-gray-400 flex justify-between">
        <span>Messages: {messages.length}</span>
        <span>Errors: {globalErrors.length}</span>
        <span>Hub: /embedhub</span>
      </div>
    </div>
  );
}
