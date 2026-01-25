@echo off
REM Generate complete Python project structure

echo ========================================
echo Generating Python Project Files
echo ========================================
echo.

REM Create directories
mkdir embeddings 2>nul
mkdir analytics_pipeline 2>nul

echo [1/12] Creating embeddings/__init__.py...
echo # Embeddings package > embeddings\__init__.py

echo [2/12] Creating embeddings/model.py...
(
echo import torch
echo from transformers import AutoProcessor, AutoModel
echo from PIL import Image
echo from io import BytesIO
echo import numpy as np
echo.
echo # Load SigLIP model for image embeddings
echo processor = AutoProcessor.from_pretrained^("google/siglip-base-patch16-256"^)
echo model = AutoModel.from_pretrained^("google/siglip-base-patch16-256"^)
echo.
echo def get_embedding^(image_bytes: bytes^) -^> list:
echo     """Generate 512-dimensional embedding from image"""
echo     img = Image.open^(BytesIO^(image_bytes^)^)
echo     inputs = processor^(images=img, return_tensors="pt"^)
echo.
echo     with torch.no_grad^(^):
echo         emb = model^(**inputs^).image_embeds[0]
echo.
echo     return emb.numpy^(^).tolist^(^)
) > embeddings\model.py

echo [3/12] Creating embeddings/embedding_server.py...
(
echo from fastapi import FastAPI, File, UploadFile
echo from typing import List
echo from .model import get_embedding
echo import uvicorn
echo.
echo app = FastAPI^(title="Trendplus Embedding Server"^)
echo.
echo @app.post^("/embed"^)
echo async def embed^(file: bytes = File^(...^)^):
echo     """Generate embedding for single image"""
echo     vector = get_embedding^(file^)
echo     return {"embedding": vector}
echo.
echo @app.post^("/embed-batch"^)
echo async def embed_batch^(files: List[UploadFile]^):
echo     """Generate embeddings for multiple images"""
echo     results = []
echo     for f in files:
echo         content = await f.read^(^)
echo         vector = get_embedding^(content^)
echo         results.append^({"filename": f.filename, "embedding": vector}^)
echo     return {"results": results}
echo.
echo if __name__ == "__main__":
echo     uvicorn.run^(app, host="0.0.0.0", port=8000^)
) > embeddings\embedding_server.py

echo [4/12] Creating analytics_pipeline/__init__.py...
echo # Analytics pipeline package > analytics_pipeline\__init__.py

echo [5/12] Creating analytics_pipeline/db.py...
(
echo import psycopg2
echo import os
echo from dotenv import load_dotenv
echo.
echo load_dotenv^(^)
echo.
echo def get_conn^(^):
echo     """Get PostgreSQL connection"""
echo     return psycopg2.connect^(
echo         host=os.getenv^("DB_HOST"^),
echo         port=os.getenv^("DB_PORT"^),
echo         dbname=os.getenv^("DB_NAME"^),
echo         user=os.getenv^("DB_USER"^),
echo         password=os.getenv^("DB_PASS"^),
echo         sslmode="require"
echo     ^)
) > analytics_pipeline\db.py

echo [6/12] Creating analytics_pipeline/import_to_db.py...
(
echo from scraper.zalando_scraper import scrape_zalando
echo from scraper.deichmann_scraper import scrape_deichmann
echo from scraper.social_trends import get_social_trends_for_category
echo from .db import get_conn
echo import uuid
echo.
echo def import_eu_trends^(^):
echo     """Import EU trends to database"""
echo     print^("Importing EU trends..."^)
echo.
echo     # Scrape data
echo     zalando = scrape_zalando^(5^)
echo     deichmann = scrape_deichmann^(3^)
echo     all_products = zalando + deichmann
echo.
echo     conn = get_conn^(^)
echo     cur = conn.cursor^(^)
echo.
echo     for p in all_products:
echo         cur.execute^("""
echo             INSERT INTO "EuTrends" ^(
echo                 "Id", "ProductName", "Brand", "Category", "Color",
echo                 "Price", "Rank", "ImageUrl", "Season", "UpdatedAt"
echo             ^) VALUES ^(%s,%s,%s,%s,%s,%s,%s,%s,%s,NOW^(^)^)
echo             ON CONFLICT ^("Id"^) DO UPDATE SET
echo                 "Rank" = EXCLUDED."Rank",
echo                 "Price" = EXCLUDED."Price",
echo                 "UpdatedAt" = NOW^(^);
echo         """, ^(
echo             str^(uuid.uuid4^(^)^), p["name"], p["brand"], p["category"],
echo             p["color"], p["price"], p["rank"], p["image_url"], p["season"]
echo         ^)^)
echo.
echo     conn.commit^(^)
echo     conn.close^(^)
echo     print^(f"Imported {len^(all_products^)} products"^)
) > analytics_pipeline\import_to_db.py

echo [7/12] Creating analytics_pipeline/score_calculation.py...
(
echo from .db import get_conn
echo.
echo def calculate_scores^(^):
echo     """Calculate trend scores for products"""
echo     conn = get_conn^(^)
echo     cur = conn.cursor^(^)
echo.
echo     # Simple EU trend score based on rank
echo     cur.execute^("""
echo         UPDATE "GlobalTrendScores" gts
echo         SET "EuTrendScore" = ^(100.0 / et."Rank"^)
echo         FROM "EuTrends" et
echo         WHERE gts."MatchedEuTrendId" = et."Id";
echo     """^)
echo.
echo     conn.commit^(^)
echo     conn.close^(^)
echo     print^("Scores calculated"^)
) > analytics_pipeline\score_calculation.py

echo [8/12] Creating run_all.py...
(
echo from analytics_pipeline.import_to_db import import_eu_trends
echo from analytics_pipeline.score_calculation import calculate_scores
echo import sys
echo.
echo def run^(^):
echo     """Main entry point"""
echo     try:
echo         print^("=== Trendplus Global Trends Scraper ==="^)
echo         print^(^)
echo.
echo         print^("Step 1: Scraping EU trends..."^)
echo         import_eu_trends^(^)
echo.
echo         print^("Step 2: Calculating scores..."^)
echo         calculate_scores^(^)
echo.
echo         print^(^)
echo         print^("=== Complete! ==="^)
echo.
echo     except Exception as e:
echo         print^(f"Error: {e}"^)
echo         sys.exit^(1^)
echo.
echo if __name__ == "__main__":
echo     run^(^)
) > run_all.py

echo [9/12] Creating .env.example...
(
echo # Database Configuration
echo DB_HOST=your-neon-host.neon.tech
echo DB_PORT=5432
echo DB_NAME=analytics
echo DB_USER=neondb_owner
echo DB_PASS=your_password
echo.
echo # RapidAPI
echo RAPIDAPI_KEY=
echo.
echo # .NET API
echo DOTNET_API_URL=http://localhost:8080
) > .env.example

echo [10/12] Creating Dockerfile...
(
echo FROM python:3.10-slim
echo.
echo WORKDIR /app
echo.
echo COPY requirements.txt .
echo RUN pip install --no-cache-dir -r requirements.txt
echo.
echo COPY . .
echo.
echo CMD ["python", "run_all.py"]
) > Dockerfile

echo [11/12] Creating .dockerignore...
(
echo venv/
echo __pycache__/
echo *.pyc
echo .env
echo *.log
echo .pytest_cache/
) > .dockerignore

echo [12/12] Creating run_scraper.bat...
(
echo @echo off
echo call venv\Scripts\activate.bat
echo python run_all.py
echo pause
) > run_scraper.bat

echo.
echo ========================================
echo SUCCESS! Project structure created
echo ========================================
echo.
echo Next steps:
echo   1. Run setup.bat to install dependencies
echo   2. Edit .env with your credentials
echo   3. Run run_scraper.bat to start scraping
echo.

pause
