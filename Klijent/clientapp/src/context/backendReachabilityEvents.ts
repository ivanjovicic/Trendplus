export const BACKEND_REACHABLE_EVENT = "trendplus:backend-reachable";
export const BACKEND_UNREACHABLE_EVENT = "trendplus:backend-unreachable";
const BACKEND_AVAILABILITY_CHANNEL = "trendplus:backend-availability";

export type BackendReachabilitySource = "health" | "request";
export type BackendAvailabilityStatus = "unknown" | "down" | "recovering" | "up";
export type BackendUnreachableReason = "network" | "timeout" | "server-error";

export type BackendReachabilityDetail = {
  checkedAt: number;
  source: BackendReachabilitySource;
  url?: string;
  status?: number;
};

export type BackendUnreachableDetail = BackendReachabilityDetail & {
  reason: BackendUnreachableReason;
  message?: string;
};

type BroadcastPayload =
  | { type: "reachable"; detail: BackendReachabilityDetail }
  | { type: "unreachable"; detail: BackendUnreachableDetail };

let latestAvailabilityPayload: BroadcastPayload | null = null;

function getAvailabilityChannel(): BroadcastChannel | null {
  if (typeof window === "undefined" || typeof BroadcastChannel === "undefined") {
    return null;
  }

  return new BroadcastChannel(BACKEND_AVAILABILITY_CHANNEL);
}

function broadcastAvailability(payload: BroadcastPayload): void {
  const channel = getAvailabilityChannel();
  if (!channel) {
    return;
  }

  try {
    channel.postMessage(payload);
  } finally {
    channel.close();
  }
}

export function getLatestBackendAvailabilityPayload(): BroadcastPayload | null {
  return latestAvailabilityPayload;
}

export function createBackendAvailabilityChannel(): BroadcastChannel | null {
  return getAvailabilityChannel();
}

export function notifyBackendReachable(detail: Omit<BackendReachabilityDetail, "checkedAt"> & { checkedAt?: number }): void {
  if (typeof window === "undefined") {
    return;
  }

  if (typeof detail.status === "number" && detail.status >= 500) {
    return;
  }

  const eventDetail: BackendReachabilityDetail = {
    checkedAt: detail.checkedAt ?? Date.now(),
    source: detail.source,
    status: detail.status,
    url: detail.url,
  };

  latestAvailabilityPayload = { type: "reachable", detail: eventDetail };
  window.dispatchEvent(
    new CustomEvent<BackendReachabilityDetail>(BACKEND_REACHABLE_EVENT, {
      detail: eventDetail,
    })
  );
  broadcastAvailability(latestAvailabilityPayload);
}

export function notifyBackendUnreachable(
  detail: Omit<BackendUnreachableDetail, "checkedAt"> & { checkedAt?: number }
): void {
  if (typeof window === "undefined") {
    return;
  }

  const eventDetail: BackendUnreachableDetail = {
    checkedAt: detail.checkedAt ?? Date.now(),
    source: detail.source,
    reason: detail.reason,
    message: detail.message,
    status: detail.status,
    url: detail.url,
  };

  latestAvailabilityPayload = { type: "unreachable", detail: eventDetail };
  window.dispatchEvent(
    new CustomEvent<BackendUnreachableDetail>(BACKEND_UNREACHABLE_EVENT, {
      detail: eventDetail,
    })
  );
  broadcastAvailability(latestAvailabilityPayload);
}
