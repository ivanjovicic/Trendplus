# 🌍 Trendplus Global Trends Scraper

Python service for scraping EU fashion trends and social media data.

## 📁 Project Structure

```
Python/
├── scraper/
│   ├── __init__.py
│   ├── zalando_scraper.py      ✅ Scrapes Zalando.de
│   ├── deichmann_scraper.py    ✅ Scrapes Deichmann.com
│   ├── social_trends.py        ✅ TikTok/Instagram trends
│   └── utils.py                ✅ Helper functions
│
├── embeddings/
│   ├── __init__.py
│   ├── model.py                📸 CLIP/SigLIP embeddings
│   └── embedding_server.py     🚀 FastAPI server
│
├── analytics_pipeline/
│   ├── __init__.py
│   ├── import_to_db.py         💾 Import to PostgreSQL
│   ├── score_calculation.py   📊 Calculate trend scores
│   └── db.py                   🔌 Database connection
│
├── run_all.py                  🎯 Main entry point
├── requirements.txt            📦 Dependencies
├── setup.bat                   ⚙️ Windows setup
├── .env.example                🔐 Environment template
├── Dockerfile                  🐳 Docker image
└── README.md                   📖 This file
```

## 🚀 Quick Start

### Windows

```batch
# 1. Setup environment
setup.bat

# 2. Activate virtual environment
venv\Scripts\activate.bat

# 3. Run scraper
python run_all.py
```

### Linux/Mac

```bash
# 1. Create virtual environment
python3 -m venv venv
source venv/bin/activate

# 2. Install dependencies
pip install -r requirements.txt

# 3. Configure environment
cp .env.example .env
# Edit .env with your credentials

# 4. Run scraper
python run_all.py
```

## 📋 Configuration

Create `.env` file:

```env
# Database (Analytics)
DB_HOST=your-neon-host.neon.tech
DB_PORT=5432
DB_NAME=analytics
DB_USER=neondb_owner
DB_PASS=your_password

# RapidAPI (for social media data)
RAPIDAPI_KEY=your_rapidapi_key

# .NET API
DOTNET_API_URL=http://localhost:8080
```

## 🔄 How It Works

### 1. **Scrape EU Trends**
```python
from scraper.zalando_scraper import scrape_zalando

products = scrape_zalando(max_pages=5)
# Returns: [{rank, name, brand, price, image_url, ...}, ...]
```

### 2. **Get Social Trends**
```python
from scraper.social_trends import get_social_trends_for_category

trends = get_social_trends_for_category("Patike")
# Returns: [{hashtag, posts_count, growth, ...}, ...]
```

### 3. **Generate Embeddings**
```python
from embeddings.model import get_embedding

embedding = get_embedding(image_bytes)
# Returns: [512-dimensional vector]
```

### 4. **Import to Database**
```python
from analytics_pipeline.import_to_db import import_eu_trends

import_eu_trends()  # Inserts into EuTrends table
```

### 5. **Calculate Scores**
```python
from analytics_pipeline.score_calculation import calculate_scores

calculate_scores()  # Updates GlobalTrendScores table
```

## 📊 Database Tables

### EuTrends
Stores trending products from EU markets.

```sql
SELECT * FROM "EuTrends" ORDER BY "Rank" LIMIT 10;
```

### SocialTrends
Tracks hashtag popularity and growth.

```sql
SELECT * FROM "SocialTrends" 
WHERE "TiktokGrowth" > 20 
ORDER BY "TiktokGrowth" DESC;
```

### GlobalTrendScores
Maps your products to trends with AI scoring.

```sql
SELECT * FROM "GlobalTrendScores" 
ORDER BY "FinalGlobalScore" DESC 
LIMIT 10;
```

## 🤖 Integration with .NET API

### Send scraped data to .NET API:

```python
import requests

response = requests.post(
    "http://localhost:8080/api/trends/import",
    json={"products": scraped_products}
)
```

### Get embeddings from .NET:

```python
# .NET API calls Python embedding service
POST http://localhost:8000/embed
Content-Type: multipart/form-data

file: <image_bytes>
```

## ⏱️ Scheduling

### Run every 6 hours:

```python
import schedule
import time

def job():
    from run_all import run
    run()

schedule.every(6).hours.do(job)

while True:
    schedule.run_pending()
    time.sleep(60)
```

### Windows Task Scheduler:

```batch
# Create task.bat
@echo off
cd C:\Path\To\Trendplus2\Python
call venv\Scripts\activate.bat
python run_all.py
```

Schedule in Task Scheduler to run every 6 hours.

## 🐳 Docker Deployment

```bash
# Build image
docker build -t trendplus-scraper .

# Run container
docker run -d \
  --name trendplus-scraper \
  --env-file .env \
  trendplus-scraper
```

## 📈 API Endpoints (Embedding Server)

Start embedding server:

```bash
cd embeddings
uvicorn embedding_server:app --host 0.0.0.0 --port 8000
```

### Endpoints:

**POST /embed** - Generate embedding
```bash
curl -X POST "http://localhost:8000/embed" \
  -F "file=@image.jpg"
```

**POST /embed-batch** - Batch embeddings
```bash
curl -X POST "http://localhost:8000/embed-batch" \
  -F "files=@image1.jpg" \
  -F "files=@image2.jpg"
```

## 🧪 Testing

```bash
# Run tests
pytest

# Test zalando scraper
python -m scraper.zalando_scraper

# Test embedding generation
python -m embeddings.model
```

## 🔧 Troubleshooting

### Issue: Rate limited by Zalando
**Solution:** Increase delays in `utils.safe_request()`

### Issue: No products found
**Solution:** Check if Zalando changed HTML structure

### Issue: Database connection failed
**Solution:** Verify `.env` credentials and VPN/firewall

### Issue: CUDA not available for embeddings
**Solution:** Install PyTorch with CUDA or use CPU-only version

## 📚 References

- [Zalando](https://www.zalando.de)
- [Deichmann](https://www.deichmann.com)
- [RapidAPI TikTok](https://rapidapi.com/yi-wei-lim/api/tiktok-scraper7)
- [SigLIP Model](https://huggingface.co/google/siglip-base-patch16-256)
- [pgvector](https://github.com/pgvector/pgvector)

## 📝 License

MIT License - Use freely in your Trendplus project.

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open Pull Request

## 🆘 Support

For issues, contact: support@trendplus.rs
