using CustomerWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<CarRentalApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});
builder.Services.AddScoped<AuthSession>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapGet("/catalog/busy-periods/{vehicleId:int}", async (
    int vehicleId,
    DateTime? from,
    DateTime? to,
    CarRentalApiClient api) =>
{
    var (data, status, error) = await api.GetVehicleBusyPeriodsAsync(vehicleId, from, to);
    return data is null
        ? Results.Json(new { message = error }, statusCode: status)
        : Results.Json(data);
});
app.MapRazorPages().WithStaticAssets();

app.Run();
