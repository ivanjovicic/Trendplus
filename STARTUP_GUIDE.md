# 🚀 Trendplus Full Stack Startup Guide

## Quick Start (3 Steps)

### Option 1: Start Everything at Once

```bash
# Double-click this file:
start-app.bat
```

This will start:
1. Python Trends API (port 8000)
2. .NET Backend API (port 8080)
3. React Frontend (port 5173)

### Option 2: Manual Step-by-Step

#### 1. Start Python Trends Service

```bash
cd Python
start_api.bat
```

**Check if running:**
```bash
curl http://localhost:8000/
```

Expected response:
```json
{
  "service": "Trendplus Global Trends API",
  "status": "running",
  "scrapers_available": true
}
```

#### 2. Start .NET Backend API

```bash
cd Api
dotnet run --urls "http://localhost:8080"
```

**Check if running:**
```bash
curl http://localhost:8080/health
```

#### 3. Start React Frontend

```bash
cd Klijent\clientapp
npm run dev
```

**Access:** http://localhost:5173

---

## 🧪 Testing Services

Run the test script:

```bash
test-services.bat
```

Or test manually:

```bash
# Python API
curl "http://localhost:8000/trends/social?category=Patike"

# Backend API
curl http://localhost:8080/health

# Frontend
# Open browser: http://localhost:5173
```

---

## 📊 Global Trends Feature

### How It Works

1. **Frontend** → Requests trends from Backend API
2. **Backend API** → Calls Python service
3. **Python Service** → Returns:
   - **Social media trends** (TikTok/Instagram hashtags)
   - **EU market data** (Zalando, Deichmann scrapers)

### Testing Global Trends

1. Open frontend: http://localhost:5173
2. Navigate to **Global Trends** page
3. Select category (Patike, Cizme, Sandale...)
4. Click **"Fetch Trends"**

You should see:
- Real hashtag data with trend scores
- TikTok/Instagram metrics
- Product recommendations

---

## ⚠️ Troubleshooting

### Python Service Not Starting

```bash
cd Python
setup.bat  # Install dependencies
start_api.bat
```

### Backend API Fails

Check database connection in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "your-neon-postgres-url"
  }
}
```

### Frontend Not Loading

```bash
cd Klijent\clientapp
npm install  # Install dependencies
npm run dev
```

### "Python service unavailable" Error

**Cause:** Backend can't reach Python service

**Fix:**
1. Check Python service is running: `curl http://localhost:8000/`
2. Check firewall isn't blocking port 8000
3. Restart Python service

---

## 🛑 Stopping Services

Press **Ctrl+C** in each terminal window, or:

```bash
# Kill all processes
taskkill /F /IM python.exe
taskkill /F /IM dotnet.exe
taskkill /F /IM node.exe
```

---

## 📁 Service Ports

| Service | Port | URL |
|---------|------|-----|
| Python API | 8000 | http://localhost:8000 |
| Backend API | 8080 | http://localhost:8080 |
| Swagger UI | 8080 | http://localhost:8080/swagger |
| Frontend | 5173 | http://localhost:5173 |

---

## 🔧 Configuration Files

- **Python:** `Python/.env` - API keys for TikTok/Instagram
- **Backend:** `Api/appsettings.json` - Database connection
- **Frontend:** `Klijent/clientapp/.env` - API base URL

---

## 📝 Notes

- **Mock data removed** - System now requires Python service
- **Python service** has its own mock fallback if scrapers fail
- **Scrapers** need API keys in `Python/.env` for real data

---

## 🎯 Next Steps

1. Configure API keys in `Python/.env` for real TikTok/Instagram data
2. Test all features in the UI
3. Check logs for any errors

**Happy coding! 🚀**
