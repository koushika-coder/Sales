using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Sales.Data;
using Sales.Middleware;
using Sales.Services;
using System.Text;
using YourApp.Services;
using static Sales.Services.LotteryInstanceService;
using static Sales.Services.LotteryService;
using static YourApp.Services.SafeDropService;

var builder = WebApplication.CreateBuilder(args);

// Disable configuration file reload watchers to avoid Render/container file-descriptor limits.
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Add services to the container.

// Database
// Render (and Heroku-style platforms) hand out connection strings as a
// postgres:// URI, but Npgsql's connection string parser only understands
// the ADO.NET Key=Value;Key=Value format — convert if needed.
static string ToNpgsqlConnectionString(string raw)
{
    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        return raw;

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);

    return new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
        SslMode = SslMode.Prefer,
    }.ConnectionString;
}

var dbConnectionString = ToNpgsqlConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty);

builder.Services.AddDbContext<SalesDbContext>(options =>
    options.UseNpgsql(dbConnectionString)
);

// JWT Configuration
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? string.Empty;
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? string.Empty;
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? string.Empty;

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep original claim names from the token
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

// CORS
var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(",") ?? ["*"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policyBuilder =>
    {
        policyBuilder
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IShopSaleService, ShopSaleService>();
//builder.Services.AddScoped<ICashBankingService, CashBankingService>();
builder.Services.AddScoped<IPaypointService, PaypointService>();
// Program.cs
builder.Services.AddScoped<IDeductionsService, DeductionsService>();
builder.Services.AddScoped<ISuppliersService, SuppliersService>();//builder.Services.AddScoped<IInstantLotteryService, InstantLotteryService>();
builder.Services.AddScoped<ILotteryService, LotteryService>();
builder.Services .AddHttpContextAccessor();

builder.Services.AddScoped<ILotteryInstanceService, LotteryInstanceService>();
builder.Services.AddScoped<ISafeDropService, SafeDropService>();

//builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
//builder.Services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
// Program.cs
builder.Services.AddScoped<IDeductionsService, DeductionsService>();
builder.Services.AddScoped<ISuppliersService, SuppliersService>();
builder.Services.AddScoped<ICreditCardService, CreditCardBankingService>();
builder.Services.AddScoped<IGmailService,GmailServiceImpl>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAdminReconciliationService, AdminReconciliationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();


// HTTP Client Factory for Google OAuth
builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db          = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

    db.Database.Migrate();

    // Seed the first admin user if none exists.
    // Set SEED_ADMIN_EMAIL and SEED_ADMIN_PASSWORD as env vars in Render
    // (remove them after first successful login).
    var seedEmail    = builder.Configuration["SEED_ADMIN_EMAIL"];
    var seedPassword = builder.Configuration["SEED_ADMIN_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedPassword)
        && !db.Users.Any(u => u.Role == "admin"))
    {
        db.Users.Add(new Sales.Models.User
        {
            Email        = seedEmail,
            PasswordHash = authService.HashPassword(seedPassword),
            Name         = "Admin",
            Role         = "admin",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
        });
        db.SaveChanges();
    }
}

// Render (and most PaaS hosts) terminate TLS at the edge and forward plain HTTP —
// trust those headers so the app knows the original request was HTTPS.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Middleware
app.UseErrorHandlingMiddleware();
app.UseMiddleware<JwtAuthenticationMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok("OK"));

app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
