using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sales.Data;
using Sales.Middleware;
using Sales.Services;
using System.Text;
using YourApp.Services;
using static Sales.Services.LotteryInstanceService;
using static Sales.Services.LotteryService;
using static YourApp.Services.SafeDropService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Database
builder.Services.AddDbContext<SalesDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
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

// Middleware
app.UseErrorHandlingMiddleware();
app.UseMiddleware<JwtAuthenticationMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
