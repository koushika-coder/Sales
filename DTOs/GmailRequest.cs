namespace Sales.DTOs
{
    public class GmailRequest
    {
        public string? SenderEmail { get; set; }
        public string? SubjectKeyword { get; set; }
        public int MaxResults { get; set; } = 10;

        // Optional date window for historical Z-Report lookup
        // Gmail query format:  after:YYYY/MM/DD before:YYYY/MM/DD
        public DateOnly? AfterDate { get; set; }
        public DateOnly? BeforeDate { get; set; }
    }
}
