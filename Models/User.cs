namespace Sales.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // user or admin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    ////// Google OAuth Properties
    //public string? GoogleAccessToken { get; set; }
    //public string? GoogleRefreshToken { get; set; }
    //public DateTime? GoogleTokenExpiresAt { get; set; }

    //// Navigation property
    // public ICollection<Order> Orders { get; set; } = new List<Order>();
}
