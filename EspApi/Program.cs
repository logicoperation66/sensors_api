using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["ApiKey"] ?? "twoj-bezpieczny-klucz-zmien-mnie";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Limit = 100,
            Period = "1h"
        }
    };
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
// Dodaj singleton do przechowywania odczytów
builder.Services.AddSingleton<SensorDataStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Middleware weryfikujący API key
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/health") || 
        context.Request.Path.StartsWithSegments("/api/sensor") && context.Request.Method == "GET")
    {
        await next();
        return;
    }

    if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("API Key brakuje");
        return;
    }

    if (!apiKey.Equals(extractedApiKey))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Nieprawidłowy API Key");
        return;
    }

    await next();
});

app.MapPost("/api/sensor", (SensorData data, SensorDataStore store) =>
{
    var reading = new SensorReading
    {
        Temperature = data.Temperature,
        DeviceId = data.DeviceId ?? "unknown",
        Timestamp = DateTime.UtcNow
    };
    
    store.AddReading(reading);
    
    Console.WriteLine($"Otrzymano dane: {reading.Temperature}°C od {reading.DeviceId}");
    
    return Results.Ok(new { status = "received", timestamp = reading.Timestamp });
});

// Endpoint do pobrania ostatnich odczytów (bez zabezpieczenia - read-only)
app.MapGet("/api/sensor", (SensorDataStore store, int? limit) =>
{
    var readings = store.GetLatestReadings(limit ?? 20);
    return Results.Ok(readings);
});

// Endpoint do statystyk
app.MapGet("/api/sensor/stats", (SensorDataStore store) =>
{
    var readings = store.GetLatestReadings(20);
    if (!readings.Any())
        return Results.Ok(new { message = "Brak danych" });
    
    var stats = new
    {
        count = readings.Count,
        avgTemperature = readings.Average(r => r.Temperature),
        minTemperature = readings.Min(r => r.Temperature),
        maxTemperature = readings.Max(r => r.Temperature),
        latestReading = readings.First(),
        oldestReading = readings.Last()
    };
    
    return Results.Ok(stats);
});

app.MapGet("/health", () => Results.Ok(new { status = "OK" }));

app.Run();

record SensorData(float Temperature, string? DeviceId = null);

record SensorReading
{
    public float Temperature { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

class SensorDataStore
{
    private readonly Queue<SensorReading> _readings = new();
    private readonly object _lock = new();
    private const int MaxReadings = 20;

    public void AddReading(SensorReading reading)
    {
        lock (_lock)
        {
            _readings.Enqueue(reading);
            
            // Usuń najstarszy odczyt jeśli przekroczono limit
            while (_readings.Count > MaxReadings)
            {
                _readings.Dequeue();
            }
        }
    }

    public List<SensorReading> GetLatestReadings(int count)
    {
        lock (_lock)
        {
            return _readings
                .OrderByDescending(r => r.Timestamp)
                .Take(Math.Min(count, _readings.Count))
                .ToList();
        }
    }
}