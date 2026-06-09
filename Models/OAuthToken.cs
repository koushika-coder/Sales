namespace Sales.Models;

public class OAuthToken
{
    public int OAuthTokenId { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = "Google"; // Google, Microsoft, etc.
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Foreign key
    public User? User { get; set; }
}
