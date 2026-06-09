namespace Sales.Models
{
    public class GmailConfiguration
    {
        public int Id { get; set; }

        public string GmailAddress { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
