import axios, { AxiosInstance, InternalAxiosRequestConfig } from "axios";

const apiClient: AxiosInstance = axios.create({
  timeout: 300000, // 5 minutes
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true, // Required for CORS with credentials
});

// Function to get CSRF token from cookie
const getCsrfToken = (): string | null => {
  const name = "XSRF-TOKEN=";
  const decodedCookie = decodeURIComponent(document.cookie);
  const cookieArray = decodedCookie.split(";");

  for (let i = 0; i < cookieArray.length; i++) {
    let cookie = cookieArray[i].trim();
    if (cookie.indexOf(name) === 0) {
      return cookie.substring(name.length, cookie.length);
    }
  }
  return null;
};

// Function to fetch CSRF token
export const fetchCsrfToken = async (): Promise<void> => {
  try {
    await apiClient.get("/api/antiforgery/token");
    console.log("CSRF token fetched and stored in cookie");
  } catch (error) {
    console.error("Failed to fetch CSRF token:", error);
  }
};

// Add request interceptor to include CSRF token
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    // Add CSRF token to header for state-changing requests
    if (
      config.method &&
      ["post", "put", "patch", "delete"].includes(config.method.toLowerCase())
    ) {
      const token = getCsrfToken();
      if (token) {
        config.headers["X-XSRF-TOKEN"] = token;
      }
    }

    console.log("API Request:", {
      method: config.method,
      url: config.url,
      baseURL: config.baseURL,
      headers: config.headers,
    });
    return config;
  },
  (error) => {
    console.error("API Request Error:", error);
    return Promise.reject(error);
  },
);

// Add response interceptor for debugging
apiClient.interceptors.response.use(
  (response) => {
    console.log("API Response:", {
      status: response.status,
      url: response.config.url,
      data: response.data,
    });
    const apiResponse = {
      data: response.data,
      status: response.status,
      statusText: response.statusText,
      headers: response.headers,
      config: response.config,
      request: response.request,
      ok: response.status >= 200 && response.status < 300,
    };
    return apiResponse;
  },
  (error) => {
    console.error("API Response Error:", {
      message: error.message,
      response: error.response?.data,
      status: error.response?.status,
    });
    return Promise.reject(error);
  },
);

export default apiClient;
