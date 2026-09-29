using Microsoft.Data.Sqlite;
using SalesDataFetcher.Models;

namespace SalesDataFetcher.Services;

/// <summary>
/// Service responsible for SQLite database operations.
/// Creates the sales_data table and inserts fetched sales records.
/// </summary>
public class SalesDbService : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Initializes the database service and opens a connection to the specified SQLite file.
    /// Creates the database file if it does not exist.
    /// </summary>
    /// <param name="dbPath">Absolute path to the SQLite database file</param>
    public SalesDbService(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
    }

    /// <summary>
    /// Creates the sales_data table if it does not already exist.
    /// Schema is exactly as specified in the machine test requirements.
    /// </summary>
    public void CreateTable()
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS sales_data (
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
            );";

        using var command = new SqliteCommand(sql, _connection);
        command.ExecuteNonQuery();

        Console.WriteLine("[DB] Table 'sales_data' is ready.");
    }

    /// <summary>
    /// Inserts a list of sales records into the sales_data table.
    /// Uses parameterized queries to prevent SQL injection.
    /// Wraps all inserts in a single transaction for atomicity —
    /// either all records are inserted or none are (on error, the transaction is rolled back).
    /// </summary>
    /// <param name="records">List of SalesRecord objects to insert</param>
    /// <returns>Number of records successfully inserted</returns>
    public int InsertRecords(List<SalesRecord> records)
    {
        if (records.Count == 0)
        {
            Console.WriteLine("[DB] No records to insert.");
            return 0;
        }

        const string sql = @"
            INSERT INTO sales_data (
                receipt_number, sale_date, transaction_time,
                sale_amount, tax_amount, discount_amount,
                round_off, net_sale, payment_mode,
                order_type, transaction_status
            ) VALUES (
                @receipt_number, @sale_date, @transaction_time,
                @sale_amount, @tax_amount, @discount_amount,
                @round_off, @net_sale, @payment_mode,
                @order_type, @transaction_status
            );";

        int insertedCount = 0;

        // Use a transaction for atomicity — all records insert or none do
        using var transaction = _connection.BeginTransaction();

        try
        {
            using var command = new SqliteCommand(sql, _connection, transaction);

            // Pre-define parameters once and reuse for each record (better performance)
            command.Parameters.Add("@receipt_number", SqliteType.Text);
            command.Parameters.Add("@sale_date", SqliteType.Text);
            command.Parameters.Add("@transaction_time", SqliteType.Text);
            command.Parameters.Add("@sale_amount", SqliteType.Real);
            command.Parameters.Add("@tax_amount", SqliteType.Real);
            command.Parameters.Add("@discount_amount", SqliteType.Real);
            command.Parameters.Add("@round_off", SqliteType.Real);
            command.Parameters.Add("@net_sale", SqliteType.Real);
            command.Parameters.Add("@payment_mode", SqliteType.Text);
            command.Parameters.Add("@order_type", SqliteType.Text);
            command.Parameters.Add("@transaction_status", SqliteType.Text);

            foreach (var record in records)
            {
                // Set parameter values; use DBNull.Value for null fields
                command.Parameters["@receipt_number"].Value = (object?)record.ReceiptNumber ?? DBNull.Value;
                command.Parameters["@sale_date"].Value = (object?)record.SaleDate ?? DBNull.Value;
                command.Parameters["@transaction_time"].Value = (object?)record.TransactionTime ?? DBNull.Value;
                command.Parameters["@sale_amount"].Value = (object?)record.SaleAmount ?? DBNull.Value;
                command.Parameters["@tax_amount"].Value = (object?)record.TaxAmount ?? DBNull.Value;
                command.Parameters["@discount_amount"].Value = (object?)record.DiscountAmount ?? DBNull.Value;
                command.Parameters["@round_off"].Value = (object?)record.RoundOff ?? DBNull.Value;
                command.Parameters["@net_sale"].Value = (object?)record.NetSale ?? DBNull.Value;
                command.Parameters["@payment_mode"].Value = (object?)record.PaymentMode ?? DBNull.Value;
                command.Parameters["@order_type"].Value = (object?)record.OrderType ?? DBNull.Value;
                command.Parameters["@transaction_status"].Value = (object?)record.TransactionStatus ?? DBNull.Value;

                command.ExecuteNonQuery();
                insertedCount++;
            }

            transaction.Commit();
            Console.WriteLine($"[DB] Successfully inserted {insertedCount} records into 'sales_data'.");
        }
        catch
        {
            // Roll back the entire transaction on any error
            transaction.Rollback();
            Console.WriteLine("[DB] Error during insertion — transaction rolled back.");
            throw;
        }

        return insertedCount;
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
