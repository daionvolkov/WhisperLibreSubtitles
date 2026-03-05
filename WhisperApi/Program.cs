using Serilog;
using WhisperApi.Services;


var builder = WebApplication.CreateBuilder(args);

// logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Host.UseSerilog((ctx, services, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
);


// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddHttpClient<TranslationApiClient>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["TranslationApi:BaseUrl"] ?? "http://translation-api:3830";
    http.BaseAddress = new Uri(baseUrl);
    http.Timeout = TimeSpan.FromSeconds(60);
});

// config-driven URL/port
var url = builder.Configuration["Kestrel:Endpoints:Http:Url"] ?? "http://0.0.0.0:3823";
builder.WebHost.UseUrls(url);

builder.Services.AddControllers();
builder.Services.AddSingleton<SubtitleTranscriptionService>();
builder.Services.AddSingleton<AudioDownloadService>();

var app = builder.Build();

app.UseCors("AllowAll");
app.MapControllers();

app.Run();