using WhisperApi.Services;


var builder = WebApplication.CreateBuilder(args);

// logging
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

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