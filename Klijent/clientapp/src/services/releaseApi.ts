export async function fetchReleaseCalendar(gender = "mens") {
    // Simple fetch wrapper with retry
    const maxAttempts = 3;
    let attempt = 0;
    let lastErr = null;

    while (attempt < maxAttempts) {
        attempt++;
        try {
            const res = await fetch(`/api/release/${encodeURIComponent(gender)}`);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            // Backend returns object; try to return .items if present
            return data?.items ?? data;
        } catch (err) {
            lastErr = err;
            await new Promise(r => setTimeout(r, 200 * attempt));
        }
    }

    throw lastErr;
}
