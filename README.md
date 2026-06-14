# TrixCompareDb

## 📋 Project Overview

**TrixCompareDb** is a database comparison tool that allows you to compare tables between two SQL Server databases side-by-side. It consists of:

- **Backend API** (.NET 8 ASP.NET Core): Manages database connections, retrieves table structures and data, and performs comparisons
- **Frontend** (Vue 3 + Vite): Provides an interactive UI for selecting databases, tables, and viewing comparison results

### Key Features

- 🔍 Compare tables between multiple SQL Server databases
- 📊 View side-by-side data comparison with highlighting for differences
- 🗄️ Dynamically fetch available databases and tables
- ⚡ Real-time filtering and result visualization
- 🎨 Clean UI

---

## 🔧 Prerequisites

### Backend (.NET)
- **.NET 8** SDK or later ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- **SQL Server** (or SQL Server Express/LocalDB)
  - The project uses LocalDB by default. Alternatively, use SQL Server 2019+ or Azure SQL Database.
- **Visual Studio 2022** (recommended) or Visual Studio Code

### Frontend (Node.js)
- **Node.js 18+** ([Download](https://nodejs.org/))
- **npm** 9+ (comes with Node.js)

---

## 🚀 Getting Started

### Step 1: Clone the Repository

```
git clone https://github.com/MatrixAna/TrixCompareDb.git
cd TrixCompareDb
```

### Step 2: Configure Backend Connection Strings

#### Option A: Using LocalDB (Quick Setup)

The default configuration already uses LocalDB. No changes needed if you have SQL Server Express or LocalDB installed.

#### Option B: Using a Different SQL Server

Edit `appsettings.json` in the root of the backend project:

```
"ConnectionStrings": {
  "TrixCompareDbTest": "Server=YOUR_SERVER;Database=TrixCompareDbTest;Trusted_Connection=True;",
  "TrixCompareDbProd": "Server=YOUR_SERVER;Database=TrixCompareDbProd;Trusted_Connection=True;"
}
```

Replace:
- `YOUR_SERVER` with your SQL Server instance name (e.g., `localhost`, `DESKTOP-ABC123\SQLEXPRESS`, or `server.database.windows.net` for Azure)
- Add credentials if not using Windows Authentication:
```
  Server=YOUR_SERVER;Database=TrixCompareDbTest;User Id=sa;Password=YourPassword;
```

### Step 3: Set Up Backend

#### 3.1 Restore NuGet Packages
```
dotnet restore
```

#### 3.2 Create Databases

manually create the databases in SQL Server:
```
CREATE DATABASE TrixCompareDbTest;
CREATE DATABASE TrixCompareDbProd;
```

#### 3.3 Run the Backend API

```
dotnet run
```

The API will start at a different port depending the device:
- **HTTP** / **HTTPS**: check on the terminal window after running the backend `Listening on http://localhost/portnumber` or `Listening on https://localhost/portnumber`
- **Swagger UI**: `http://localhost:portnumber/swagger`

> **Note**: If you encounter HTTPS certificate issues, run:
> ```bash
> dotnet dev-certs https --trust
> ```

### Step 4: Configure Frontend

#### 4.1 Install Dependencies

```
cd frontend
npm install
```

#### 4.2 Configure Backend URL

```

Edit `.env` and set the backend URL (leave empty to use the Vite proxy):
VITE_API_BASE_URL=http://localhost:portnumber
```

**Options:**
- **Leave empty** (default): The Vite dev server will proxy `/api` requests to the backend
- **Set to backend URL**: Makes direct requests to the backend (requires CORS to be enabled)

#### 4.3 Run the Frontend Dev Server

```
npm run dev
```

The frontend will start at `http://localhost:5173`

---

## 🔌 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/databases` | Get list of configured database keys |
| `GET` | `/api/tables?database={name}` | Get list of tables in a database |
| `POST` | `/api/compare` | Compare a table between two databases |

### Example: Compare Tables

**Request:**
```
POST /api/compare

{
  "databaseSource": "TrixCompareDbTest",
  "databaseTarget": "TrixCompareDbProd",
  "tableName": "dbo.Products"
}
```

**Response:**
```
[
  {
    "status": "Equal",
    "source": { "id": 1, "name": "Product A", "price": 10.50 },
    "target": { "id": 1, "name": "Product A", "price": 10.50 }
  },
  {
    "status": "Different",
    "source": { "id": 2, "name": "Product B", "price": 20.00 },
    "target": { "id": 2, "name": "Product B", "price": 25.00 }
  }
]
```

---

## 📁 Project Structure

```
TrixCompareDb/
├── Controllers/              # API endpoints
│   ├── DatabasesController.cs
│   ├── TablesController.cs
│   └── CompareController.cs
├── Data/
│   ├── AppDbContext.cs       # EF Core context
│   └── TableRepository.cs    # Database access logic
├── Services/
│   └── CompareTables.cs      # Comparison logic
├── Models/
│   └── CompareRequest.cs
├── Properties/
│   └── launchSettings.json   # Launch profiles
├── appsettings.json          # Configuration
├── Program.cs                # App setup
├── frontend/                 # Vue 3 frontend
│   ├── src/
│   │   ├── components/
│   │   │   ├── CompareDashboard.vue
│   │   │   └── CompareTables.vue
│   │   └── main.js
│   ├── vite.config.mjs
│   ├── .env.example          # Frontend config template
│   └── package.json
└── README.md
```

---

## 🔄 Collaboration Workflow

### For Team Members:

1. **Clone the repo:**
```
   git clone https://github.com/MatrixAna/TrixCompareDb.git
   cd TrixCompareDb
```

2. **Set up your local configuration:**
   - Edit `appsettings.json` with your local database connection strings
   - Edit `frontend/.env` with your local backend URL
   - Edit the url on `TrixCompareDb.http` with your local backend url


3. **Run the project:**
```
   # Terminal 1: Backend
   dotnet run

   # Terminal 2: Frontend
   cd frontend
   npm run dev
```

4. **Access the app:**
   - Frontend: `http://localhost:5173`
   - API Swagger: `http://localhost:portnumber/swagger`

---

## 🛠️ Development Workflow

### Building the Project

```
# Backend
dotnet build

# Frontend
cd frontend
npm run build
```

### Code Style & Formatting

- **Backend**: Follows C# naming conventions (PascalCase for public members)
- **Frontend**: Uses ESLint and Prettier (configured in `frontend/`)

---

## 📝 Configuration Reference

### Backend (appsettings.json)

```
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "TrixCompareDbTest": "Server=(localdb)\\MSSQLLocalDB;Database=TrixCompareDb;Trusted_Connection=True;",
    "TrixCompareDbProd": "Server=(localdb)\\ProjectModels;Database=TrixCompareDb;Trusted_Connection=True;"
  },
  "AllowedHosts": "*"
}
```

### Frontend (.env)

```
VITE_API_BASE_URL=http://localhost:5187
```

---

## 🐛 Troubleshooting

### CORS Errors

**Problem**: Frontend can't reach backend API

**Solutions**:
1. Ensure backend is running: `dotnet run`
2. Check backend URL in `.env` file
3. If using direct URL (not proxy), ensure CORS is enabled in `Program.cs`
4. Try using Vite proxy by leaving `VITE_API_BASE_URL` empty

### HTTPS Certificate Issues

**Problem**: HTTPS requests fail with certificate warnings

**Solution**:
```
dotnet dev-certs https --trust
```

### Database Connection Errors

**Problem**: "Connection string not found"

**Solutions**:
1. Check `appsettings.json` connection strings
2. Verify SQL Server is running
3. Test connection with SQL Server Management Studio
4. For LocalDB: `sqlcmd -S (localdb)\MSSQLLocalDB`

### Tables Not Loading

**Problem**: Frontend shows "No tables available"

**Solutions**:
1. Verify database exists and contains tables
2. Check API logs: `GET /api/tables?database=TrixCompareDbTest`
3. Ensure tables use standard schema (default: `dbo`)

---

## 📚 Useful Resources

- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [ASP.NET Core Web API](https://learn.microsoft.com/en-us/aspnet/core/web-api/)
- [Vue 3 Guide](https://vuejs.org/guide/introduction.html)
- [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)

---

## 📄 License

This project is part of the TrixCompare initiative. For more information, visit the [GitHub Repository](https://github.com/MatrixAna/TrixCompareDb).

---

## 🤝 Contributing

To contribute:
1. Create a feature branch: `git checkout -b feature/your-feature`
2. Make your changes and test locally
3. Commit with clear messages: `git commit -m "Add feature description"`
4. Push to your branch: `git push origin feature/your-feature`
5. Submit a Pull Request

**Important**: Never commit connection strings or local configuration files. Always use `.env` and `appsettings.Development.json` for local overrides.

---

## ❓ Questions?

For issues or questions, please open an [Issue on GitHub](https://github.com/MatrixAna/TrixCompareDb/issues).