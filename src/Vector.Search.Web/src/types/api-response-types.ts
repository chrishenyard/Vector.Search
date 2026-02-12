export interface ApiError {
  detail?: string;
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export interface ValidationError {
  field: string;
  message: string;
}