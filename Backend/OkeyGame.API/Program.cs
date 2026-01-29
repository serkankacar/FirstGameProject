using OkeyGame.API.Hubs;
using OkeyGame.API.Services;
using OkeyGame.Application.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// SERVISLER
// ============================================

// Redis kullanımını kontrol et (development'ta opsiyonel)
var useRedis = builder.Configuration.GetValue<bool>("UseRedis", false);
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

if (useRedis)
{
    // Redis bağlantısı
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var configuration = ConfigurationOptions.Parse(redisConnectionString);
        configuration.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(configuration);
    });

    // Redis Distributed Cache
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "OkeyGame:";
    });

    // SignalR with Redis Backplane (Scale-out için)
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redisConnectionString, options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("OkeyGame");
        });
    
    // Redis Game State
    builder.Services.AddSingleton<IGameStateService, RedisGameStateService>();
}
else
{
    // In-Memory Cache (development için)
    builder.Services.AddDistributedMemoryCache();
    
    // SignalR without Redis
    builder.Services.AddSignalR();
    
    // In-Memory Game State
    builder.Services.AddSingleton<IGameStateService, InMemoryGameStateService>();
}

// Uygulama Servisleri
builder.Services.AddSingleton<ProvablyFairService>();
builder.Services.AddScoped<IGameService, GameService>();

// CORS (Unity için gerekli)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Tüm originlere izin ver
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // SignalR için gerekli
    });

    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "https://yourgame.com",
                "https://www.yourgame.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Controllers (opsiyonel REST API için)
builder.Services.AddControllers();

// Swagger (development için)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// ============================================
// MIDDLEWARE PIPELINE
// ============================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AllowAll");
}
else
{
    app.UseHttpsRedirection(); // Sadece production'da HTTPS redirect
    app.UseCors("Production");
}

// Development'ta HTTPS redirect yok - WebSocket için gerekli
app.UseRouting();

// WebSockets middleware - SignalR için gerekli
app.UseWebSockets();

// Authentication & Authorization (ileride eklenecek)
// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();

// SignalR Hub endpoint
app.MapHub<GameHub>("/gamehub");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0",
    RedisEnabled = useRedis
}));

// Redis connection check (sadece Redis aktifse)
if (useRedis)
{
    app.MapGet("/health/redis", async (IConnectionMultiplexer redis) =>
    {
        try
        {
            var db = redis.GetDatabase();
            await db.PingAsync();
            return Results.Ok(new { Status = "Connected", Timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Redis connection failed: {ex.Message}");
        }
    });
}

app.Run();
