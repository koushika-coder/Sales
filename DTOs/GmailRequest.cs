namespace Sales.DTOs
{
    public class GmailRequest
    {
        
            public string? SenderEmail { get; set; }

            public string? SubjectKeyword { get; set; }

            public int MaxResults { get; set; } = 10;
    }
}
