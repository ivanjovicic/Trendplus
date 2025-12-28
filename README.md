# :package: Trendplus - Inventory & Sales Management System

Modern inventory and sales management system built with **.NET 8** and **React + TypeScript**.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18.3-61DAFB?logo=react)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.x-3178C6?logo=typescript)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql)](https://www.postgresql.org/)

## :rocket: Features

### Core Functionality
- :white_check_mark: **Inventory Management** - Create, update, and track products (Artikli)
- :white_check_mark: **Sales Management** - Process sales with multiple items per transaction
- :white_check_mark: **Supplier Management** - Track suppliers (Dobavlja?i)
- :white_check_mark: **Product Categories** - Manage product types (Tipovi Obu?e)

### Advanced Features
- :zap: **Performance Dashboard** - Real-time tracking of slow operations (>1s)
- :clipboard: **Logs Viewer** - Comprehensive log viewing with filtering
- :mag: **Search & Filter** - Quick product and sales lookup
- :bar_chart: **Analytics** - Performance metrics and request tracking

## :building_construction: Architecture

### Backend (.NET 8)
```
??? Api (Trendplus2)          # Web API with minimal endpoints
??? Application               # CQRS + MediatR handlers
??? Domain                    # Domain models
??? Infrastructure            # Database & external services
??? Workers                   # Background jobs
```

**Tech Stack:**
- ASP.NET Core 8.0 (Minimal APIs)
- MediatR 12.4.1 (CQRS pattern)
- Entity Framework Core 8.0
- PostgreSQL (Neon.tech)
- Serilog (Structured logging)

### Frontend (React + TypeScript)
```
Klijent/clientapp/
??? src/
?   ??? components/           # Reusable UI components
?   ??? pages/                # Page components
?   ??? services/             # API clients
?   ??? types/                # TypeScript types
?   ??? context/              # React context providers
??? ...
```

**Tech Stack:**
- React 18.3
- TypeScript 5.x
- Vite 7.x (Build tool)
- Tailwind CSS 4.x
- React Router 7.x

## :card_file_box: Database

### Databases:
1. **trendplus** (Main) - Products, Sales, Suppliers
2. **analytics** (Analytics) - Performance logs, Metrics

### Connection:
- Hosted on [Neon.tech](https://neon.tech) (Serverless PostgreSQL)
- Region: EU Central (Frankfurt)

## :package: Installation

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [Git](https://git-scm.com/)

### Clone Repository
```bash
git clone https://github.com/ivanjovicic/Trendplus.git
cd Trendplus
```

### Backend Setup
```bash
cd Trendplus2
dotnet restore
dotnet run
```

Backend will run on: **http://localhost:8080**

### Frontend Setup
```bash
cd Klijent/clientapp
npm install
npm run dev
```

Frontend will run on: **http://localhost:5173**

## :key: Configuration

### Backend Configuration
Edit `Trendplus2/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=trendplus;...",
    "AnalyticsConnection": "Host=...;Database=analytics;..."
  }
}
```

### Frontend Configuration
Create `Klijent/clientapp/.env.development`:

```env
VITE_API_BASE_URL=http://localhost:8080
```

## :book: API Endpoints

### Products (Artikli)
- `GET /artikli` - List all products
- `GET /artikli/{id}` - Get product by ID
- `POST /artikli` - Create new product
- `PUT /artikli/{id}` - Update product

### Sales (Prodaja)
- `POST /api/prodaja` - Create new sale

### Suppliers & Categories
- `GET /dobavljaci` - List suppliers
- `POST /dobavljaci` - Create supplier
- `GET /tipovi-obuce` - List product types
- `POST /tipovi-obuce` - Create product type

### Monitoring
- `GET /api/logs` - View application logs
- `GET /api/performance` - Performance statistics
- `GET /health` - Health check

## :dart: Usage

### Create Product
1. Navigate to "Kreiraj artikal"
2. Fill in product details
3. Optionally create new supplier/category inline
4. Click "Kreiraj artikal"

### Process Sale
1. Navigate to "Prodaja"
2. Enter receipt number
3. Add products to cart
4. Adjust quantities and prices
5. Click "Sa?uvaj prodaju"

### View Performance
1. Navigate to "Performance Dashboard"
2. See slow requests (>1s)
3. Filter by duration, date range
4. Identify bottlenecks

### View Logs
1. Navigate to "Logovi"
2. Filter by level, date range
3. View exceptions and stack traces

## :test_tube: Testing

### Backend Tests
```bash
cd Trendplus2
dotnet test
```

### Frontend Tests
```bash
cd Klijent/clientapp
npm test
```

## :bar_chart: Performance Features

### Automatic Tracking
- All MediatR requests tracked
- Requests >1000ms logged to database
- Failed requests always logged
- Minimal overhead (<5ms per request)

### Dashboard Metrics
- Total requests count
- Slow requests (>1s)
- Failed requests
- Average duration
- Max duration

## :lock: Security Notes

- :warning: Connection strings contain credentials - **DO NOT COMMIT** to public repos
- Use environment variables in production
- Enable HTTPS in production
- Implement authentication/authorization for production use

## :hammer_and_wrench: Development

### Backend Development
```bash
cd Trendplus2
dotnet watch run
```

### Frontend Development
```bash
cd Klijent/clientapp
npm run dev
```

### Database Migrations
Automatic migrations run on startup. Manual migrations:
```sql
-- See Database/Migrations/ folder
psql -h <host> -U <user> -d <database> -f <migration.sql>
```

## :books: Documentation

- [Logs Feature](LOGS_FEATURE.md) - Log viewing documentation
- [Performance Tracking](PERFORMANCE_TRACKING.md) - Performance monitoring guide

## :handshake: Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open Pull Request

## :memo: License

This project is private and proprietary.

## :bust_in_silhouette: Author

**Ivan Jovicic**
- GitHub: [@ivanjovicic](https://github.com/ivanjovicic)

## :pray: Acknowledgments

- Built with [MediatR](https://github.com/jbogard/MediatR)
- Logging with [Serilog](https://serilog.net/)
- Database hosted on [Neon.tech](https://neon.tech)
- UI styled with [Tailwind CSS](https://tailwindcss.com/)

---

**Made with :heart: for efficient inventory management**
