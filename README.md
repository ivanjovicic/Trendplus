# Trendplus - Inventory & Sales Management System

Modern inventory and sales management system built with **.NET 8** and **React + TypeScript**.

## Features

### Core Functionality
- **Inventory Management** - Create, update, and track products (Artikli)
- **Sales Management** - Process sales with multiple items per transaction
- **Supplier Management** - Track suppliers (Dobavlja?i)
- **Product Categories** - Manage product types (Tipovi obu?e)

### Monitoring
- **Logs Viewer** - `/api/logs` (greške iz baze)
- **Performance Dashboard** - `/api/performance` (spore operacije)
- **Health check** - `/health`

### Unos robe (Goods Receiving) - ENHANCED GRID v2

The goods receiving page features a **full-width responsive grid** with improved dropdown visibility.

**Grid Features:**
- Full-width layout (up to 1400px)
- Increased minimum height (500px)
- Taller rows (70px)
- Responsive font sizing
- Smart dropdown positioning (opens upward near bottom)
- Horizontal scroll on smaller screens

**Search for existing articles:**
- Type article name to search existing inventory
- Autocomplete dropdown (up to 10 results)
- Click to select and auto-fill data
- Green highlight for existing articles

## Architecture

### Backend (.NET 8)
- `Trendplus2` (Api)
- `Application` (CQRS + MediatR)
- `Domain`
- `Infrastructure`
- `Workers`

### Frontend (React + TypeScript)
- `Klijent/clientapp`

## Installation

### Prerequisites
- .NET 8 SDK
- Node.js 18+

### Backend
```bash
cd Trendplus2
dotnet restore
dotnet run
```

### Frontend
```bash
cd Klijent/clientapp
npm install
npm run dev
```

## Configuration

### Frontend
Create `Klijent/clientapp/.env.development`:

```env
VITE_API_BASE_URL=http://localhost:8080
```

### Backend
Edit `Trendplus2/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=trendplus;...",
    "AnalyticsConnection": "Host=...;Database=analytics;..."
  }
}
```

## Database / Migrations

Run migrations from solution root:

```bash
dotnet ef database update --project Infrastructure\Infrastructure.csproj --startup-project Trendplus2\Api.csproj --context TrendplusDbContext
```

```bash
dotnet ef database update --project Infrastructure\Infrastructure.csproj --startup-project Trendplus2\Api.csproj --context AnalyticsDbContext
```

## Error Logging

The app includes middleware (`ExceptionLoggingMiddleware`) that logs unhandled exceptions to Serilog and attempts to persist errors to the `ErrorRecords` table.

If the table doesn't exist (migrations not run), errors will still be logged to Serilog but won't be persisted.

## API Endpoints

### Products (Artikli)
- `GET /artikli`
- `GET /artikli/{id}`
- `POST /artikli`
- `PUT /artikli/{id}`

### Sales
- `POST /api/prodaja`

### Suppliers & Categories
- `GET /dobavljaci`
- `POST /dobavljaci`
- `GET /tipovi-obuce`
- `POST /tipovi-obuce`

### Monitoring
- `GET /api/logs`
- `GET /api/performance`
- `GET /health`
