import { createFileRoute } from "@tanstack/react-router";
import { useState, useCallback, useRef, useEffect } from "react";
import * as signalR from "@microsoft/signalr";
import useHubConnection from "../common/use-hub-connection";
import { ChunkMessage, ConsoleMessage } from "../types/chunk-message-types";
import apiClient from "../services/api";

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
    (chunkMessage: ChunkMessage) => {
      addMessage({
        type: "signalr",
        message: `${chunkMessage.status?.toUpperCase() || "PROCESSING"}: ${chunkMessage.filePath}${chunkMessage.message ? ` - ${chunkMessage.message}` : ""}`,
        data: chunkMessage,
      });

      if (chunkMessage.error) {
        addError(chunkMessage.error);
      }

      if (
        chunkMessage.status === "completed" ||
        chunkMessage.status === "error"
      ) {
        setIsProcessing(false);
      }
    },
    [addMessage, addError],
  );

  const handleRetryAttempt = useCallback(
    (attempt: number, message: string) => {
      attemptsRef.current = attempt;
      setRetryAttempt(attempt);
      setRetryMessage(message);
      addMessage({
        type: "system",
        message: message,
      });
    },
    [addMessage],
  );

  const cleanUp = useCallback(() => {
    const connection = connectionRef.current;
    const attempts = attemptsRef.current;

    if (!connection) return;

    if (
      connection.state === signalR.HubConnectionState.Connected ||
      (connection.state === signalR.HubConnectionState.Reconnecting &&
        attempts >= maxRetries)
    ) {
      connection.off("ChunkProcessed");
      connection.stop().catch((err) => {
        addError(
          `Connection cleanup failed: ${err instanceof Error ? err.message : String(err)}`,
        );
      });
      connectionRef.current = null;
    }
  }, [addError]);

  const { connection, connectionRef, startConnection } = useHubConnection({
    hubUrl: "/embedhub",
    maxRetries: maxRetries,
    onConnectionStateChange: setStatus,
    onRetryAttempt: handleRetryAttempt,
    onCleanUp: cleanUp,
    keepAliveInterval: 30000, // 30 seconds - more lenient for long-running operations
    serverTimeout: 60000, // 60 seconds - wait longer for server response
  });

  useEffect(() => {
    (async () => {
      try {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
          addMessage({
            type: "system",
            message: "Connecting to SignalR hub...",
          });

          connection.on("ChunkProcessed", handleChunkMessage);
          await startConnection();
          setStatus("connected");

          addMessage({
            type: "system",
            message: "Connected to SignalR hub. Ready to process embeddings.",
          });
        }
      } catch (err) {
        const errorMsg = err instanceof Error ? err.message : String(err);
        addError(`Failed to connect to SignalR hub: ${errorMsg}`);
      }
    })();

    return () => {
      cleanUp();
    };
  }, [
    connection,
    startConnection,
    handleChunkMessage,
    addMessage,
    addError,
    cleanUp,
  ]);

  const startEmbedding = async () => {
    if (isProcessing) return;

    setIsProcessing(true);
    addMessage({
      type: "api",
      message: "Starting embedding process...",
    });

    const response = await apiClient.post("/api/embed");

    if (response.status === 200 || response.status === 202) {
      addMessage({
        type: "api",
        message: `Embedding process accepted and started (HTTP ${response.status})`,
      });
    } else {
      const errorMsg = `HTTP ${response.status}: ${response.statusText}`;
      addError(`Failed to start embedding process: ${errorMsg}`);
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
            disabled={isProcessing || status !== "connected"}
            className={`px-4 py-2 rounded font-medium ${
              isProcessing || status !== "connected"
                ? "bg-gray-600 text-gray-400 cursor-not-allowed"
                : "bg-blue-600 hover:bg-blue-700 text-white"
            }`}
          >
            {isProcessing ? "Processing..." : "Start Embedding"}
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
            Console ready. Click "Start Embedding" to begin processing.
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
