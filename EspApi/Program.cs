var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

/*cicdtest*/

app.MapPost("/api/sensor", (SensorData data) =>
{
    Console.WriteLine($"Otrzymano dane: {data.Temperature}");

    return Results.Ok(new { status = "received", timestamp = DateTime.UtcNow });

});
app.MapGet("/health", () => Results.Ok(new { status = "OK" }));

app.Run();

record SensorData(float Temperature, string? DeviceId= null);
