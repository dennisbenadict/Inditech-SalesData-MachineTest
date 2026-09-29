using SalesDataFetcher.Services;

// ============================================================
// SalesDataFetcher — Machine Test: Fetch & Store Sales Data
// ============================================================
// Fetches sales data from the PetPooja API and stores it in
// a local SQLite database (sales_data.db) at the repository root.
//
// All API credentials are read from environment variables:
//   APP_KEY, APP_SECRET, ACCESS_TOKEN, REST_ID
//
// ============================================================

Console.WriteLine("=== SalesDataFetcher ===");
Console.WriteLine();

// --- Step 1: Read API credentials from environment variables ---
var appKey = Environment.GetEnvironmentVariable("APP_KEY");
var appSecret = Environment.GetEnvironmentVariable("APP_SECRET");
var accessToken = Environment.GetEnvironmentVariable("ACCESS_TOKEN");
var restId = Environment.GetEnvironmentVariable("REST_ID");
var fromDate = Environment.GetEnvironmentVariable("FROM_DATE") ?? "2025-05-03 00:00:00";
var toDate = Environment.GetEnvironmentVariable("TO_DATE") ?? "2025-05-30 23:59:19";

// Validate that all required credentials are provided
if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret) ||
    string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(restId))
{
    Console.WriteLine("ERROR: Missing required environment variables.");
    Console.WriteLine();
    Console.WriteLine("Please set the following environment variables before running:");
    Console.WriteLine("  APP_KEY        — PetPooja API application key");
    Console.WriteLine("  APP_SECRET     — PetPooja API application secret");
    Console.WriteLine("  ACCESS_TOKEN   — PetPooja API access token");
    Console.WriteLine("  REST_ID        — PetPooja restaurant ID");
    Console.WriteLine();
    Console.WriteLine("Optional (defaults are set for the task date range):");
    Console.WriteLine("  FROM_DATE      — Start date (default: 2025-05-03 00:00:00)");
    Console.WriteLine("  TO_DATE        — End date   (default: 2025-05-30 23:59:19)");
    Environment.Exit(1);
    return;
}

Console.WriteLine("[Config] Environment variables loaded successfully.");
Console.WriteLine($"[Config] Date range: {fromDate} to {toDate}");
Console.WriteLine();

// --- Step 2: Set database path (fixed at repository root) ---
var dbPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "sales_data.db"));
Console.WriteLine($"[Config] Database path: {dbPath}");
Console.WriteLine();

try
{
    // --- Step 3: Fetch sales data from the PetPooja API ---
    using var apiService = new SalesApiService();
    var records = await apiService.FetchSalesDataAsync(
        appKey, appSecret, accessToken, restId, fromDate, toDate);

    if (records.Count == 0)
    {
        Console.WriteLine("[Info] No records were returned by the API. Nothing to insert.");
        return;
    }

    Console.WriteLine();

    // --- Step 4: Store records in SQLite database ---
    using var dbService = new SalesDbService(dbPath);
    dbService.CreateTable();
    var inserted = dbService.InsertRecords(records);

    // --- Step 5: Print summary ---
    Console.WriteLine();
    Console.WriteLine("=== Summary ===");
    Console.WriteLine($"  Records fetched:  {records.Count}");
    Console.WriteLine($"  Records inserted: {inserted}");
    Console.WriteLine($"  Database file:    {dbPath}");
    Console.WriteLine();
    Console.WriteLine("Done.");
}
catch (HttpRequestException ex)
{
    // API errors — message is safe (never contains credentials)
    Console.WriteLine($"[ERROR] API request failed: {ex.Message}");
    Environment.Exit(1);
}
catch (Microsoft.Data.Sqlite.SqliteException ex)
{
    Console.WriteLine($"[ERROR] Database error: {ex.Message}");
    Environment.Exit(1);
}
catch (System.Text.Json.JsonException ex)
{
    Console.WriteLine($"[ERROR] Failed to parse API response: {ex.Message}");
    Environment.Exit(1);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"[ERROR] {ex.Message}");
    Environment.Exit(1);
}
catch (Exception ex)
{
    // Catch-all for unexpected errors — print only the message, not the stack trace
    Console.WriteLine($"[ERROR] Unexpected error: {ex.Message}");
    Environment.Exit(1);
}
