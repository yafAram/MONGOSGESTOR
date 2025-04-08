using Microsoft.Extensions.Options;
using MongoDB.Driver;
using WebApplication1.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración básica
builder.Services.AddControllersWithViews();

// 2. Configuración MongoDB segura
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDB"));

builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

// 3. Registro de servicios con logging
builder.Services.AddScoped<MongoDBService>();
builder.Services.AddScoped<SecurityService>();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// 4. Configuración del pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Database/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Database}/{action=Index}/{id?}");

app.Run();

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