using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Sales.Data;
using Sales.DTOs;
using System.Text;

namespace Sales.Services
{
    public class GmailServiceImpl : IGmailService
    {
        private readonly SalesDbContext _context;
        private readonly IConfiguration _configuration;

        public GmailServiceImpl(
            SalesDbContext context,
            IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private async Task<string> GetAccessTokenAsync(
            string refreshToken)
        {
            using var client = new HttpClient();

            var values = new Dictionary<string, string>
            {
                { "client_id", _configuration["GoogleOAuth:ClientId"]! },
                { "client_secret", _configuration["GoogleOAuth:ClientSecret"]! },
                { "refresh_token", refreshToken },
                { "grant_type", "refresh_token" }
            };

            var response = await client.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(values));

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Google token exchange failed ({response.StatusCode}): {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();

            using var doc =
                System.Text.Json.JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("access_token")
                .GetString()!;
        }

        public async Task<List<GmailMessageResponse>>
            GetEmailsAsync(GmailRequest request)
        {
            var config = await _context.GmailConfiguration
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedDate)
                .FirstOrDefaultAsync();

            if (config == null)
                throw new Exception(
                    "Active Gmail configuration not found");

            string accessToken =
                await GetAccessTokenAsync(
                    config.RefreshToken);

            var credential =
                GoogleCredential.FromAccessToken(
                    accessToken);

            var gmailService =
                new GmailService(
                    new BaseClientService.Initializer
                    {
                        HttpClientInitializer =
                            credential,
                        ApplicationName =
                            "Sales Gmail Reader"
                    });

            var gmailRequest =
                gmailService.Users.Messages.List("me");

            if (!string.IsNullOrWhiteSpace(
                request.SenderEmail))
            {
                gmailRequest.Q =
                    $"from:{request.SenderEmail}";
            }

            if (!string.IsNullOrWhiteSpace(
                request.SubjectKeyword))
            {
                gmailRequest.Q +=
                    $" subject:\"{request.SubjectKeyword}\"";
            }

            if (request.AfterDate.HasValue)
                gmailRequest.Q += $" after:{request.AfterDate.Value:yyyy/MM/dd}";

            if (request.BeforeDate.HasValue)
                gmailRequest.Q += $" before:{request.BeforeDate.Value:yyyy/MM/dd}";

            gmailRequest.MaxResults =
                request.MaxResults;

            var result =
                await gmailRequest.ExecuteAsync();

            var emails =
                new List<GmailMessageResponse>();

            if (result.Messages == null)
                return emails;

            foreach (var item in result.Messages)
            {
                var getRequest =
                    gmailService.Users.Messages.Get("me", item.Id);

                getRequest.Format =
                    UsersResource.MessagesResource
                        .GetRequest
                        .FormatEnum.Full;

                var message =
                    await getRequest.ExecuteAsync();
                Console.WriteLine(
    $"Subject: {message.Snippet}");

                Console.WriteLine(
                    $"MimeType: {message.Payload?.MimeType}");

                if (message.Payload?.Parts != null)
                {
                    foreach (var part in message.Payload.Parts)
                    {
                        Console.WriteLine(
                            $"Part MimeType: {part.MimeType}");

                        //Console.WriteLine(
                        //    $"FileName: {part.FileName}");

                        Console.WriteLine(
                            $"AttachmentId: {part.Body?.AttachmentId}");
                    }
                }

                emails.Add(
                    new GmailMessageResponse
                    {
                        Id = message.Id,

                        Subject = message.Payload?.Headers
                            ?.FirstOrDefault(x => x.Name == "Subject")
                            ?.Value ?? "",

                        From = message.Payload?.Headers
                            ?.FirstOrDefault(x => x.Name == "From")
                            ?.Value ?? "",

                        Date = message.Payload?.Headers
                            ?.FirstOrDefault(x => x.Name == "Date")
                            ?.Value ?? "",

                        Body = await GetEmailBody(gmailService, message)
                    });
            }

            return emails;
        }

        private async Task<string> GetEmailBody(
     GmailService gmailService,
     Message message)
        {
            if (!string.IsNullOrEmpty(
                message.Payload?.Body?.Data))
            {
                return DecodeBase64(
                    message.Payload.Body.Data);
            }

            var body = await ExtractContentAsync(
                gmailService,
                message,
                message.Payload);

            if (!string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            return message.Snippet ?? string.Empty;
        }

        private string DecodeBase64(string input)
        {
            string output = input
                .Replace("-", "+")
                .Replace("_", "/");

            switch (output.Length % 4)
            {
                case 2:
                    output += "==";
                    break;

                case 3:
                    output += "=";
                    break;
            }

            byte[] bytes = Convert.FromBase64String(output);

            return Encoding.UTF8.GetString(bytes);
        }


        private async Task<string> ExtractContentAsync(
        GmailService gmailService,
        Message message,
        MessagePart? part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            if ((part.MimeType == "text/plain" ||
                 part.MimeType == "text/html")
                &&
                !string.IsNullOrEmpty(
                    part.Body?.Data))
            {
                return DecodeBase64(
                    part.Body.Data);
            }

            if (!string.IsNullOrEmpty(
                part.Body?.AttachmentId))
            {
                try
                {
                    var attachmentRequest =
                        gmailService
                            .Users
                            .Messages
                            .Attachments
                            .Get(
                                "me",
                                message.Id,
                                part.Body.AttachmentId);

                    var attachment =
                        await attachmentRequest.ExecuteAsync();

                    if (!string.IsNullOrEmpty(
                        attachment.Data))
                    {
                        return DecodeBase64(
                            attachment.Data);
                    }
                }
                catch
                {
                }
            }

            if (part.Parts != null)
            {
                foreach (var childPart in part.Parts)
                {
                    var content =
                        await ExtractContentAsync(
                            gmailService,
                            message,
                            childPart);

                    if (!string.IsNullOrWhiteSpace(
                        content))
                    {
                        return content;
                    }
                }
            }

            return string.Empty;
        }
    }

    public interface IGmailService
    {
        Task<List<GmailMessageResponse>> GetEmailsAsync(
            GmailRequest request);

    }
}