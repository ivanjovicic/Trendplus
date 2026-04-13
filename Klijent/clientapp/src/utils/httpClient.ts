import { apiCircuitBreaker, CircuitBreakerError } from "./circuitBreaker";
import { ApiException, parseApiError, getErrorMessage } from "./apiErrors";
import { API_COLD_START_TIMEOUT_MS } from "./apiTimeouts";

const API = import.meta.env.VITE_API_BASE_URL || "";

interface RequestConfig extends RequestInit {
    timeout?: number;
    skipCircuitBreaker?: boolean;
}

/**
 * HTTP Client with circuit breaker, error handling, and logging
 */
class HttpClient {
    private getUrl(path: string): string {
        return import.meta.env.DEV ? path : `${API}${path}`;
    }

    private async executeRequest(
        url: string,
        config: RequestConfig
    ): Promise<Response> {
        const { timeout = API_COLD_START_TIMEOUT_MS, skipCircuitBreaker = false, ...fetchConfig } = config;

        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), timeout);

        try {
            const execute = async () => {
                const response = await fetch(url, {
                    ...fetchConfig,
                    signal: controller.signal,
                    headers: {
                        "Content-Type": "application/json",
                        ...fetchConfig.headers,
                    },
                });

                // Log response
                console.log(`?? ${fetchConfig.method || "GET"} ${url} ? ${response.status}`);

                // Handle errors
                if (!response.ok) {
                    throw await parseApiError(response);
                }

                return response;
            };

            if (skipCircuitBreaker) {
                return await execute();
            }

            return await apiCircuitBreaker.execute(execute);
        } catch (error) {
            if (error instanceof ApiException) {
                console.error(`? API Error [${error.errorCode}]: ${error.message}`);
                throw error;
            }
            if (error instanceof CircuitBreakerError) {
                console.warn(`?? Circuit breaker: ${error.message}`);
                throw error;
            }
            if (error instanceof DOMException && error.name === "AbortError") {
                console.error(`?? Request timeout: ${url}`);
                throw new Error("Request timeout");
            }
            console.error(`? Request failed: ${getErrorMessage(error)}`);
            throw error;
        } finally {
            clearTimeout(timeoutId);
        }
    }

    async get<T>(path: string, config?: RequestConfig): Promise<T> {
        const response = await this.executeRequest(this.getUrl(path), {
            method: "GET",
            ...config,
        });
        return response.json();
    }

    async post<T>(path: string, data?: unknown, config?: RequestConfig): Promise<T> {
        const response = await this.executeRequest(this.getUrl(path), {
            method: "POST",
            body: data ? JSON.stringify(data) : undefined,
            ...config,
        });
        
        // Handle 204 No Content
        if (response.status === 204) {
            return {} as T;
        }
        
        return response.json();
    }

    async put<T>(path: string, data?: unknown, config?: RequestConfig): Promise<T> {
        const response = await this.executeRequest(this.getUrl(path), {
            method: "PUT",
            body: data ? JSON.stringify(data) : undefined,
            ...config,
        });
        
        if (response.status === 204) {
            return {} as T;
        }
        
        return response.json();
    }

    async delete<T>(path: string, config?: RequestConfig): Promise<T> {
        const response = await this.executeRequest(this.getUrl(path), {
            method: "DELETE",
            ...config,
        });
        
        if (response.status === 204) {
            return {} as T;
        }
        
        return response.json();
    }
}

// Singleton instance
export const httpClient = new HttpClient();

// Re-export error utilities
export { ApiException, getErrorMessage, getErrorMessageByCode } from "./apiErrors";
export type { ApiError, ValidationError } from "./apiErrors";
