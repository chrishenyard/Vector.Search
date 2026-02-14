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

export interface ApiResponse<T> {
  data: T;
  status: number;
  statusText: string;
  headers: (H & RawAxiosResponseHeaders) | AxiosResponseHeaders;
  config: InternalAxiosRequestConfig<D>;
  request?: any;
  ok: boolean;
}
