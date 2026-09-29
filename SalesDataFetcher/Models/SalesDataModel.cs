using System.Text.Json.Serialization;

namespace SalesDataFetcher.Models;

/// <summary>
/// Represents the top-level JSON response from the PetPooja sales data API.
/// The API wraps records in an object with a "success" flag and a "Records" array.
/// </summary>
public class ApiResponse
{
    [JsonPropertyName("success")]
    public string? Success { get; set; }

    [JsonPropertyName("Records")]
    public List<SalesRecordDto>? Records { get; set; }
}

/// <summary>
/// DTO matching the exact JSON field names returned by the PetPooja API.
/// All values are strings as the API returns every field as a string.
/// Fields not needed for the database (order_status, packing_charge, etc.) are excluded.
/// </summary>
public class SalesRecordDto
{
    [JsonPropertyName("Receipt number")]
    public string? ReceiptNumber { get; set; }

    [JsonPropertyName("Receipt Date")]
    public string? ReceiptDate { get; set; }

    [JsonPropertyName("Transaction Time")]
    public string? TransactionTime { get; set; }

    [JsonPropertyName("Invoice amount")]
    public string? InvoiceAmount { get; set; }

    [JsonPropertyName("Discount amount")]
    public string? DiscountAmount { get; set; }

    [JsonPropertyName("Tax amount")]
    public string? TaxAmount { get; set; }

    [JsonPropertyName("Round Off")]
    public string? RoundOff { get; set; }

    [JsonPropertyName("Net sale")]
    public string? NetSale { get; set; }

    [JsonPropertyName("Payment Mode")]
    public string? PaymentMode { get; set; }

    [JsonPropertyName("Order Type")]
    public string? OrderType { get; set; }

    [JsonPropertyName("Transaction status")]
    public string? TransactionStatus { get; set; }
}

/// <summary>
/// Domain model representing a sales record as stored in the SQLite database.
/// Numeric fields use nullable doubles to safely handle missing or invalid values.
/// </summary>
public class SalesRecord
{
    public string? ReceiptNumber { get; set; }
    public string? SaleDate { get; set; }
    public string? TransactionTime { get; set; }
    public double? SaleAmount { get; set; }
    public double? TaxAmount { get; set; }
    public double? DiscountAmount { get; set; }
    public double? RoundOff { get; set; }
    public double? NetSale { get; set; }
    public string? PaymentMode { get; set; }
    public string? OrderType { get; set; }
    public string? TransactionStatus { get; set; }

    /// <summary>
    /// Maps a DTO from the API response to a SalesRecord for database storage.
    /// Converts string values to their appropriate types.
    /// </summary>
    public static SalesRecord FromDto(SalesRecordDto dto)
    {
        return new SalesRecord
        {
            ReceiptNumber = dto.ReceiptNumber,
            SaleDate = dto.ReceiptDate,
            TransactionTime = dto.TransactionTime,
            SaleAmount = ParseDouble(dto.InvoiceAmount),
            TaxAmount = ParseDouble(dto.TaxAmount),
            DiscountAmount = ParseDouble(dto.DiscountAmount),
            RoundOff = ParseDouble(dto.RoundOff),
            NetSale = ParseDouble(dto.NetSale),
            PaymentMode = dto.PaymentMode,
            OrderType = dto.OrderType,
            TransactionStatus = dto.TransactionStatus
        };
    }

    /// <summary>
    /// Safely parses a string to a nullable double.
    /// Returns null if the string is null or empty (genuinely missing).
    /// Logs a warning if the string is non-empty but not a valid number.
    /// </summary>
    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        Console.WriteLine($"[Warning] Could not parse numeric value: \"{value}\"");
        return null;
    }
}
