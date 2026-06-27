using System.Net;
using System.Net.Mail;
using System.Text;
using Sales.DTOs;

namespace Sales.Services
{
    public interface IEmailService
    {
        Task SendComparisonEmailAsync(ZReportComparisonResponse comparison, bool committed);
        Task SendPasswordResetEmailAsync(string toEmail, string name, string tempPassword);
        Task SendReconciliationSubmittedEmailAsync(AdminSubmitReconciliationRequest data, DateOnly date);
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

        public async Task SendPasswordResetEmailAsync(string toEmail, string name, string tempPassword)
        {
            var host      = _config["Email:SmtpHost"]       ?? "smtp.gmail.com";
            var port      = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var sender    = _config["Email:SenderEmail"]    ?? "";
            var password  = _config["Email:SenderPassword"] ?? "";

            if (string.IsNullOrWhiteSpace(toEmail)) return;

            var subject = "Your Temporary Password — Sales App";
            var body = $"""
                <html><body style='font-family:Arial,sans-serif;'>
                <h2>Password Reset</h2>
                <p>Hi {name},</p>
                <p>Your temporary password is:</p>
                <p style='font-size:20px;font-weight:bold;letter-spacing:2px;padding:12px 20px;background:#f2f2f2;display:inline-block;border-radius:6px;'>{tempPassword}</p>
                <p>Please log in and change your password as soon as possible.</p>
                </body></html>
                """;

            using var message = new MailMessage(sender, toEmail, subject, body) { IsBodyHtml = true };
            using var client  = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(sender, password),
                EnableSsl   = true,
            };
            await client.SendMailAsync(message);
        }

        public async Task SendReconciliationSubmittedEmailAsync(AdminSubmitReconciliationRequest data, DateOnly date)
        {
            var host      = _config["Email:SmtpHost"]       ?? "smtp.gmail.com";
            var port      = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var sender    = _config["Email:SenderEmail"]    ?? "";
            var password  = _config["Email:SenderPassword"] ?? "";
            var recipient = _config["Email:RecipientEmail"] ?? "";

            if (string.IsNullOrWhiteSpace(recipient)) return;

            var subject = $"✅ Reconciliation Submitted — {date:dd/MM/yyyy}";
            var cash = data.LastSafe + data.SafeDropAmount;

            var body = $"""
                <html><body style='font-family:Arial,sans-serif;'>
                <h2>Admin Reconciliation Submitted — {date:dd MMM yyyy}</h2>
                <p style='color:green;font-weight:bold;'>✅ Submitted successfully</p>
                <table style='border-collapse:collapse;width:100%;margin-top:16px;'>
                    <thead>
                        <tr style='background:#f2f2f2;'>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:left;'>Section</th>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:left;'>Field</th>
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>Amount (£)</th>
                        </tr>
                    </thead>
                    <tbody>
                        {Row("Credit Card", "Manual Card Amount",    data.ManualCardAmount)}
                        {Row("Credit Card", "Card Amount",            data.CardAmount)}
                        {Row("Cash",        "Last Safe",              data.LastSafe)}
                        {Row("Cash",        "Safe Drop Amount",       data.SafeDropAmount)}
                        {Row("Cash",        "Cash Total",             cash)}
                        {Row("Deductions",  "Cashback",               data.Cashback)}
                        {Row("Deductions",  "Paypoint Payout",        data.PaypointPayout)}
                        {Row("Deductions",  "Instant Lottery Payout", data.InstantLotteryPayout)}
                        {Row("Deductions",  "News Voucher",           data.NewsVoucher)}
                        {Row("Deductions",  "DD Point",               data.DDPoint)}
                        {Row("Deductions",  "Lottery Payout",         data.LotteryPayout)}
                        {Row("Lottery",     "Lottery Value",          data.LotteryValue)}
                        {Row("Paypoint",    "Paypoint Value",         data.PaypointValue)}
                    </tbody>
                    <tfoot>
                        <tr style='font-weight:bold;background:#f9f9f9;'>
                            <td colspan='2' style='padding:8px 12px;border:1px solid #ddd;'>Summary Total</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>£{data.SummaryTotal:F2}</td>
                        </tr>
                        <tr style='font-weight:bold;background:#f9f9f9;'>
                            <td colspan='2' style='padding:8px 12px;border:1px solid #ddd;'>Z-Report Total</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>£{data.ZReportTotal:F2}</td>
                        </tr>
                        <tr style='font-weight:bold;background:#f9f9f9;'>
                            <td colspan='2' style='padding:8px 12px;border:1px solid #ddd;'>Difference</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>£{data.Difference:F2}</td>
                        </tr>
                    </tfoot>
                </table>
                {(string.IsNullOrWhiteSpace(data.AdminNotes) ? "" : $"<p><strong>Admin Notes:</strong> {data.AdminNotes}</p>")}
                </body></html>
                """;

            using var message = new MailMessage(sender, recipient, subject, body) { IsBodyHtml = true };
            using var client  = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(sender, password),
                EnableSsl   = true,
            };
            await client.SendMailAsync(message);
        }

        private static string Row(string section, string field, decimal value) =>
            $"<tr><td style='padding:6px 12px;border:1px solid #ddd;'>{section}</td><td style='padding:6px 12px;border:1px solid #ddd;'>{field}</td><td style='padding:6px 12px;border:1px solid #ddd;text-align:right;'>£{value:F2}</td></tr>";

        private static string BuildHtml(ZReportComparisonResponse c, bool committed)
        {
            var status = committed
                ? "<p style='color:green;font-size:16px;font-weight:bold;'>✅ Committed Successfully</p>"
                : $"<p style='color:red;font-size:16px;font-weight:bold;'>⚠️ Cannot Commit — Total Difference £{Math.Abs(c.TotalDifference):F2} exceeds £5.00 limit</p>";

            var rows = new StringBuilder();
            foreach (var f in c.Fields)
            {
                rows.Append($"""
                    <tr>
                        <td style='padding:6px 12px;border:1px solid #ddd;'>{f.Section}</td>
                        <td style='padding:6px 12px;border:1px solid #ddd;'>{f.Field}</td>
                        <td style='padding:6px 12px;border:1px solid #ddd;text-align:right;'>£{f.UserValue:F2}</td>
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
                            <th style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>Value (£)</th>
                        </tr>
                    </thead>
                    <tbody>{rows}</tbody>
                    <tfoot>
                        <tr style='font-weight:bold;background:#e8f5e9;'>
                            <td colspan='2' style='padding:8px 12px;border:1px solid #ddd;'>User Total</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>£{c.UserTotal:F2}</td>
                        </tr>
                        <tr style='font-weight:bold;background:#f9f9f9;'>
                            <td colspan='2' style='padding:8px 12px;border:1px solid #ddd;'>Z-Report Department Total</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;'>£{c.ZReportGrandTotal:F2}</td>
                        </tr>
                        <tr style='font-weight:bold;'>
                            <td colspan='2' style='padding:8px 12px;border:1px solid #ddd;'>Difference</td>
                            <td style='padding:8px 12px;border:1px solid #ddd;text-align:right;color:{totalRowColour};'>£{c.TotalDifference:F2}</td>
                        </tr>
                    </tfoot>
                </table>
                </body></html>
                """;
        }
    }
}
