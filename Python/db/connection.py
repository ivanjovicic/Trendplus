"""
Async PostgreSQL connection pool using asyncpg.

Usage:
    from db.connection import get_pool, fetch, execute, executemany

    pool = await get_pool()
    rows = await fetch("SELECT * FROM trend_product_snapshots WHERE snapshot_date = $1", today)
"""

from __future__ import annotations

import logging
import os
from contextlib import asynccontextmanager
from typing import Any, List, Optional

import asyncpg

logger = logging.getLogger("db")

_pool: Optional[asyncpg.Pool] = None

# ── Config ────────────────────────────────────────────────────────────────────

def _dsn() -> str:
    """Read DATABASE_URL from env (supports Neon, Render, local Postgres)."""
    dsn = os.getenv("DATABASE_URL") or os.getenv("POSTGRES_URL")
    if not dsn:
        host     = os.getenv("PGHOST",     "localhost")
        port     = os.getenv("PGPORT",     "5432")
        user     = os.getenv("PGUSER",     "postgres")
        password = os.getenv("PGPASSWORD", "")
        dbname   = os.getenv("PGDATABASE", "trendplus")
        dsn = f"postgresql://{user}:{password}@{host}:{port}/{dbname}"
    # asyncpg needs postgresql:// not postgres://
    return dsn.replace("postgres://", "postgresql://", 1)


# ── Pool lifecycle ────────────────────────────────────────────────────────────

async def get_pool(
    min_size: int = 2,
    max_size: int = 10,
    command_timeout: float = 60.0,
) -> asyncpg.Pool:
    """Return (or create) the global connection pool."""
    global _pool
    if _pool is None:
        logger.info("Creating asyncpg pool ...")
        _pool = await asyncpg.create_pool(
            dsn=_dsn(),
            min_size=min_size,
            max_size=max_size,
            command_timeout=command_timeout,
        )
        logger.info("Pool ready.")
    return _pool


async def close_pool() -> None:
    """Gracefully close the pool. Call on shutdown."""
    global _pool
    if _pool:
        await _pool.close()
        _pool = None


@asynccontextmanager
async def acquire():
    """Async context manager that yields a connection from the pool."""
    pool = await get_pool()
    async with pool.acquire() as conn:
        yield conn


# ── Helpers ───────────────────────────────────────────────────────────────────

async def fetch(query: str, *args: Any) -> List[asyncpg.Record]:
    pool = await get_pool()
    return await pool.fetch(query, *args)


async def fetchrow(query: str, *args: Any) -> Optional[asyncpg.Record]:
    pool = await get_pool()
    return await pool.fetchrow(query, *args)


async def fetchval(query: str, *args: Any) -> Any:
    pool = await get_pool()
    return await pool.fetchval(query, *args)


async def execute(query: str, *args: Any) -> str:
    pool = await get_pool()
    return await pool.execute(query, *args)


async def executemany(query: str, args: List[tuple]) -> None:
    pool = await get_pool()
    async with pool.acquire() as conn:
        async with conn.transaction():
            await conn.executemany(query, args)


async def bulk_insert(table: str, rows: List[dict]) -> int:
    """
    Generic bulk insert via COPY (fastest path).
    All dicts must have the same keys.
    Returns the number of rows inserted.
    """
    if not rows:
        return 0

    columns = list(rows[0].keys())
    records = [tuple(r[c] for c in columns) for r in rows]

    pool = await get_pool()
    async with pool.acquire() as conn:
        result = await conn.copy_records_to_table(
            table,
            records=records,
            columns=columns,
        )
    # result is like "COPY N"
    try:
        return int(str(result).split()[-1])
    except Exception:
        return len(records)
