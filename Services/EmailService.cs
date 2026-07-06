using System.Text;
using Sales.DTOs;

namespace Sales.Services
{
    public interface IEmailService
    {
        Task SendComparisonEmailAsync(ZReportComparisonResponse comparison, bool committed);
        Task SendPasswordResetEmailAsync(string toEmail, string name, string tempPassword);
        Task SendReconciliationSubmittedEmailAsync(AdminSubmitReconciliationRequest data, DateOnly date);
        Task SendStaffLockedOutToAdminsAsync(string staffName, string staffEmail, IEnumerable<(string Email, string Name)> adminRecipients);
        Task SendAdminLockedOutSelfAsync(string adminEmail, string adminName, string tempPassword);
    }

    // Sends email via the Resend HTTP API (https://resend.com) instead of raw SMTP.
    // Render's network hangs/blocks outbound SMTP (confirmed: the same Gmail app-password
    // credentials worked instantly from a local machine but hung for 130+ seconds on Render),
    // so email now goes out over HTTPS instead, which isn't blocked.
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public EmailService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        private async Task SendAsync(string to, string subject, string html)
        {
            var apiKey = _config["Resend:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(to))
                return;

            // Resend requires the "from" address to be on a domain you've verified with them,
            // or their shared sandbox sender — it can never be an arbitrary Gmail address.
            var from = _config["Resend:FromAddress"] ?? "onboarding@resend.com";

            var payload = new
            {
                from,
                to = new[] { to },
                subject,
                html,
            };

            using var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await client.PostAsJsonAsync("emails", payload, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                throw new Exception($"Resend send failed ({response.StatusCode}): {body}");
            }
        }

        public async Task SendComparisonEmailAsync(ZReportComparisonResponse comparison, bool committed)
        {
            var recipient = _config["Email:RecipientEmail"] ?? "";
            if (string.IsNullOrWhiteSpace(recipient)) return;

            var subject = committed
                ? $"✅ Z-Report Committed Successfully — {comparison.Date:dd/MM/yyyy}"
                : $"⚠️ Z-Report Variance Exceeds £5 — {comparison.Date:dd/MM/yyyy}";

            var body = BuildHtml(comparison, committed);

            await SendAsync(recipient, subject, body);
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string name, string tempPassword)
        {
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

            await SendAsync(toEmail, subject, body);
        }

        public async Task SendReconciliationSubmittedEmailAsync(AdminSubmitReconciliationRequest data, DateOnly date)
        {
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

            await SendAsync(recipient, subject, body);
        }

        public async Task SendStaffLockedOutToAdminsAsync(
            string staffName,
            string staffEmail,
            IEnumerable<(string Email, string Name)> adminRecipients)
        {
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

            foreach (var (email, _) in adminRecipients)
            {
                if (string.IsNullOrWhiteSpace(email)) continue;
                await SendAsync(email, subject, body);
            }
        }

        public async Task SendAdminLockedOutSelfAsync(string adminEmail, string adminName, string tempPassword)
        {
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

            await SendAsync(adminEmail, subject, body);
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
