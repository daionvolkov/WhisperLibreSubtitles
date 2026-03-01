using TranslationApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAny", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var libreBaseUrl = builder.Configuration["LibreTranslate:BaseUrl"]
                   ?? "http://libretranslate:5000";

builder.Services.AddHttpClient("libre", http =>
{
    http.BaseAddress = new Uri(libreBaseUrl);
    http.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddScoped<TranslationService>();

var app = builder.Build();

app.UseCors("AllowAny");
app.MapControllers();

app.Run();