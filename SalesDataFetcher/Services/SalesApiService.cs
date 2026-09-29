using System.Text.Json;
using SalesDataFetcher.Models;

namespace SalesDataFetcher.Services;

/// <summary>
/// Service responsible for fetching sales data from the PetPooja API.
/// Implements retry with exponential backoff for transient failures.
/// </summary>
public class SalesApiService : IDisposable
{
    private const string BaseUrl = "http://api.petpooja.com/V1/orders/get_sales_data/";
    private const int MaxRetries = 3;

    // Exponential backoff delays: 1s, 2s, 4s
    private static readonly int[] RetryDelaysMs = [1000, 2000, 4000];

    // HTTP status codes that should trigger a retry (server errors + rate limiting)
    private static readonly HashSet<int> RetryableStatusCodes = [429, 500, 502, 503, 504];

    private readonly HttpClient _httpClient;

    public SalesApiService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Fetches sales data from the PetPooja API and returns a list of SalesRecord objects.
    /// </summary>
    /// <param name="appKey">API application key</param>
    /// <param name="appSecret">API application secret</param>
    /// <param name="accessToken">API access token</param>
    /// <param name="restId">Restaurant ID</param>
    /// <param name="fromDate">Start date for the query range</param>
    /// <param name="toDate">End date for the query range</param>
    /// <returns>List of SalesRecord objects parsed from the API response</returns>
    public async Task<List<SalesRecord>> FetchSalesDataAsync(
        string appKey, string appSecret, string accessToken,
        string restId, string fromDate, string toDate)
    {
        // Build the request URL — never log this, it contains credentials in query params
        var url = $"{BaseUrl}" +
                  $"?app_key={Uri.EscapeDataString(appKey)}" +
                  $"&app_secret={Uri.EscapeDataString(appSecret)}" +
                  $"&access_token={Uri.EscapeDataString(accessToken)}" +
                  $"&restID={Uri.EscapeDataString(restId)}" +
                  $"&from_date={Uri.EscapeDataString(fromDate)}" +
                  $"&to_date={Uri.EscapeDataString(toDate)}";

        // Log only the base URL (no credentials)
        Console.WriteLine($"[API] Fetching sales data from {BaseUrl}");

        // Fetch with retry logic
        var json = await FetchWithRetryAsync(url);

        // Parse the JSON response into domain models
        return ParseResponse(json);
    }

    /// <summary>
    /// Performs an HTTP GET request with retry logic and exponential backoff.
    /// Retries on: network errors, timeouts, HTTP 5xx, and HTTP 429 (rate limit).
    /// Does NOT retry on: HTTP 401, 403, or other 4xx client errors.
    /// Maximum 3 attempts with delays of 1s, 2s, 4s between retries.
    /// </summary>
    private async Task<string> FetchWithRetryAsync(string url)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            bool isLastAttempt = attempt == MaxRetries;

            // --- Try to get an HTTP response ---
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(url);
            }
            catch (TaskCanceledException) when (!isLastAttempt)
            {
                // Timeout — retry with backoff
                var delay = RetryDelaysMs[attempt - 1];
                Console.WriteLine($"[API] Attempt {attempt}/{MaxRetries} — timed out. Waiting {delay}ms before retry...");
                await Task.Delay(delay);
                continue;
            }
            catch (HttpRequestException) when (!isLastAttempt)
            {
                // Network error (DNS failure, connection refused, etc.) — retry with backoff
                var delay = RetryDelaysMs[attempt - 1];
                Console.WriteLine($"[API] Attempt {attempt}/{MaxRetries} — network error. Waiting {delay}ms before retry...");
                await Task.Delay(delay);
                continue;
            }
            // On the last attempt, exceptions passed back naturally to the caller.

            // --- We have a response — evaluate the status code ---
            using (response)
            {
                var statusCode = (int)response.StatusCode;
                Console.WriteLine($"[API] Attempt {attempt}/{MaxRetries} — HTTP {statusCode}");

                // Success — return the body
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                // Non-retryable client error (4xx except 429): fail immediately, do not retry
                if (statusCode >= 400 && statusCode < 500 && !RetryableStatusCodes.Contains(statusCode))
                {
                    throw new HttpRequestException(
                        $"API returned HTTP {statusCode}. Check your credentials and parameters.");
                }

                // Retryable server error (5xx or 429)
                if (!isLastAttempt)
                {
                    var delay = RetryDelaysMs[attempt - 1];
                    Console.WriteLine($"[API] Retryable error (HTTP {statusCode}). Waiting {delay}ms before retry...");
                    await Task.Delay(delay);
                }
                else
                {
                    throw new HttpRequestException(
                        $"API request failed after {MaxRetries} attempts. Last HTTP status: {statusCode}.");
                }
            }
        }

        // Unreachable due to throws above, but required by the compiler
        throw new HttpRequestException($"API request failed after {MaxRetries} attempts.");
    }

    /// <summary>
    /// Parses the raw JSON response and maps each record to a SalesRecord.
    /// Validates the API response structure before processing.
    /// </summary>
    private static List<SalesRecord> ParseResponse(string json)
    {
        var apiResponse = JsonSerializer.Deserialize<ApiResponse>(json);

        if (apiResponse == null)
        {
            throw new JsonException("Failed to deserialize API response — result was null.");
        }

        if (apiResponse.Success != "1")
        {
            throw new InvalidOperationException(
                $"API returned unsuccessful response (success: \"{apiResponse.Success}\").");
        }

        if (apiResponse.Records == null || apiResponse.Records.Count == 0)
        {
            Console.WriteLine("[API] Warning: API returned no records.");
            return [];
        }

        Console.WriteLine($"[API] Successfully fetched {apiResponse.Records.Count} records.");

        // Map each DTO to a domain model (parses string values to proper types)
        return apiResponse.Records
            .Select(SalesRecord.FromDto)
            .ToList();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
