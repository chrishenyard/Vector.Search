export interface ChunkMessage {
  filePath: string;
  status?: "started" | "processing" | "completed" | "error";
  message?: string;
  timestamp?: string;
  progress?: number;
  error?: string;
}

export interface ConsoleMessage {
  id: string;
  type: "system" | "api" | "signalr" | "error";
  message: string;
  timestamp: Date;
  data?: any;
}
