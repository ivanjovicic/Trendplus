/**
 * Circuit Breaker Implementation for API calls
 * 
 * States:
 * - CLOSED: Normal operation, all requests go through
 * - OPEN: Failure threshold exceeded, reject all requests immediately
 * - HALF_OPEN: After cooldown, allow one test request
 */

export type CircuitState = "CLOSED" | "OPEN" | "HALF_OPEN";

export interface CircuitBreakerConfig {
    failureThreshold: number;
    successThreshold: number;
    cooldownPeriod: number;
    timeout: number;
    name?: string;
}

export interface CircuitBreakerStats {
    state: CircuitState;
    failures: number;
    successes: number;
    lastFailureTime: number | null;
    totalRequests: number;
    rejectedRequests: number;
    name: string;
}

const DEFAULT_CONFIG: CircuitBreakerConfig = {
    failureThreshold: 5,
    successThreshold: 2,
    cooldownPeriod: 30000,
    timeout: 15000,
    name: "default",
};

class CircuitBreaker {
    private state: CircuitState = "CLOSED";
    private failures = 0;
    private successes = 0;
    private lastFailureTime: number | null = null;
    private totalRequests = 0;
    private rejectedRequests = 0;
    private config: CircuitBreakerConfig;
    private listeners: Set<(stats: CircuitBreakerStats) => void> = new Set();

    constructor(config: Partial<CircuitBreakerConfig> = {}) {
        this.config = { ...DEFAULT_CONFIG, ...config };
    }

    subscribe(listener: (stats: CircuitBreakerStats) => void): () => void {
        this.listeners.add(listener);
        listener(this.getStats());
        return () => this.listeners.delete(listener);
    }

    private notifyListeners(): void {
        const stats = this.getStats();
        this.listeners.forEach(listener => listener(stats));
    }

    getStats(): CircuitBreakerStats {
        return {
            state: this.state,
            failures: this.failures,
            successes: this.successes,
            lastFailureTime: this.lastFailureTime,
            totalRequests: this.totalRequests,
            rejectedRequests: this.rejectedRequests,
            name: this.config.name || "default",
        };
    }

    private checkCooldown(): void {
        if (this.state === "OPEN" && this.lastFailureTime) {
            const elapsed = Date.now() - this.lastFailureTime;
            if (elapsed >= this.config.cooldownPeriod) {
                console.log(`?? [${this.config.name}] Circuit: OPEN ? HALF_OPEN`);
                this.state = "HALF_OPEN";
                this.notifyListeners();
            }
        }
    }

    private recordSuccess(): void {
        this.failures = 0;
        if (this.state === "HALF_OPEN") {
            this.successes++;
            if (this.successes >= this.config.successThreshold) {
                console.log(`?? [${this.config.name}] Circuit: HALF_OPEN ? CLOSED`);
                this.state = "CLOSED";
                this.successes = 0;
                this.notifyListeners();
            }
        }
    }

    private recordFailure(): void {
        this.failures++;
        this.successes = 0;
        this.lastFailureTime = Date.now();

        if (this.state === "HALF_OPEN") {
            console.log(`?? [${this.config.name}] Circuit: HALF_OPEN ? OPEN`);
            this.state = "OPEN";
            this.notifyListeners();
        } else if (this.failures >= this.config.failureThreshold) {
            console.log(`?? [${this.config.name}] Circuit: CLOSED ? OPEN (${this.failures} failures)`);
            this.state = "OPEN";
            this.notifyListeners();
        }
    }

    async execute<T>(fn: () => Promise<T>): Promise<T> {
        this.totalRequests++;
        this.checkCooldown();

        if (this.state === "OPEN") {
            this.rejectedRequests++;
            this.notifyListeners();
            const timeRemaining = this.lastFailureTime 
                ? Math.max(0, this.config.cooldownPeriod - (Date.now() - this.lastFailureTime))
                : this.config.cooldownPeriod;
            throw new CircuitBreakerError(
                `Circuit breaker is OPEN. Retry in ${Math.ceil(timeRemaining / 1000)}s`,
                this.getStats()
            );
        }

        try {
            const result = await Promise.race([
                fn(),
                new Promise<never>((_, reject) =>
                    setTimeout(() => reject(new Error("Request timeout")), this.config.timeout)
                ),
            ]);
            this.recordSuccess();
            return result;
        } catch (error) {
            this.recordFailure();
            throw error;
        }
    }

    reset(): void {
        console.log(`?? [${this.config.name}] Circuit: Manual reset ? CLOSED`);
        this.state = "CLOSED";
        this.failures = 0;
        this.successes = 0;
        this.lastFailureTime = null;
        this.notifyListeners();
    }

    isAvailable(): boolean {
        this.checkCooldown();
        return this.state !== "OPEN";
    }

    getState(): CircuitState {
        this.checkCooldown();
        return this.state;
    }

    getRemainingCooldown(): number {
        if (this.state !== "OPEN" || !this.lastFailureTime) return 0;
        return Math.max(0, this.config.cooldownPeriod - (Date.now() - this.lastFailureTime));
    }
}

export class CircuitBreakerError extends Error {
    public readonly stats: CircuitBreakerStats;

    constructor(message: string, stats: CircuitBreakerStats) {
        super(message);
        this.name = "CircuitBreakerError";
        this.stats = stats;
    }
}

// Global circuit breaker for API calls
export const apiCircuitBreaker = new CircuitBreaker({
    name: "API",
    failureThreshold: 5,
    successThreshold: 2,
    cooldownPeriod: 30000,
    timeout: 15000,
});

export default CircuitBreaker;
