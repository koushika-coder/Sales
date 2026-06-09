using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using Sales.Models;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sales.Services;

public interface IShopSaleService
{
    Task<ShopSaleDto?> CreateAsync(int userId, CreateShopSaleRequest request);
    //Task<ShopSaleDto?> ImportFromGmailAsync(int userId, ImportShopSaleFromGmailRequest request);
    //Task<ShopSaleDto?> ImportFromGmailSearchAsync(int userId, ImportShopSaleFromGmailSearchRequest request);
    //Task<ShopSaleDto?> ImportFromGmailWithAuthCodeAsync(int userId, GmailImportRequest request /*IGoogleOAuthService oauthService*/);
    //Task<ShopSaleDto?> ImportFromGmailSearchWithAuthCodeAsync(int userId, GmailSearchRequest request /*IGoogleOAuthService oauthService*/);
    Task<ShopSaleDto?> GetByIdAsync(int shopSaleId);
    Task<PaginatedResponse<ShopSaleDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10);
    Task<ShopSaleDto?> UpdateAsync(int shopSaleId, UpdateShopSaleRequest request);
    Task<bool> CommitAsync(int shopSaleId);
    Task<bool> DeleteAsync(int shopSaleId);
}

public class ShopSaleService : IShopSaleService
{
    private readonly SalesDbContext _context;

    public ShopSaleService(SalesDbContext context)
    {
        _context = context;
    }

    public async Task<ShopSaleDto?> CreateAsync(int userId, CreateShopSaleRequest request)
    {
        var shopSale = new ShopSale
        {
            UserId = userId,
            TransactionNumber = GenerateTransactionNumber("SS"),
            TransactionDate = request.TransactionDate,
            ProductDetails = request.ProductDetails,
            Quantity = request.Quantity,
            Amount = request.Amount,
            Status = "pending"
        };

        _context.ShopSales.Add(shopSale);
        await _context.SaveChangesAsync();

        await LogAudit(userId, "ShopSale", shopSale.ShopSaleId, "Created", null, shopSale);

        return MapToDto(shopSale);
    }

    public async Task<ShopSaleDto?> ImportFromGmailAsync(int userId, ImportShopSaleFromGmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.MessageId))
            return null;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var messageUrl = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{Uri.EscapeDataString(request.MessageId)}?format=full";
        var response = await client.GetAsync(messageUrl);
        if (!response.IsSuccessStatusCode)
            return null;

        var payloadText = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payloadText);

        var root = document.RootElement;
        if (!root.TryGetProperty("payload", out var payload))
            return null;

        var rawBodyText = ExtractBodyText(payload);
        var subject = GetHeaderValue(payload, "Subject");
        var internalDate = GetInternalDate(root);

        var content = string.IsNullOrWhiteSpace(rawBodyText) ? subject : rawBodyText;
        var parsed = ParseShopSaleContent(content);
        if (parsed == null)
            return null;

        parsed.TransactionDate ??= internalDate ?? DateTime.UtcNow;

        return await CreateAsync(userId, new CreateShopSaleRequest
        {
            ProductDetails = parsed.ProductDetails,
            Quantity = parsed.Quantity,
            Amount = parsed.Amount,
            TransactionDate = parsed.TransactionDate.Value
        });
    }

    public async Task<ShopSaleDto?> ImportFromGmailSearchAsync(int userId, ImportShopSaleFromGmailSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
            return null;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var queryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.SenderEmail))
            queryParts.Add($"from:{request.SenderEmail}");

        if (!string.IsNullOrWhiteSpace(request.SubjectKeyword))
            queryParts.Add($"subject:{request.SubjectKeyword}");

        if (!string.IsNullOrWhiteSpace(request.GmailQuery))
            queryParts.Add(request.GmailQuery);

        var query = string.Join(" ", queryParts.Where(part => !string.IsNullOrWhiteSpace(part)));
        var maxResults = request.MaxResults > 0 ? request.MaxResults : 10;

        var searchUrl = $"https://gmail.googleapis.com/gmail/v1/users/me/messages?q={Uri.EscapeDataString(query)}&maxResults={maxResults}&includeSpamTrash=false";
        var searchResponse = await client.GetAsync(searchUrl);
        if (!searchResponse.IsSuccessStatusCode)
            return null;

        var searchJson = await searchResponse.Content.ReadAsStringAsync();
        using var searchDocument = JsonDocument.Parse(searchJson);

        if (!searchDocument.RootElement.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array || messages.GetArrayLength() == 0)
            return null;

        var firstMessageId = messages.EnumerateArray()
            .Select(message => message.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        if (string.IsNullOrWhiteSpace(firstMessageId))
            return null;

        return await ImportFromGmailAsync(userId, new ImportShopSaleFromGmailRequest
        {
            AccessToken = request.AccessToken,
            MessageId = firstMessageId
        });
    }

    //public async Task<ShopSaleDto?> ImportFromGmailWithAuthCodeAsync(int userId, GmailImportRequest request, IGoogleOAuthService oauthService)
    //{
    //    if (string.IsNullOrWhiteSpace(request.AuthCode) || string.IsNullOrWhiteSpace(request.MessageId))
    //        return null;

    //    // Exchange auth code for access token
    //    var tokenResponse = await oauthService.ExchangeAuthCodeForTokenAsync(request.AuthCode);
    //    if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.access_token))
    //        return null;

    //    // Store tokens
    //    await oauthService.StoreGoogleTokensAsync(userId, tokenResponse);

    //    // Import using the access token
    //    return await ImportFromGmailAsync(userId, new ImportShopSaleFromGmailRequest
    //    {
    //        AccessToken = tokenResponse.access_token,
    //        MessageId = request.MessageId
    //    });
    //}

    //public async Task<ShopSaleDto?> ImportFromGmailSearchWithAuthCodeAsync(int userId, GmailSearchRequest request,/* IGoogleOAuthService oauthService*/)
    //{
    //    if (string.IsNullOrWhiteSpace(request.AuthCode))
    //        return null;

    //    // Get valid access token (exchange if needed or refresh if expired)
    //    var accessToken = await oauthService.GetValidAccessTokenAsync(userId);
        
    //    if (string.IsNullOrWhiteSpace(accessToken))
    //    {
    //        // Token not found or expired, exchange auth code
    //        var tokenResponse = await oauthService.ExchangeAuthCodeForTokenAsync(request.AuthCode);
    //        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.access_token))
    //            return null;

    //        await oauthService.StoreGoogleTokensAsync(userId, tokenResponse);
    //        accessToken = tokenResponse.access_token;
    //    }

    //    // Import using the valid access token
    //    return await ImportFromGmailSearchAsync(userId, new ImportShopSaleFromGmailSearchRequest
    //    {
    //        AccessToken = accessToken,
    //        SenderEmail = request.SenderEmail,
    //        SubjectKeyword = request.SubjectKeyword,
    //        GmailQuery = request.GmailQuery,
    //        MaxResults = request.MaxResults
    //    });
    //}

    public async Task<ShopSaleDto?> GetByIdAsync(int shopSaleId)
    {
        var shopSale = await _context.ShopSales.FirstOrDefaultAsync(s => s.ShopSaleId == shopSaleId);
        return shopSale != null ? MapToDto(shopSale) : null;
    }

    public async Task<PaginatedResponse<ShopSaleDto>> GetUserTransactionsAsync(int userId, int pageNumber = 1, int pageSize = 10)
    {
        var query = _context.ShopSales.Where(s => s.UserId == userId);
        var totalCount = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResponse<ShopSaleDto>
        {
            Items = transactions.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ShopSaleDto?> UpdateAsync(int shopSaleId, UpdateShopSaleRequest request)
    {
        var shopSale = await _context.ShopSales.FirstOrDefaultAsync(s => s.ShopSaleId == shopSaleId);
        if (shopSale == null || shopSale.Status != "pending")
            return null;

        var oldShopSale = new ShopSale
        {
            ProductDetails = shopSale.ProductDetails,
            Quantity = shopSale.Quantity,
            Amount = shopSale.Amount
        };

        if (!string.IsNullOrEmpty(request.ProductDetails))
            shopSale.ProductDetails = request.ProductDetails;

        if (request.Quantity.HasValue && request.Quantity > 0)
            shopSale.Quantity = request.Quantity.Value;

        if (request.Amount.HasValue && request.Amount > 0)
            shopSale.Amount = request.Amount.Value;

        shopSale.UpdatedAt = DateTime.UtcNow;
        _context.ShopSales.Update(shopSale);
        await _context.SaveChangesAsync();

        await LogAudit(shopSale.UserId, "ShopSale", shopSaleId, "Updated", oldShopSale, shopSale);

        return MapToDto(shopSale);
    }

    public async Task<bool> CommitAsync(int shopSaleId)
    {
        var shopSale = await _context.ShopSales.FirstOrDefaultAsync(s => s.ShopSaleId == shopSaleId);
        if (shopSale == null || shopSale.Status != "pending")
            return false;

        shopSale.Status = "committed";
        shopSale.UpdatedAt = DateTime.UtcNow;
        _context.ShopSales.Update(shopSale);
        await _context.SaveChangesAsync();

        await LogAudit(shopSale.UserId, "ShopSale", shopSaleId, "Committed", null, shopSale);

        return true;
    }

    public async Task<bool> DeleteAsync(int shopSaleId)
    {
        var shopSale = await _context.ShopSales.FirstOrDefaultAsync(s => s.ShopSaleId == shopSaleId && s.Status == "pending");
        if (shopSale == null)
            return false;

        _context.ShopSales.Remove(shopSale);
        await _context.SaveChangesAsync();

        await LogAudit(shopSale.UserId, "ShopSale", shopSaleId, "Deleted", shopSale, null);

        return true;
    }

    private static ShopSaleDto MapToDto(ShopSale shopSale)
    {
        return new ShopSaleDto
        {
            ShopSaleId = shopSale.ShopSaleId,
            UserId = shopSale.UserId,
            TransactionNumber = shopSale.TransactionNumber,
            TransactionDate = shopSale.TransactionDate,
            Amount = shopSale.Amount,
            ProductDetails = shopSale.ProductDetails,
            Quantity = shopSale.Quantity,
            Status = shopSale.Status,
            CreatedAt = shopSale.CreatedAt,
            UpdatedAt = shopSale.UpdatedAt
        };
    }

    private static string? GetHeaderValue(JsonElement payload, string headerName)
    {
        if (!payload.TryGetProperty("headers", out var headers) || headers.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var header in headers.EnumerateArray())
        {
            if (!header.TryGetProperty("name", out var nameProperty) || !header.TryGetProperty("value", out var valueProperty))
                continue;

            if (string.Equals(nameProperty.GetString(), headerName, StringComparison.OrdinalIgnoreCase))
                return valueProperty.GetString();
        }

        return null;
    }

    private static DateTime? GetInternalDate(JsonElement root)
    {
        if (!root.TryGetProperty("internalDate", out var internalDateProperty))
            return null;

        if (internalDateProperty.ValueKind == JsonValueKind.String && long.TryParse(internalDateProperty.GetString(), out var epochMillis))
            return DateTimeOffset.FromUnixTimeMilliseconds(epochMillis).UtcDateTime;

        if (internalDateProperty.ValueKind == JsonValueKind.Number && internalDateProperty.TryGetInt64(out var epochNumber))
            return DateTimeOffset.FromUnixTimeMilliseconds(epochNumber).UtcDateTime;

        return null;
    }

    private static string ExtractBodyText(JsonElement payload)
    {
        if (payload.TryGetProperty("body", out var body) &&
            body.TryGetProperty("data", out var bodyData) &&
            bodyData.ValueKind == JsonValueKind.String)
        {
            var decoded = DecodeBase64Url(bodyData.GetString() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(decoded))
                return decoded;
        }

        if (payload.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                var mimeType = part.TryGetProperty("mimeType", out var mimeTypeProperty)
                    ? mimeTypeProperty.GetString()
                    : null;

                var nestedText = ExtractBodyText(part);
                if (!string.IsNullOrWhiteSpace(nestedText) &&
                    (string.Equals(mimeType, "text/plain", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(mimeType, "text/html", StringComparison.OrdinalIgnoreCase)))
                {
                    return nestedText;
                }
            }

            foreach (var part in parts.EnumerateArray())
            {
                var nestedText = ExtractBodyText(part);
                if (!string.IsNullOrWhiteSpace(nestedText))
                    return nestedText;
            }
        }

        return string.Empty;
    }

    private static string DecodeBase64Url(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var padded = input.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ParsedShopSale? ParseShopSaleContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var normalized = content.Replace("\r", string.Empty);
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var zReport = TryParseZReportContent(lines);
        if (zReport != null)
            return zReport;

        string? productDetails = null;
        int? quantity = null;
        decimal? amount = null;
        DateTime? transactionDate = null;

        foreach (var line in lines)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line,
                @"^(?<key>productdetails|product|item|quantity|amount|total|transactiondate|date)\s*[:=]\s*(?<value>.+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
                continue;

            var key = match.Groups["key"].Value.ToLowerInvariant();
            var value = match.Groups["value"].Value.Trim();

            switch (key)
            {
                case "productdetails":
                case "product":
                case "item":
                    productDetails = value;
                    break;
                case "quantity":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedQuantity))
                        quantity = parsedQuantity;
                    break;
                case "amount":
                case "total":
                    if (TryParseDecimal(value, out var parsedAmount))
                        amount = parsedAmount;
                    break;
                case "transactiondate":
                case "date":
                    if (TryParseFlexibleDate(value, out var parsedDate))
                        transactionDate = parsedDate;
                    break;
            }
        }

        if (productDetails == null)
            productDetails = lines.FirstOrDefault();

        if (productDetails == null || !quantity.HasValue || !amount.HasValue)
            return null;

        return new ParsedShopSale
        {
            ProductDetails = productDetails,
            Quantity = quantity.Value,
            Amount = amount.Value,
            TransactionDate = transactionDate
        };
    }

    private static ParsedShopSale? TryParseZReportContent(IEnumerable<string> lines)
    {
        var lineList = lines.ToList();
        var reportId = FindRegexValue(lineList, @"Z-REPORT ID:\s*(?<value>\S+)");
        var transactionCountText = FindRegexValue(lineList, @"No of Transactions\s*:\s*(?<value>\d+)");
        var departmentTotalText = FindRegexValue(lineList, @"DEPARTMENT TOTAL\s+(?<value>[\d,]+\.\d{2})");
        var grandTotalText = FindRegexValue(lineList, @"GRAND TOTAL\s+(?<value>[\d,]+\.\d{2})");
        var printedAtText = FindRegexValue(lineList, @"Z-Report Printed by .* at (?<value>\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2})");
        var reportDateText = FindRegexValue(lineList, @"Date:\s*(?<value>\d{2}/\d{2}/\d{4})");

        if (reportId == null && departmentTotalText == null && grandTotalText == null && transactionCountText == null)
            return null;

        var amountText = departmentTotalText ?? grandTotalText;
        if (amountText == null || transactionCountText == null)
            return null;

        if (!TryParseDecimal(amountText, out var amount))
            return null;

        if (!int.TryParse(transactionCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
            return null;

        DateTime? transactionDate = null;
        if (printedAtText != null && TryParseFlexibleDate(printedAtText, out var printedDate))
            transactionDate = printedDate;
        else if (reportDateText != null && TryParseFlexibleDate(reportDateText, out var reportDate))
            transactionDate = reportDate;

        return new ParsedShopSale
        {
            ProductDetails = string.IsNullOrWhiteSpace(reportId) ? "Z-Report" : $"Z-Report {reportId}",
            Quantity = quantity,
            Amount = amount,
            TransactionDate = transactionDate
        };
    }

    private static string? FindRegexValue(IEnumerable<string> lines, string pattern)
    {
        foreach (var line in lines)
        {
            var match = System.Text.RegularExpressions.Regex.Match(line, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups["value"].Value.Trim();
        }

        return null;
    }

    private static bool TryParseFlexibleDate(string value, out DateTime dateTime)
    {
        var formats = new[]
        {
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm",
            "MM/dd/yyyy"
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dateTime))
            return true;

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out dateTime);
    }

    private static bool TryParseDecimal(string value, out decimal amount)
    {
        var sanitized = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == ',').ToArray()).Replace(",", string.Empty);
        return decimal.TryParse(sanitized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
    }

    private string GenerateTransactionNumber(string prefix)
    {
        return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 8)}";
    }

    private async Task LogAudit(int userId, string transactionType, int transactionId, string action, object? oldValues, object? newValues)
    {
        var audit = new TransactionAudit
        {
            UserId = userId,
            TransactionType = transactionType,
            TransactionId = transactionId,
            Action = action,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : string.Empty,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : string.Empty
        };

        _context.TransactionAudits.Add(audit);
        await _context.SaveChangesAsync();
    }

    private sealed class ParsedShopSale
    {
        public string ProductDetails { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Amount { get; set; }
        public DateTime? TransactionDate { get; set; }
    }
}
