using System.Net;
using System.Net.Mail;
using System.Text;
using Sales.DTOs;

namespace Sales.Services
{
    public interface IEmailService
    {
        Task SendComparisonEmailAsync(ZReportComparisonResponse comparison, bool committed);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendComparisonEmailAsync(ZReportComparisonResponse comparison, bool committed)
        {
            var host      = _config["Email:SmtpHost"]      ?? "smtp.gmail.com";
            var port      = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var sender    = _config["Email:SenderEmail"]   ?? "";
            var password  = _config["Email:SenderPassword"] ?? "";
            var recipient = _config["Email:RecipientEmail"] ?? "";

            if (string.IsNullOrWhiteSpace(recipient)) return;

            var subject = committed
                ? $"✅ Z-Report Committed Successfully — {comparison.Date:dd/MM/yyyy}"
                : $"⚠️ Z-Report Variance Exceeds £5 — {comparison.Date:dd/MM/yyyy}";

            var body = BuildHtml(comparison, committed);

            using var message = new MailMessage(sender, recipient, subject, body)
            {
                IsBodyHtml = true
            };

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(sender, password),
                EnableSsl    = true,
            };

            await client.SendMailAsync(message);
        }

        private static string BuildHtml(ZReportComparisonResponse c, bool committed)
        {
            var status = committed
                ? "<p style='color:green;font-size:16px;font-weight:bold;'>✅ Committed Successfully</p>"
                : $"<p style='color:red;font-size:16px;font-weight:bold;'>⚠️ Cannot Commit — Total Difference £{Math.Abs(c.TotalDifference):F2} exceeds £5.00 limit</p>";

            var rows = new StringBuilder();
            foreach (var f in c.Fields)
            {
                var diffColour = f.Difference == 0 ? "black" : (Math.Abs(f.Difference) > 0 ? "orange" : "black");
                rows.Append($"""
                    <tr>
                        <td style='padding:6px 12px;border:1px solid #ddd;'>{f.Section}</td>
                        <td style='padding:6px 12px;border:1px solid #ddd;'>{f.Field}</td>
                        <td style='padding:6px 12px;border:1px solid #ddd;text-align:right;'>£{f.UserValue:F2}</td>
                        <td style='padding:6px 12px;border:1px solid #ddd;text-align:right;'>£{f.ZReportValue:F2}</td>
                        <td style='padding:6px 12px;border:1px solid #ddd;text-align:right;color:{diffColour};'>£{f.Difference:F2}</td>
                    </tr>
                """);
            }

            var totalRowColour = Math.Abs(c.TotalDifference) > 5 ? "red" : "green";

            return $"""
                <html><body style='font-family:Arial,sans-serif;'>
                <h2>Z-Report Reconciliation — {c.Date:dd MMM yyyy}</h2>
                {status}
                <table style='border-collapse:collapse;width:100%;margin-top:16px;'>
                    <thead>
                        <tr style='background:#f2f2f2;'>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:left;'>Section</th>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:left;'>Field</th>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>Your Value</th>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>Z-Report</th>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>Difference</th>
                        </tr>
                    </thead>
                    <tbody>{rows}</tbody>
                    <tfoot>
                        <tr style='font-weight:bold;background:#f9f9f9;'>
                            <td colspan='2' style='padding:8px 12px;border:1px solid #ddd;'>TOTAL (Tender)</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>£{c.UserTotal:F2}</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>£{c.ZReportGrandTotal:F2}</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;color:{totalRowColour};'>£{c.TotalDifference:F2}</td>
                        </tr>
                    </tfoot>
                </table>
                </body></html>
                """;
        }
    }
}
