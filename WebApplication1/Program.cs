using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración básica
builder.Services.AddControllersWithViews();

// 2. Configuración Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"))
    .SetApplicationName("MongoSGestor");

// 3. Configuración MongoDB segura
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDB"));

builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// 4. Registro de servicios
builder.Services.AddScoped<MongoDBService>();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Validación de configuración al inicio
var mongoSettings = app.Services.GetRequiredService<IOptions<MongoDBSettings>>().Value;
try
{
    mongoSettings.Validate();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Configuración de MongoDB inválida");
    throw;
}

// 5. Configuración del pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandler = context.Features.Get<IExceptionHandlerPathFeature>();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exceptionHandler.Error, "Error no controlado");

            context.Response.Redirect("/Database/Error");
            await Task.CompletedTask;
        });
    });
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Database}/{action=Index}/{id?}");

app.Run();

// Clase de configuración (mantener igual)
public class MongoDBSettings
{
    public required string ConnectionString { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentNullException(nameof(ConnectionString));

        if (!ConnectionString.Contains("mongodb://") && !ConnectionString.Contains("mongodb+srv://"))
            throw new ArgumentException("Formato de cadena de conexión inválido");
    }
}