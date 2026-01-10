/**
 * API Error types and handling utilities
 */

export interface ApiError {
    status: number;
    title: string;
    detail: string;
    errorCode: string;
    correlationId: string;
    timestamp: string;
    errors?: Record<string, string[]>;
    metadata?: Record<string, unknown>;
}

export interface ValidationError {
    field: string;
    messages: string[];
}

/**
 * Custom error class for API errors
 */
export class ApiException extends Error {
    public readonly status: number;
    public readonly errorCode: string;
    public readonly correlationId: string;
    public readonly validationErrors?: ValidationError[];
    public readonly metadata?: Record<string, unknown>;

    constructor(error: ApiError) {
        super(error.detail || error.title);
        this.name = "ApiException";
        this.status = error.status;
        this.errorCode = error.errorCode;
        this.correlationId = error.correlationId;
        this.metadata = error.metadata;

        if (error.errors) {
            this.validationErrors = Object.entries(error.errors).map(([field, messages]) => ({
                field,
                messages,
            }));
        }
    }

    get isValidationError(): boolean {
        return this.errorCode === "VALIDATION_ERROR";
    }

    get isNotFound(): boolean {
        return this.status === 404;
    }

    get isUnauthorized(): boolean {
        return this.status === 401;
    }

    get isServerError(): boolean {
        return this.status >= 500;
    }

    get isCircuitBreakerOpen(): boolean {
        return this.errorCode === "CIRCUIT_BREAKER_OPEN";
    }

    getValidationErrorsForField(field: string): string[] {
        return this.validationErrors?.find(e => e.field === field)?.messages ?? [];
    }

    getAllValidationMessages(): string[] {
        return this.validationErrors?.flatMap(e => e.messages) ?? [];
    }
}

/**
 * Parse API error response
 */
export async function parseApiError(response: Response): Promise<ApiException> {
    try {
        const error: ApiError = await response.json();
        return new ApiException(error);
    } catch {
        return new ApiException({
            status: response.status,
            title: response.statusText || "Error",
            detail: `Request failed with status ${response.status}`,
            errorCode: "UNKNOWN_ERROR",
            correlationId: response.headers.get("X-Correlation-ID") || "",
            timestamp: new Date().toISOString(),
        });
    }
}

/**
 * Get user-friendly error message
 */
export function getErrorMessage(error: unknown): string {
    if (error instanceof ApiException) {
        if (error.isValidationError && error.validationErrors) {
            return error.getAllValidationMessages().join(". ");
        }
        return error.message;
    }

    if (error instanceof Error) {
        if (error.message.includes("Failed to fetch")) {
            return "Ne mogu da se povežem sa serverom. Proverite internet konekciju.";
        }
        if (error.message.includes("timeout")) {
            return "Zahtev je istekao. Pokušajte ponovo.";
        }
        return error.message;
    }

    return "Došlo je do neočekivane greške.";
}

/**
 * Error code to user message mapping
 */
const errorMessages: Record<string, string> = {
    VALIDATION_ERROR: "Molimo ispravite greške u formi.",
    ENTITY_NOT_FOUND: "Traženi resurs nije pronađen.",
    BUSINESS_RULE_VIOLATION: "Operacija nije dozvoljena.",
    INSUFFICIENT_STOCK: "Nedovoljna količina na stanju.",
    DUPLICATE_ENTITY: "Zapis sa ovim podacima već postoji.",
    EXTERNAL_SERVICE_ERROR: "Eksterni servis je trenutno nedostupan.",
    CIRCUIT_BREAKER_OPEN: "Servis je privremeno nedostupan. Pokušajte za nekoliko sekundi.",
    UNAUTHORIZED: "Niste autorizovani za ovu akciju.",
    REQUEST_TIMEOUT: "Zahtev je istekao. Pokušajte ponovo.",
    INTERNAL_ERROR: "Došlo je do greške na serveru. Kontaktirajte podršku.",
};

export function getErrorMessageByCode(code: string): string {
    return errorMessages[code] || errorMessages.INTERNAL_ERROR;
}
