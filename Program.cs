using VIDEO_RECOLECTOR.Services;
using VIDEO_RECOLECTOR.Models;
using Serilog;
using Serilog.Events;
using Microsoft.OpenApi.Models;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog
    var logPath = Path.Combine(Directory.GetCurrentDirectory(), "AppLogs"); 
    Directory.CreateDirectory(logPath);

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logPath, "log-.txt"),
            rollingInterval: RollingInterval.Day,
            shared: true,  
            flushToDiskInterval: TimeSpan.FromSeconds(1), 
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.Configure<CameraSettings>(
        builder.Configuration.GetSection("CameraSettings"));
    builder.Services.AddSingleton<ICameraService, CameraService>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { 
            Title = "Video Recolector API", 
            Version = "v1",
            Description = "API para grabación de video"
        });
    });

    // Configurar CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
    });

    // Configurar Kestrel
    builder.WebHost.ConfigureKestrel((context, options) => {
        options.Configure(context.Configuration.GetSection("Kestrel"));
    });

    var app = builder.Build();

    // Swagger siempre disponible, incluso en producción
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Video Recolector API v1");
            c.RoutePrefix = "swagger"; // Esto hace que Swagger UI sea la página principal
        });
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles(); 
    app.UseRouting();
    app.UseCors("AllowAll");
    app.UseAuthorization();

    app.MapControllers();

    // Asegurar que el directorio de videos existe
    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    var wwwrootPath = Path.Combine(baseDirectory, "wwwroot");
    var videoPath = Path.Combine(wwwrootPath, "videos");
    
    if (!Directory.Exists(wwwrootPath))
    {
        Directory.CreateDirectory(wwwrootPath);
    }
    if (!Directory.Exists(videoPath))
    {
        Directory.CreateDirectory(videoPath);
    }

    Log.Information("Iniciando aplicación Video Recolector");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación terminó inesperadamente");
    Console.WriteLine($"Error fatal: {ex}");
    Console.WriteLine("\nPresione cualquier tecla para cerrar...");
    Console.ReadKey();
}
finally
{
    Log.CloseAndFlush();
}
