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
builder.Services.AddScoped<DriverEligibilityService>();
builder.Services.AddScoped<DriverService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<VehicleInspectionService>();
builder.Services.AddScoped<BookingFeeService>();
builder.Services.AddScoped<AdminCustomerService>();
builder.Services.AddScoped<CustomerProfileService>();
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

// Step 2: Backend → standalone V7 Python RecommendationService (port 8000). Integration client only.
builder.Services.Configure<RecommendationServiceOptions>(
    builder.Configuration.GetSection(RecommendationServiceOptions.SectionName));
builder.Services.AddSingleton<Backend.Services.Recommendation.V7CarIdVehicleMapStore>();
builder.Services.AddHttpClient<Backend.Services.Recommendation.IRecommendationService,
    Backend.Services.Recommendation.RecommendationService>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RecommendationServiceOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl) ? "http://127.0.0.1:8000" : opts.BaseUrl.TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds <= 0 ? 30 : opts.TimeoutSeconds);
});
// Step 3: Customer/Vehicle SQLite → V7 profile → Python → map car_id → Vehicle (no frontend API).
builder.Services.AddScoped<Backend.Services.Recommendation.IRecommendationMappingService,
    Backend.Services.Recommendation.RecommendationMappingService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<VehicleStatusHistoryService>();
builder.Services.AddScoped<RecommendationLogService>();
builder.Services.AddScoped<VehicleImageService>();
builder.Services.AddScoped<CustomerVehicleInteractionService>();
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
    db.EnsureSqliteBookingRequestedDriverColumn();
    db.EnsureSqliteMaintenanceRecordsTable();
    db.EnsureSqliteBookingRecommendationColumn();
    db.EnsureSqliteBookingCancellationReasonColumn();
    db.EnsureSqliteContractsTable();
    db.EnsureSqliteInspectionConditionColumns();
    db.EnsureSqliteIncidentReportsTable();
    db.EnsureSqliteVehicleLegalColumns();
    db.EnsureSqliteLicensePlateUniqueIndex();
    db.EnsureSqlitePhase1FinalSchema();
    DbSeeder.Seed(db);
    if (ShouldSeedDemoRich(app.Configuration, connectionString))
        DemoRichSeeder.Seed(db);
    db.FillVehicleTypePricingDefaults();
    db.ReconcileOpenAssignmentResourceStatus();

    // Step 3.5: idempotent V7 vehicle subset sync + car_id map (no schema change).
    // Only mutate/write the shared Data/v7-carid-vehicle-map.json when the active SQLite
    // file is ContentRoot/carrental.db — avoids desync from bin/Debug copies of the DB.
    var dataDir = Path.Combine(app.Environment.ContentRootPath, "Data");
    var v7MapStore = scope.ServiceProvider.GetRequiredService<Backend.Services.Recommendation.V7CarIdVehicleMapStore>();
    var primaryDb = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "carrental.db"));
    var activeDb = TryResolveSqlitePath(connectionString);
    var syncOnly = args.Any(a => string.Equals(a, "--sync-v7-vehicles", StringComparison.OrdinalIgnoreCase));
    var allowSync = syncOnly
        || (activeDb is not null
            && string.Equals(activeDb, primaryDb, StringComparison.OrdinalIgnoreCase));
    try
    {
        if (allowSync && File.Exists(Path.Combine(dataDir, Backend.Data.V7VehicleSeeder.CatalogFileName)))
        {
            var sync = Backend.Data.V7VehicleSeeder.Sync(db, dataDir, app.Logger);
            v7MapStore.Replace(sync.CarIdToVehicleId);
            foreach (var w in sync.Warnings)
                app.Logger.LogWarning("V7 sync warning: {Warning}", w);
        }
        else
        {
            if (!allowSync && activeDb is not null)
                app.Logger.LogInformation(
                    "Skipping V7 vehicle sync for non-primary SQLite {ActiveDb} (primary={PrimaryDb}).",
                    activeDb, primaryDb);
            v7MapStore.Replace(Backend.Data.V7VehicleSeeder.LoadMap(dataDir));
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "V7 vehicle sync failed; continuing without refreshing map.");
        v7MapStore.Replace(Backend.Data.V7VehicleSeeder.LoadMap(dataDir));
    }
}

if (args.Any(a => string.Equals(a, "--sync-v7-vehicles", StringComparison.OrdinalIgnoreCase)))
{
    app.Logger.LogInformation("V7 vehicle sync completed (--sync-v7-vehicles). Exiting without starting web host.");
    return;
}

static string? TryResolveSqlitePath(string connectionString)
{
    const string key = "Data Source=";
    var idx = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return null;
    var rest = connectionString[(idx + key.Length)..];
    var end = rest.IndexOf(';');
    var path = (end >= 0 ? rest[..end] : rest).Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(path)) return null;
    return Path.IsPathRooted(path)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
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
