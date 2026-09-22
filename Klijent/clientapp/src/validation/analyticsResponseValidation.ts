import type { ZodType } from "zod";
import type { AnalyticsResponseMeta } from "../types/analytics";

export class AnalyticsResponseValidationError extends Error {
  readonly context: string;
  readonly correlationId: string | null;
  readonly issuePaths: string[];

  constructor(context: string, issuePaths: string[], correlationId?: string | null) {
    super(`${context} response nije u očekivanom formatu.`);
    this.name = "AnalyticsResponseValidationError";
    this.context = context;
    this.correlationId = correlationId ?? null;
    this.issuePaths = issuePaths;
  }
}

function getMeta(payload: unknown): AnalyticsResponseMeta | null {
  if (!payload || typeof payload !== "object") {
    return null;
  }

  const meta = (payload as { meta?: unknown }).meta;
  return meta && typeof meta === "object" ? meta as AnalyticsResponseMeta : null;
}

export function validateAnalyticsResponse<T>(
  payload: unknown,
  schema: ZodType<unknown>,
  context: string,
): T {
  const result = schema.safeParse(payload);
  if (result.success) {
    return result.data as T;
  }

  throw new AnalyticsResponseValidationError(
    context,
    result.error.issues.map((issue) => issue.path.join(".") || "<root>"),
    getMeta(payload)?.correlationId,
  );
}
