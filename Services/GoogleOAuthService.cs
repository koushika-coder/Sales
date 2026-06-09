//using Microsoft.EntityFrameworkCore;
//using Sales.Data;
//using Sales.DTOs;
//using Sales.Models;
//using System.Text;
//using System.Text.Json;

//namespace Sales.Services;

//public interface IGoogleOAuthService
//{
//    Task<GoogleTokenResponse?> ExchangeAuthCodeForTokenAsync(string authCode);
//    Task<GoogleTokenResponse?> RefreshAccessTokenAsync(string refreshToken);
//    Task<bool> StoreGoogleTokensAsync(int userId, GoogleTokenResponse tokenResponse);
//    Task<string?> GetValidAccessTokenAsync(int userId);
//    Task<bool> ClearGoogleTokensAsync(int userId);
//}

//public class GoogleOAuthService : IGoogleOAuthService
//{
//    private readonly IHttpClientFactory _httpClientFactory;
//    private readonly IConfiguration _configuration;
//    private readonly SalesDbContext _context;

//    public GoogleOAuthService(
//        IHttpClientFactory httpClientFactory,
//        IConfiguration configuration,
//        SalesDbContext context)
//    {
//        _httpClientFactory = httpClientFactory;
//        _configuration = configuration;
//        _context = context;
//    }

//    public async Task<GoogleTokenResponse?> ExchangeAuthCodeForTokenAsync(string authCode)
//    {
//        if (string.IsNullOrWhiteSpace(authCode))
//            return null;

//        var clientId = _configuration["GoogleOAuth:ClientId"];
//        var clientSecret = _configuration["GoogleOAuth:ClientSecret"];
//        var redirectUri = _configuration["GoogleOAuth:RedirectUri"];
//        var tokenEndpoint = _configuration["GoogleOAuth:TokenEndpoint"];

//        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
//            return null;

//        using var client = _httpClientFactory.CreateClient();
//        var content = new FormUrlEncodedContent(new Dictionary<string, string>
//        {
//            { "code", authCode },
//            { "client_id", clientId },
//            { "client_secret", clientSecret },
//            { "redirect_uri", redirectUri ?? "https://localhost:7276/api/ShopSales/oauth-callback" },
//            { "grant_type", "authorization_code" }
//        });

//        try
//        {
//            var response = await client.PostAsync(tokenEndpoint, content);
//            if (!response.IsSuccessStatusCode)
//            {
//                var errorContent = await response.Content.ReadAsStringAsync();
//                System.Diagnostics.Debug.WriteLine($"Token exchange error: {errorContent}");
//                return null;
//            }

//            var jsonResponse = await response.Content.ReadAsStringAsync();
//            using var document = JsonDocument.Parse(jsonResponse);
//            var root = document.RootElement;

//            return new GoogleTokenResponse
//            {
//                access_token = root.TryGetProperty("access_token", out var at) ? at.GetString() : null,
//                refresh_token = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
//                expires_in = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600,
//                token_type = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer"
//            };
//        }
//        catch (Exception ex)
//        {
//            System.Diagnostics.Debug.WriteLine($"Error exchanging auth code: {ex.Message}");
//            return null;
//        }
//    }

//    public async Task<GoogleTokenResponse?> RefreshAccessTokenAsync(string refreshToken)
//    {
//        if (string.IsNullOrWhiteSpace(refreshToken))
//            return null;

//        var clientId = _configuration["GoogleOAuth:ClientId"];
//        var clientSecret = _configuration["GoogleOAuth:ClientSecret"];
//        var tokenEndpoint = _configuration["GoogleOAuth:TokenEndpoint"];

//        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
//            return null;

//        using var client = _httpClientFactory.CreateClient();
//        var content = new FormUrlEncodedContent(new Dictionary<string, string>
//        {
//            { "client_id", clientId },
//            { "client_secret", clientSecret },
//            { "refresh_token", refreshToken },
//            { "grant_type", "refresh_token" }
//        });

//        try
//        {
//            var response = await client.PostAsync(tokenEndpoint, content);
//            if (!response.IsSuccessStatusCode)
//            {
//                var errorContent = await response.Content.ReadAsStringAsync();
//                System.Diagnostics.Debug.WriteLine($"Token refresh error: {errorContent}");
//                return null;
//            }

//            var jsonResponse = await response.Content.ReadAsStringAsync();
//            using var document = JsonDocument.Parse(jsonResponse);
//            var root = document.RootElement;

//            return new GoogleTokenResponse
//            {
//                access_token = root.TryGetProperty("access_token", out var at) ? at.GetString() : null,
//                refresh_token = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken,
//                expires_in = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600,
//                token_type = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer"
//            };
//        }
//        catch (Exception ex)
//        {
//            System.Diagnostics.Debug.WriteLine($"Error refreshing token: {ex.Message}");
//            return null;
//        }
//    }

//    //public async Task<bool> StoreGoogleTokensAsync(int userId, GoogleTokenResponse tokenResponse)
//    //{
//    //    try
//    //    {
//    //        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
//    //        if (user == null)
//    //            return false;

//    //        user.GoogleAccessToken = tokenResponse.access_token;
//    //        user.GoogleRefreshToken = tokenResponse.refresh_token;
//    //        user.GoogleTokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 300); // 5 min buffer

//    //        _context.Users.Update(user);
//    //        await _context.SaveChangesAsync();
//    //        return true;
//    //    }
//    //    catch (Exception ex)
//    //    {
//    //        System.Diagnostics.Debug.WriteLine($"Error storing Google tokens: {ex.Message}");
//    //        return false;
//    //    }
//    //}

//    //public async Task<string?> GetValidAccessTokenAsync(int userId)
//    //{
//    //    try
//    //    {
//    //        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
//    //        if (user == null || string.IsNullOrWhiteSpace(user.GoogleAccessToken))
//    //            return null;

//    //        // Check if token is expired or about to expire
//    //        if (user.GoogleTokenExpiresAt.HasValue && DateTime.UtcNow >= user.GoogleTokenExpiresAt)
//    //        {
//    //            // Token expired, try to refresh
//    //            if (!string.IsNullOrWhiteSpace(user.GoogleRefreshToken))
//    //            {
//    //                var newTokenResponse = await RefreshAccessTokenAsync(user.GoogleRefreshToken);
//    //                if (newTokenResponse != null && !string.IsNullOrWhiteSpace(newTokenResponse.access_token))
//    //                {
//    //                    await StoreGoogleTokensAsync(userId, newTokenResponse);
//    //                    return newTokenResponse.access_token;
//    //                }
//    //            }
//    //            return null; // Token expired and couldn't refresh
//    //        }

//    //        return user.GoogleAccessToken;
//    //    }
//    //    catch (Exception ex)
//    //    {
//    //        System.Diagnostics.Debug.WriteLine($"Error getting valid access token: {ex.Message}");
//    //        return null;
//    //    }
//    //}

//    public async Task<bool> ClearGoogleTokensAsync(int userId)
//    {
//        try
//        {
//            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
//            if (user == null)
//                return false;

//            user.GoogleAccessToken = null;
//            user.GoogleRefreshToken = null;
//            user.GoogleTokenExpiresAt = null;

//            _context.Users.Update(user);
//            await _context.SaveChangesAsync();
//            return true;
//        }
//        catch (Exception ex)//        {
//            System.Diagnostics.Debug.WriteLine($"Error clearing Google tokens: {ex.Message}");
//            return false;
//        }
//    }
//}
