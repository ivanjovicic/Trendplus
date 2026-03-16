"""
trend_engine/api.py
===================
FastAPI servis koji izlaže trend scoring kao HTTP endpoint.

C# worker konzumira: GET /generate-trends?pages=5&markets=DE&markets=AT

Pokretanje:
    cd Python/
    uvicorn trend_engine.api:app --host 0.0.0.0 --port 8001 --reload

Ili putem start_api.bat:
    uvicorn trend_engine.api:app --host 0.0.0.0 --port 8001
"""

from __future__ import annotations

import logging
from typing import List, Optional

from fastapi import FastAPI, Query, HTTPException
from fastapi.responses import JSONResponse

from trend_engine.run_batch import generate_trend_results

logger = logging.getLogger("trend_engine.api")

app = FastAPI(
    title="Trendplus – Trend Engine",
    description=(
        "Čisti scoring engine (bez DB/auth). "
        "C# worker poziva /generate-trends da dobije JSON listu grupisanih trend rezultata."
    ),
    version="1.0.0",
)


# ─── Endpoints ────────────────────────────────────────────────────────────────

@app.get(
    "/generate-trends",
    summary="Pokreni kompletan batch i vrati trend rezultate",
    response_class=JSONResponse,
)
async def generate_trends(
    top: Optional[int] = Query(default=None, ge=1, le=1000, description="Maksimalan broj rezultata u odgovoru."),
    pages: int = Query(default=5, ge=1, le=20, description="Broj stranica po izvoru/tržištu"),
    markets: Optional[List[str]] = Query(
        default=None,
        description="Tržišta za scraping, npr. ?markets=DE&markets=AT. Default: sva.",
    ),
):
    if pages < 1 or pages > 20:
        raise HTTPException(status_code=400, detail="Invalid value for pages. Must be between 1 and 20.")

    if markets and not all(isinstance(market, str) for market in markets):
        raise HTTPException(status_code=400, detail="All markets must be strings.")

    try:
        results = await generate_trend_results(pages=pages, markets=markets, top_n=top)
        return JSONResponse(content={"count": len(results), "items": results})
    except Exception as e:
        logger.error(f"Error generating trends: {e}")
        raise HTTPException(status_code=500, detail="Internal server error while generating trends.")


@app.get("/health", summary="Health check")
async def health():
    return {"status": "ok", "service": "trend-engine"}
