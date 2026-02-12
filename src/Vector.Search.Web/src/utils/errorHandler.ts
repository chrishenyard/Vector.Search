import { AxiosError } from 'axios';
import { ApiError } from '../types/ApiResponse';

export const getErrorMessage = (error: unknown, defaultMessage: string = 'An unexpected error occurred'): string => {
  const axiosError = error as AxiosError<ApiError>;
  
  if (axiosError.response) {
    if (axiosError.response.data?.errors) {
      const fieldErrors = axiosError.response.data.errors;
      return Object.values(fieldErrors).flat().join(' ');
    }
    const detail = axiosError.response.data?.detail;
    if (detail) {
      return detail;
    }
    return axiosError.response.data.title as string || defaultMessage;
  } else if (axiosError.request) {
    return 'No response from server.';
  } else {
    return `Request error: ${axiosError.message}`;
  }
};
