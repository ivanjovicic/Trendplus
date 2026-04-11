export const BACKEND_REACHABLE_EVENT = "trendplus:backend-reachable";

export type BackendReachabilitySource = "health" | "request";

export type BackendReachabilityDetail = {
  checkedAt: number;
  source: BackendReachabilitySource;
  url?: string;
  status?: number;
};

export function notifyBackendReachable(detail: Omit<BackendReachabilityDetail, "checkedAt"> & { checkedAt?: number }): void {
  if (typeof window === "undefined") {
    return;
  }

  window.dispatchEvent(
    new CustomEvent<BackendReachabilityDetail>(BACKEND_REACHABLE_EVENT, {
      detail: {
        checkedAt: detail.checkedAt ?? Date.now(),
        source: detail.source,
        status: detail.status,
        url: detail.url,
      },
    })
  );
}
