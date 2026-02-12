import { useMemo, useRef, useCallback } from "react";
import * as signalR from "@microsoft/signalr";

interface UseHubConnectionState {
  connection: signalR.HubConnection;
  connectionRef: React.RefObject<signalR.HubConnection | null>;
  startConnection: () => Promise<void>;
}

interface UseHubConnectionOptions {
  hubUrl: string;
  maxRetries?: number;
  keepAliveInterval?: number;
  serverTimeout?: number;
  onConnectionStateChange?: (state: string) => void;
  onRetryAttempt?: (attempt: number, message: string) => void;
  onCleanUp?: () => void;
}

export default function useHubConnection(
  options: UseHubConnectionOptions,
): UseHubConnectionState {
  const {
    hubUrl,
    maxRetries,
    keepAliveInterval = 30000,
    serverTimeout = 5000,
    onConnectionStateChange,
    onRetryAttempt,
    onCleanUp,
  } = options;
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const retryCountRef = useRef(0);

  const connection = useMemo(() => {
    if (!connectionRef.current) {
      connectionRef.current = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl) // proxied by Vite in dev
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            retryCountRef.current = retryContext.previousRetryCount + 1;

            onConnectionStateChange?.("reconnecting");
            onRetryAttempt?.(
              retryCountRef.current,
              `Connection lost. Attempting to reconnect... (Attempt ${retryCountRef.current}/${maxRetries})`,
            );

            if (retryCountRef.current >= (maxRetries ?? 5)) {
              onRetryAttempt?.(
                retryCountRef.current,
                `Connection lost. Retry attempts exceeded... (Attempt ${retryCountRef.current}/${maxRetries})`,
              );
              onCleanUp?.();
              return null;
            }

            return Math.pow(2, retryContext.previousRetryCount) * 1000;
          },
        })
        .configureLogging(signalR.LogLevel.Information)
        .withKeepAliveInterval(keepAliveInterval)
        .withServerTimeout(serverTimeout)
        .build();
    }
    return connectionRef.current;
  }, [hubUrl]);

  const startConnection = useCallback(async () => {
    if (!connection) return;

    if (connection.state === signalR.HubConnectionState.Disconnected) {
      connection.onreconnected((connectionId) => {
        console.log("SignalR reconnected", connectionId);
        retryCountRef.current = 0;
        onConnectionStateChange?.("ready");
      });

      console.log("Starting SignalR connection...");
      onConnectionStateChange?.("connecting");
      await connection.start();
      console.log("SignalR connection started");
    }
  }, [connection, onConnectionStateChange, onRetryAttempt, maxRetries]);

  return { connection, connectionRef, startConnection };
}
