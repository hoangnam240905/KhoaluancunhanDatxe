using PortalWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // The dispatcher pages live in Pages/Mockup (historical folder name) but are served at /DieuPhoi.
    options.Conventions.AddFolderRouteModelConvention("/Mockup", model =>
    {
        foreach (var selector in model.Selectors)
        {
            var template = selector.AttributeRouteModel?.Template;
            if (template is not null && template.StartsWith("Mockup", StringComparison.OrdinalIgnoreCase))
                selector.AttributeRouteModel!.Template = "DieuPhoi" + template["Mockup".Length..];
        }
    });
});
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
// Vehicle photos uploaded by admins at runtime are not in the build-time static asset manifest.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "images", "vehicles", "uploads")).FullName),
    RequestPath = "/images/vehicles/uploads"
});
// Old bookmarks / links to /Mockup/... keep working: send them to the same page under /DieuPhoi.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Mockup", StringComparison.OrdinalIgnoreCase, out var rest)
        && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
    {
        context.Response.Redirect("/DieuPhoi" + rest + context.Request.QueryString);
        return;
    }
    await next();
});
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
