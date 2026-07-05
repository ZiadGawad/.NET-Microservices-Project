using Microsoft.EntityFrameworkCore;
using PlatformService.AsyncDataServices;
using PlatformService.Data;
using PlatformService.SyncDataServices.Grpc;
using PlatformService.SyncDataServices.Http;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    Console.WriteLine("--> Running in Production (Kubernetes) - Using SQL Server...");

    var connectionString = builder.Configuration.GetConnectionString("PlatformsConn");
    if (!string.IsNullOrEmpty(connectionString))
    {
        if(!connectionString.EndsWith(";")) connectionString += ";";
        connectionString += "TrustServerCertificate=True;";
    }
    builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connectionString));
}
else
{
    Console.WriteLine("--> Running in Development - Using In-Memory Database...");
    builder.Services.AddDbContext<AppDbContext>(opt=>
    opt.UseInMemoryDatabase("InMem"));
}

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); 
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("InMem"));
builder.Services.AddScoped<IPlatformRepo, PlatformRepo>();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddControllers();
builder.Services.AddHttpClient<ICommandDataClient, HttpCommandDataClient>();
builder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
builder.Services.AddGrpc();


Console.WriteLine($"--> Command Service Endpoint: {builder.Configuration["CommandService"]}");

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI();

app.MapGrpcService<GrpcPlatformService>();
app.MapControllers();
app.MapGet("/protos/platforms.proto",async context =>
{
    await context.Response.WriteAsync(File.ReadAllText("Protos/platforms.proto"));
});

// Comment this line out using double slashes so it doesn't force HTTPS on local Ubuntu!
//app.UseHttpsRedirection();

PrepDb.PrepPopulation(app, app.Environment.IsProduction());

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
