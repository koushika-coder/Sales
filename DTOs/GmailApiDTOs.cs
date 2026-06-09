namespace Sales.DTOs
{
    public class GmailApiDTOs
    {
        public class GmailMessageResponse
        {
            public string Id { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string From { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;

            public string Body { get; set; } = string.Empty;
        }


        public class GmailRequest
        {
            public string AccessToken { get; set; } = string.Empty;
            public string? SenderEmail { get; set; }
            public string? SubjectKeyword { get; set; }
            public int MaxResults { get; set; } = 10;
        }
    }
}

