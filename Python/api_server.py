"""
FastAPI server for Global Trends
Provides REST API for .NET backend to call Python scrapers
"""

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from typing import Dict, List
import sys
import os

# Add parent directory to path for imports
sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

# Try to import scrapers, but provide mock data if they fail
try:
    from scraper.aggregator import get_category_trends
    from scraper.zalando_scraper import scrape_zalando
    from scraper.deichmann_scraper import scrape_deichmann
    SCRAPERS_AVAILABLE = True
except Exception as e:
    print(f"⚠️ Scrapers not available: {e}")
    print("📊 Using mock data mode")
    SCRAPERS_AVAILABLE = False

app = FastAPI(
    title="Trendplus Global Trends API",
    description="EU Market Scraping & Social Media Trends Analysis",
    version="1.0.0"
)

# CORS - allow .NET API to call this service
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Allow all origins for testing
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/")
def root():
    """Health check endpoint"""
    return {
        "service": "Trendplus Global Trends API",
        "status": "running",
        "version": "1.0.0",
        "scrapers_available": SCRAPERS_AVAILABLE
    }

@app.get("/trends/social")
def get_social_trends(category: str = "Patike"):
    """
    Get social media trends for a product category
    
    Args:
        category: Product category (Patike, Sandale, Cipele, Cizme)
    
    Returns:
        Dict with trends from TikTok & Instagram
    """
    try:
        print(f"📊 Fetching social trends for: {category}")
        
        if SCRAPERS_AVAILABLE:
            try:
                trends = get_category_trends(category)
                
                # Transform to camelCase for frontend
                result_trends = []
                for t in trends:
                    result_trends.append({
                        "hashtag": t["hashtag"],
                        "category": t["category"],
                        "tiktokScore": t["tiktok_score"],
                        "instagramScore": t["instagram_score"],
                        "finalTrendScore": t["final_trend_score"],
                        "trendLevel": t["trend_level"],
                        "tiktokViews": t["tiktok_views"],
                        "tiktokPosts": t["tiktok_posts"],
                        "instagramPosts": t["instagram_posts"],
                        "tiktokEngagement": t["tiktok_engagement"]
                    })
                
                return {
                    "category": category,
                    "trends": result_trends,
                    "count": len(result_trends)
                }
            except Exception as e:
                print(f"⚠️ Scraper error, using mock data: {e}")
        
        # Mock data fallback
        print("📊 Returning mock data")
        return get_mock_trends(category)
        
    except Exception as e:
        print(f"❌ Error fetching trends: {e}")
        raise HTTPException(status_code=500, detail=str(e))


def get_mock_trends(category: str):
    """Return mock trend data for testing"""
    mock_data = {
        "Patike": [
            {
                "hashtag": "#sneakers",
                "category": category,
                "tiktokScore": 85.5,
                "instagramScore": 82.3,
                "finalTrendScore": 84.2,
                "trendLevel": "🔥 Viral",
                "tiktokViews": 1234567890,
                "tiktokPosts": 150000,
                "instagramPosts": 2500000,
                "tiktokEngagement": 8.5
            },
            {
                "hashtag": "#airmax",
                "category": category,
                "tiktokScore": 72.2,
                "instagramScore": 75.5,
                "finalTrendScore": 73.5,
                "trendLevel": "📈 Trending",
                "tiktokViews": 890000000,
                "tiktokPosts": 95000,
                "instagramPosts": 1800000,
                "tiktokEngagement": 7.2
            },
            {
                "hashtag": "#nike",
                "category": category,
                "tiktokScore": 88.0,
                "instagramScore": 90.0,
                "finalTrendScore": 88.8,
                "trendLevel": "🔥 Viral",
                "tiktokViews": 2000000000,
                "tiktokPosts": 250000,
                "instagramPosts": 5000000,
                "tiktokEngagement": 9.1
            },
            {
                "hashtag": "#yeezy",
                "category": category,
                "tiktokScore": 65.0,
                "instagramScore": 70.0,
                "finalTrendScore": 67.0,
                "trendLevel": "📈 Trending",
                "tiktokViews": 500000000,
                "tiktokPosts": 80000,
                "instagramPosts": 1200000,
                "tiktokEngagement": 6.8
            },
            {
                "hashtag": "#jordans",
                "category": category,
                "tiktokScore": 78.5,
                "instagramScore": 80.2,
                "finalTrendScore": 79.2,
                "trendLevel": "🔥 Viral",
                "tiktokViews": 1100000000,
                "tiktokPosts": 120000,
                "instagramPosts": 2200000,
                "tiktokEngagement": 8.0
            }
        ],
        "Sandale": [
            {
                "hashtag": "#summershoes",
                "category": category,
                "tiktokScore": 55.0,
                "instagramScore": 60.0,
                "finalTrendScore": 57.0,
                "trendLevel": "👀 Growing",
                "tiktokViews": 200000000,
                "tiktokPosts": 40000,
                "instagramPosts": 800000,
                "tiktokEngagement": 5.5
            }
        ]
    }
    
    trends = mock_data.get(category, mock_data["Patike"])
    
    return {
        "category": category,
        "trends": trends,
        "count": len(trends),
        "note": "Mock data - Python scrapers not initialized"
    }


@app.post("/scrapers/run")
def run_scrapers(
    zalando_pages: int = 3,
    deichmann_pages: int = 2
):
    """
    Run EU market scrapers (Zalando, Deichmann)
    
    Args:
        zalando_pages: Number of pages to scrape from Zalando
        deichmann_pages: Number of pages to scrape from Deichmann
    
    Returns:
        Dict with scraper results
    """
    try:
        print(f"🔍 Running scrapers...")
        
        if not SCRAPERS_AVAILABLE:
            print("📊 Scrapers not available, returning mock data")
            return {
                "status": "completed",
                "results": [
                    {"source": "Zalando", "productsCount": 45, "status": "mock_data"},
                    {"source": "Deichmann", "productsCount": 38, "status": "mock_data"}
                ],
                "totalProducts": 83,
                "note": "Mock data - Python scrapers not initialized"
            }
        
        results = []
        
        # Scrape Zalando
        try:
            print(f"  Zalando ({zalando_pages} pages)...")
            zalando_products = scrape_zalando(max_pages=zalando_pages, category="sneaker")
            results.append({
                "source": "Zalando",
                "productsCount": len(zalando_products),
                "status": "success"
            })
            print(f"  ✅ Zalando: {len(zalando_products)} products")
        except Exception as e:
            print(f"  ❌ Zalando failed: {e}")
            results.append({
                "source": "Zalando",
                "productsCount": 0,
                "status": "failed"
            })
        
        # Scrape Deichmann
        try:
            print(f"  Deichmann ({deichmann_pages} pages)...")
            deichmann_products = scrape_deichmann(max_pages=deichmann_pages, headless=True)
            results.append({
                "source": "Deichmann",
                "productsCount": len(deichmann_products),
                "status": "success"
            })
            print(f"  ✅ Deichmann: {len(deichmann_products)} products")
        except Exception as e:
            print(f"  ❌ Deichmann failed: {e}")
            results.append({
                "source": "Deichmann",
                "productsCount": 0,
                "status": "failed"
            })
        
        return {
            "status": "completed",
            "results": results,
            "totalProducts": sum(r["productsCount"] for r in results)
        }
        
    except Exception as e:
        print(f"❌ Scraper error: {e}")
        raise HTTPException(status_code=500, detail=str(e))


@app.get("/cache/stats")
def get_cache_stats():
    """Get cache statistics"""
    try:
        from scraper.cache import get_cache_stats
        stats = get_cache_stats()
        return stats
    except Exception as e:
        return {
            "error": "Cache not available",
            "message": str(e)
        }


if __name__ == "__main__":
    import uvicorn
    
    print("=" * 60)
    print("🚀 Starting Trendplus Global Trends API")
    print("=" * 60)
    print()
    print("  📊 Social Trends: GET  /trends/social?category=Patike")
    print("  🔍 Run Scrapers:  POST /scrapers/run")
    print("  💾 Cache Stats:   GET  /cache/stats")
    print()
    print(f"  Scrapers available: {SCRAPERS_AVAILABLE}")
    if not SCRAPERS_AVAILABLE:
        print("  ⚠️ Using mock data mode")
    print()
    print("=" * 60)
    print()
    
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=8000,
        log_level="info"
    )
