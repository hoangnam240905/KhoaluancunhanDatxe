using System.Text;
using Backend.Data;
using Backend.Hubs;
using Backend.Options;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<CarRentalDbContext>(options =>
{
    if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(connectionString);
    else
        options.UseSqlite(connectionString);
});

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<EmailOtpService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));
builder.Services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
var emailEnabled = builder.Configuration.GetValue("Email:Enabled", false);
var smtpPassword = builder.Configuration["Email:SmtpPassword"];
var useSmtp = emailEnabled && !string.IsNullOrWhiteSpace(smtpPassword);
if (useSmtp)
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<VehicleOperationalProfileService>();
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ScheduleConflictService>();
builder.Services.AddScoped<DispatchService>();
builder.Services.AddScoped<DriverService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<VehicleInspectionService>();
builder.Services.AddScoped<BookingFeeService>();
builder.Services.AddScoped<AdminCustomerService>();
builder.Services.AddScoped<VehicleMaintenanceService>();
builder.Services.AddScoped<MaintenanceAlertService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.Configure<RecommenderOptions>(builder.Configuration.GetSection(RecommenderOptions.SectionName));
builder.Services.AddHttpClient<IRecommenderClient, FastApiRecommenderClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecommenderOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl) ? "http://127.0.0.1:8001" : opts.BaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds <= 0 ? 8 : opts.TimeoutSeconds);
});
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();

var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();
if (useSmtp)
    app.Logger.LogInformation("Email delivery: SMTP.");
else if (emailEnabled)
    app.Logger.LogWarning("Email:Enabled is true but Email:SmtpPassword is missing. Using NullEmailSender.");
else
    app.Logger.LogInformation("Email delivery: disabled (NullEmailSender).");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
    db.Database.EnsureCreated();
    db.EnsureSqliteBookingRentalColumns();
    db.EnsureSqliteBookingPriceSnapshotColumns();
    db.EnsureSqliteVehicleTypePricingColumns();
    db.EnsureSqliteBookingModeSnapshotColumns();
    db.EnsureSqlitePaymentTypeColumn();
    db.EnsureSqliteVehicleInspectionsTable();
    db.EnsureSqliteBookingFinalAmountColumn();
    db.EnsureSqliteBookingFeesTable();
    db.EnsureSqliteUsersLockColumn();
    db.EnsureSqliteUsersAccountStatusColumns();
    db.EnsureSqliteUsersAuthColumns();
    db.EnsureSqliteEmailOtpsTable();
    db.EnsureSqliteDriversActiveColumn();
    db.EnsureSqliteMaintenanceRecordsTable();
    db.EnsureSqliteBookingRecommendationColumn();
    db.EnsureSqliteContractsTable();
    db.EnsureSqliteInspectionConditionColumns();
    db.EnsureSqliteIncidentReportsTable();
    db.EnsureSqliteVehicleLegalColumns();
    db.EnsureSqliteLicensePlateUniqueIndex();
    DbSeeder.Seed(db);
    if (ShouldSeedDemoRich(app.Configuration, connectionString))
        DemoRichSeeder.Seed(db);
    db.FillVehicleTypePricingDefaults();
    db.ReconcileOpenAssignmentResourceStatus();
}

static bool ShouldSeedDemoRich(IConfiguration config, string cs)
{
    if (!config.GetValue("SeedDemoRich", false))
        return false;
    // Never enrich live carrental.db — DemoRich only runs on carrental.demo.db.
    return cs.Contains("carrental.demo.db", StringComparison.OrdinalIgnoreCase);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<RealtimeHub>("/hubs/realtime");

app.Run();

public partial class Program { }
