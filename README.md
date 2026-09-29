# SalesDataFetcher — Machine Test

A .NET 8 console application that fetches sales data from the PetPooja API and stores it in a local SQLite database.

## Project Structure

```
Inditech_SalesData_MachineTest/
├── SalesDataFetcher/
│   ├── Program.cs                    # Entry point and orchestration
│   ├── SalesDataFetcher.csproj       # .NET 8 project file
│   ├── Models/
│   │   └── SalesDataModel.cs         # API response DTOs and database model
│   └── Services/
│       ├── SalesApiService.cs        # API fetch with retry mechanism
│       └── SalesDbService.cs         # SQLite table creation and data insertion
├── sales_data.db                     # Generated SQLite database
├── .env.example                      # Template for environment variables
├── .gitignore
└── README.md
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd Inditech_SalesData_MachineTest
```

### 2. Set Environment Variables

The application reads API credentials from OS environment variables. No credentials are stored in the source code or configuration files.

**PowerShell (Windows):**

```powershell
$env:APP_KEY = "<your-app-key>"
$env:APP_SECRET = "<your-app-secret>"
$env:ACCESS_TOKEN = "<your-access-token>"
$env:REST_ID = "<your-restaurant-id>"
```

**Linux / macOS:**

```bash
export APP_KEY="<your-app-key>"
export APP_SECRET="<your-app-secret>"
export ACCESS_TOKEN="<your-access-token>"
export REST_ID="<your-restaurant-id>"
```

Optional variables (defaults match the task specification):

| Variable    | Default               |
|-------------|-----------------------|
| `FROM_DATE` | `2025-05-03 00:00:00` |
| `TO_DATE`   | `2025-05-30 23:59:19` |

### 3. Run the Application

From the **repository root**:

```bash
dotnet run --project SalesDataFetcher
```

This will:

1. Fetch sales data from the PetPooja API
2. Create the `sales_data` table in SQLite (if it does not exist)
3. Insert all fetched records
4. Generate `sales_data.db` at the repository root

## Re-Run Behavior

Running the application again appends newly fetched records to the existing `sales_data.db`. Delete `sales_data.db` before running if a completely fresh database is required.

## Database

### Schema

The SQLite database contains a single table with the following exact schema:

```sql
CREATE TABLE sales_data (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    receipt_number TEXT,
    sale_date TEXT,
    transaction_time TEXT,
    sale_amount REAL,
    tax_amount REAL,
    discount_amount REAL,
    round_off REAL,
    net_sale REAL,
    payment_mode TEXT,
    order_type TEXT,
    transaction_status TEXT
);
```

### API-to-Database Field Mapping

| API Field (JSON Key)  | Database Column      |
|-----------------------|----------------------|
| Receipt number        | receipt_number       |
| Receipt Date          | sale_date            |
| Transaction Time      | transaction_time     |
| Invoice amount        | sale_amount          |
| Tax amount            | tax_amount           |
| Discount amount       | discount_amount      |
| Round Off             | round_off            |
| Net sale              | net_sale             |
| Payment Mode          | payment_mode         |
| Order Type            | order_type           |
| Transaction status    | transaction_status   |

All 11 database columns (excluding the auto-increment `id`) are mapped to fields present in the API response. The `transaction_status` field preserves the API values exactly (e.g. `SALE`, `RETURN`).

## Error Handling

- **API failures**: Network errors, timeouts, and server errors are handled with clear error messages.
- **Invalid JSON**: If the API returns malformed JSON, the error is caught and reported.
- **Database errors**: SQLite exceptions (connection, insertion) are caught and reported.
- **Missing credentials**: If required environment variables are not set, the application prints setup instructions and exits.
- **Malformed numeric data**: If a numeric field contains a value that cannot be parsed, a warning is printed and `NULL` is stored.

## Retry Mechanism

The application retries failed API requests with exponential backoff:

| Condition                  | Action                        |
|----------------------------|-------------------------------|
| Network failure            | Retry (up to 3 attempts)      |
| Request timeout            | Retry (up to 3 attempts)      |
| HTTP 5xx (server error)    | Retry (up to 3 attempts)      |
| HTTP 429 (rate limited)    | Retry (up to 3 attempts)      |
| HTTP 401/403 (auth error)  | Fail immediately, no retry    |
| Other HTTP 4xx             | Fail immediately, no retry    |

Backoff delays: 1 second, 2 seconds, 4 seconds.

## Assumptions

- The API endpoint and credentials are provided by the machine test specification.
- The API returns all field values as JSON strings; numeric values are parsed to `REAL` during insertion.
- `Transaction Time` is a separate field in the API response (not derived from `Receipt Date`).
- The supplied API uses HTTP. HTTPS was tested and is not supported by the server, so HTTP is retained as specified in the task.
- The application does not deduplicate records. Each run inserts all records returned by the API.
- All database inserts are wrapped in a single transaction: either all records are inserted or none are.

## Technologies

- .NET 8
- Microsoft.Data.Sqlite
- System.Text.Json (built-in)
