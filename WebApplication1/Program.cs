using TCPMessageAPI.Astm;
using TCPMessageAPI.Hubs;
using TCPMessageAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR
builder.Services.AddSignalR();

builder.Services.AddSingleton<TcpService>();
builder.Services.AddSingleton<AstmService>();
builder.Services.AddSingleton<AstmHostService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapHub<AstmHub>("/astmHub");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();


app.Run();