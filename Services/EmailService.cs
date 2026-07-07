using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Sales.DTOs;

namespace Sales.Services
{
    // Diagnostic-only: reports exactly what config the app resolved, alongside the
    // send outcome, so a bad/missing env var is visible without digging through logs.
    // TcpConnected distinguishes a network/firewall-level block (TCP itself never
    // connects) from a failure later in the TLS/AUTH handshake.
    public record SmtpTestResult(
        bool Success, string Host, int Port, string Sender,
        bool PasswordConfigured, string Recipient, string? Error,
        bool TcpConnected, long TcpElapsedMs, string? TcpError);

    public interface IEmailService
    {
        // Diagnostic-only: sends a minimal test email so SMTP connectivity can be verified
        // without touching any real data or user password.
        Task<SmtpTestResult> SendTestEmailAsync();
        Task SendComparisonEmailAsync(ZReportComparisonResponse comparison, bool committed);
        Task SendPasswordResetEmailAsync(string toEmail, string name, string tempPassword);
        Task SendReconciliationSubmittedEmailAsync(AdminSubmitReconciliationRequest data, DateOnly date);
        Task SendStaffLockedOutToAdminsAsync(string staffName, string staffEmail, IEnumerable<(string Email, string Name)> adminRecipients);
        Task SendAdminLockedOutSelfAsync(string adminEmail, string adminName, string tempPassword);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<SmtpTestResult> SendTestEmailAsync()
        {
            var host      = _config["Email:SmtpHost"]       ?? "smtp.gmail.com";
            var port      = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var sender    = _config["Email:SenderEmail"]    ?? "";
            var password  = _config["Email:SenderPassword"] ?? "";
            var recipient = _config["Email:RecipientEmail"] ?? "";
            var passwordConfigured = !string.IsNullOrWhiteSpace(password);

            // Raw TCP connect first, separate from the full SMTP handshake — tells us
            // whether Render's network reaches Gmail at the transport layer at all,
            // before TLS/AUTH ever come into play.
            var tcpSw = System.Diagnostics.Stopwatch.StartNew();
            bool tcpConnected;
            string? tcpError = null;
            try
            {
                using var tcp = new TcpClient();
                using var tcpCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await tcp.ConnectAsync(host, port, tcpCts.Token);
                tcpConnected = tcp.Connected;
            }
            catch (Exception ex)
            {
                tcpConnected = false;
                tcpError = ex.Message;
            }
            var tcpElapsedMs = tcpSw.ElapsedMilliseconds;

            if (string.IsNullOrWhiteSpace(recipient))
                return new SmtpTestResult(false, host, port, sender, passwordConfigured, recipient,
                    "Email:RecipientEmail is not configured.", tcpConnected, tcpElapsedMs, tcpError);

            try
            {
                using var message = new MailMessage(sender, recipient,
                    "SMTP Test Email — Sales App",
                    $"<html><body>This is a diagnostic test email sent at {DateTime.UtcNow:u} UTC to verify SMTP connectivity.</body></html>")
                { IsBodyHtml = true };

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(sender, password),
                    EnableSsl   = true,
                    Timeout     = 10000,
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await client.SendMailAsync(message, cts.Token);

                return new SmtpTestResult(true, host, port, sender, passwordConfigured, recipient, null,
                    tcpConnected, tcpElapsedMs, tcpError);
            }
            catch (Exception ex)
            {
                return new SmtpTestResult(false, host, port, sender, passwordConfigured, recipient, ex.Message,
                    tcpConnected, tcpElapsedMs, tcpError);
            }
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
                Timeout      = 10000,
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.SendMailAsync(message, cts.Token);
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
                Timeout     = 10000,
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.SendMailAsync(message, cts.Token);
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
                Timeout     = 10000,
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.SendMailAsync(message, cts.Token);
        }

        public async Task SendStaffLockedOutToAdminsAsync(
            string staffName,
            string staffEmail,
            IEnumerable<(string Email, string Name)> adminRecipients)
        {
            var host     = _config["Email:SmtpHost"]       ?? "smtp.gmail.com";
            var port     = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var sender   = _config["Email:SenderEmail"]    ?? "";
            var password = _config["Email:SenderPassword"] ?? "";

            var subject = $"⚠️ Login Alert — {staffName}'s Account Locked After 5 Failed Attempts";
            var body = $"""
                <html><body style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
                <div style='background:#fef2f2;border:1px solid #fca5a5;border-radius:8px;padding:20px 24px;'>
                  <h2 style='margin:0 0 8px;color:#dc2626;'>⚠️ Account Locked</h2>
                  <p style='margin:0;color:#374151;'>
                    Staff member <strong>{staffName}</strong> (<a href='mailto:{staffEmail}'>{staffEmail}</a>)
                    has made <strong>5 consecutive failed login attempts</strong>.
                  </p>
                </div>
                <p style='color:#374151;margin-top:16px;'>
                  Please log in to the <strong>Admin Panel</strong> and reset this user's password
                  so they can regain access.
                </p>
                <p style='color:#6b7280;font-size:0.85rem;'>
                  If you believe this was an unauthorised attempt, you may also wish to contact the user directly.
                </p>
                </body></html>
                """;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(sender, password),
                EnableSsl   = true,
                Timeout     = 10000,
            };

            foreach (var (email, _) in adminRecipients)
            {
                if (string.IsNullOrWhiteSpace(email)) continue;
                using var msg = new MailMessage(sender, email, subject, body) { IsBodyHtml = true };
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await client.SendMailAsync(msg, cts.Token);
            }
        }

        public async Task SendAdminLockedOutSelfAsync(string adminEmail, string adminName, string tempPassword)
        {
            var host     = _config["Email:SmtpHost"]       ?? "smtp.gmail.com";
            var port     = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var sender   = _config["Email:SenderEmail"]    ?? "";
            var password = _config["Email:SenderPassword"] ?? "";

            if (string.IsNullOrWhiteSpace(adminEmail)) return;

            var subject = "🔒 Your Admin Account — Temporary Password (5 Failed Logins)";
            var body = $"""
                <html><body style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
                <div style='background:#fef2f2;border:1px solid #fca5a5;border-radius:8px;padding:20px 24px;'>
                  <h2 style='margin:0 0 8px;color:#dc2626;'>🔒 Account Locked</h2>
                  <p style='margin:0;color:#374151;'>
                    Hi <strong>{adminName}</strong>, your admin account has been locked after
                    <strong>5 consecutive failed login attempts</strong>.
                  </p>
                </div>
                <p style='color:#374151;margin-top:16px;'>
                  A new temporary password has been generated for your account:
                </p>
                <p style='font-size:22px;font-weight:bold;letter-spacing:3px;padding:14px 22px;
                          background:#f3f4f6;display:inline-block;border-radius:8px;
                          border:1px solid #d1d5db;color:#111827;'>
                  {tempPassword}
                </p>
                <p style='color:#374151;'>
                  Please log in with this temporary password and change it immediately from your account settings.
                </p>
                <p style='color:#6b7280;font-size:0.85rem;'>
                  If you did not initiate these login attempts, please contact your system administrator immediately.
                </p>
                </body></html>
                """;

            using var message = new MailMessage(sender, adminEmail, subject, body) { IsBodyHtml = true };
            using var client  = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(sender, password),
                EnableSsl   = true,
                Timeout     = 10000,
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.SendMailAsync(message, cts.Token);
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
