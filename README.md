<<<<<<< HEAD
# Trendplus2

## Features

### Unos robe (Goods Receiving) - ENHANCED GRID v2

The goods receiving page now features a **full-width responsive grid** that adapts to screen size with **improved dropdown visibility**:

**Grid Features:**
- **Full-width layout** - Utilizes maximum available screen space (up to 1400px)
- **Increased minimum height** (500px) - Ensures sufficient space for dropdown menus
- **Taller rows** (70px) - Better spacing for readability and dropdown interaction
- **Responsive font sizing:**
  - < 1024px width: 0.75rem font
  - 1024-1366px: 0.8125rem font  
  - > 1366px: 0.875rem font
- **Smart dropdown positioning:**
  - Opens **downward** for top rows
  - Opens **upward** for bottom rows (last 2) to stay visible
  - Higher z-index (1500) for better stacking
  - Blue border and strong shadow for prominence
- **Horizontal scroll** for smaller screens while maintaining data visibility
- **All inputs set to width: 100%** within their columns

**Search for existing articles - IMPROVED:**
- Type article name to search existing inventory
- **Always visible dropdown** - No need to scroll to see results
- Autocomplete dropdown with up to 10 results (max 400px height)
- Click to select and auto-fill data
- Green highlight for existing articles
- Supports both creating new and updating existing items
- **Intelligent positioning** - dropdown opens above input when near bottom of list

**Statistics:**
- Shows total articles (new vs existing count)
- Displays total purchase value
- Real-time updates as you add/remove items

### Unos robe (Goods Receiving)

The goods receiving page (formerly "Create Article") allows users to input new inventory items into the system.

**Features:**
- Quick input form for new items
- Support for item types (Tip obu?e)
- Supplier management (Dobavlja?i)
- Price tracking (purchase price, selling price, first selling price)
- Quantity management
- Comments/notes field
- **Quick add** for new item types and suppliers directly from the form

### Sales Page - Optimized Article Search

The sales page now includes an optimized search feature for quickly finding and adding articles to sales:

**Features:**
- **Debounced Search** - Search input with 300ms debounce to avoid excessive filtering
- **Real-time Filtering** - Articles are filtered as you type based on name
- **Keyboard Navigation:**
  - `?` / `?` - Navigate through search results
  - `Enter` - Add selected article to sale items
  - `Escape` - Close search results
- **Visual Feedback** - Selected item is highlighted with blue background
- **Quick Add Button** - Each search result has a "+ Dodaj" button for instant adding
- **Result Limit** - Shows top 10 matching articles to keep UI clean

**Usage:**
1. Type article name in the search field
2. Use keyboard arrows or mouse to select desired article
3. Press Enter or click "+ Dodaj" to add to sale items
4. Article is automatically added with quantity 1 and default price

## Responsive Design

The application adapts to different screen sizes:
- **Desktop (>1366px):** Full-size grid with comfortable spacing
- **Laptop (1024-1366px):** Slightly condensed but still readable
- **Tablet (<1024px):** Compact view with smaller fonts
- **All sizes:** Horizontal scroll available to preserve all columns

## Database Setup

This application uses PostgreSQL with Entity Framework Core migrations.

### Running Migrations

?? **Important:** Before running migrations, make sure to **stop the application** in Visual Studio (stop debugging) to avoid file locking issues.

To create or update the database schema, run the following command **from the solution root directory** (`C:\Users\Ivan\source\repos\Trendplus2\`):

**For TrendplusDbContext (main application database with ErrorRecords):**
```bash
dotnet ef database update --project Infrastructure\Infrastructure.csproj --startup-project Trendplus2\Api.csproj --context TrendplusDbContext
```

**For AnalyticsDbContext (analytics database):**
```bash
dotnet ef database update --project Infrastructure\Infrastructure.csproj --startup-project Trendplus2\Api.csproj --context AnalyticsDbContext
```

Or if you prefer to use relative paths from the solution root:

```bash
dotnet ef database update --project .\Infrastructure\Infrastructure.csproj --startup-project .\Trendplus2\Api.csproj --context TrendplusDbContext
```

**Important:** Make sure you run this command from the solution root directory, not from within the `Infrastructure` or `Trendplus2` folders.

### Creating New Migrations

If you need to create a new migration after changing the domain models:

**For TrendplusDbContext:**
```bash
dotnet ef migrations add YourMigrationName --project Infrastructure\Infrastructure.csproj --startup-project Trendplus2\Api.csproj --context TrendplusDbContext
```

**For AnalyticsDbContext:**
```bash
dotnet ef migrations add YourMigrationName --project Infrastructure\Infrastructure.csproj --startup-project Trendplus2\Api.csproj --context AnalyticsDbContext
```

### Common Issues

#### "More than one DbContext was found"

If you see this error, you need to specify which DbContext to use with the `--context` parameter:
- Use `--context TrendplusDbContext` for the main application database (includes ErrorRecords, Artikli, etc.)
- Use `--context AnalyticsDbContext` for the analytics database (ProductsDim, StoresDim)

#### "The file is locked by: IIS Express Worker Process"

If you see an error like:
```
The file is locked by: "IIS Express Worker Process"
```

**Solution:** Stop the application in Visual Studio before running the migration command:
1. Click the **Stop Debugging** button (red square) in Visual Studio, or press **Shift+F5**
2. Wait a few seconds for IIS Express to fully shut down
3. Run the migration command again

#### Error when accessing `/api/logs`

If you see a 500 Internal Server Error when accessing the logs endpoint, it typically means the database migrations haven't been applied yet. The error will include a helpful message:

```
Unable to fetch logs. The database table may not exist. Please run migrations: dotnet ef database update
```

**Solution:** Run the migrations command above with `--context TrendplusDbContext` to create the required database tables.

#### "Project file does not exist" Error

If you get an error like `MSBUILD : error MSB1009: Project file does not exist`, make sure:
1. You're running the command from the **solution root directory** (`C:\Users\Ivan\source\repos\Trendplus2\`)
2. You're using the correct project file paths with `.csproj` extension
3. The correct command is: `dotnet ef database update --project Infrastructure\Infrastructure.csproj --startup-project Trendplus2\Api.csproj --context TrendplusDbContext`

#### "Contains more than one project file" Error

This happens when you try to run the command from inside a project folder that contains multiple `.csproj` files. Always run from the solution root and specify the full project paths.

#### Database Connection Issues

Make sure your PostgreSQL connection string is correctly configured in:
- `Trendplus2/appsettings.json` (for general configuration)
- `Trendplus2/appsettings.Development.json` (for local development)
- `Trendplus2/appsettings.Production.json` (for production)

The connection strings are stored under the `ConnectionStrings` section:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=trendplus;Username=your_user;Password=your_password",
    "AnalyticsConnection": "Host=localhost;Database=analytics;Username=your_user;Password=your_password"
=======
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
>>>>>>> d02157d9ed8b2a830e03a0a23003aa43726588f7
  }
}
```

<<<<<<< HEAD
## Error Logging

The application includes middleware (`ExceptionLoggingMiddleware`) that:
1. Logs all unhandled exceptions to Serilog
2. Attempts to persist errors to the `ErrorRecords` database table
3. Returns a friendly JSON response to the client with a correlation ID

If the `ErrorRecords` table doesn't exist (migrations not run), errors will still be logged to Serilog but won't be persisted to the database.

## Development

### Backend (API)
- Navigate to `Trendplus2` folder
- Run: `dotnet run` or press F5 in Visual Studio

### Frontend (React + Vite)
- Navigate to `Klijent/clientapp` folder
- Run: `npm install` (first time only)
- Run: `npm run dev`

## Project Structure

- **Api** (`Trendplus2/`) - ASP.NET Core Web API
- **Application** - Application layer with MediatR commands/queries
- **Domain** - Domain models
- **Infrastructure** - Data access, DbContext, repositories, migrations
- **Klijent/clientapp** - React + TypeScript frontend with Vite

## Database Contexts

The application uses two separate database contexts:
- **TrendplusDbContext** - Main application database containing Artikli, ErrorRecords, TipoviObuce, Dobavljaci
- **AnalyticsDbContext** - Analytics database containing ProductsDim, StoresDim
=======
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
>>>>>>> d02157d9ed8b2a830e03a0a23003aa43726588f7
