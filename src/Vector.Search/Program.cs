using Serilog;
using Vector.Search;
using Vector.Search.Configuration;
using Vector.Search.Hubs;
using Vector.Search.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(builder =>
{
    builder.AddServerHeader = false;
});

builder.Host
    .UseDefaultServiceProvider((context, options) =>
    {
        options.ValidateOnBuild = true;
    })
    .UseSerilog((context, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration);
    });

builder
    .AddConfiguration();

var configuration = builder.Configuration;

builder.Services
    .ConfigureAntiForgery()
    .AddOpenApi()
    .AddTelemetry(configuration)
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddProblemDetails()
    .AddSettings()
    .AddHttp(configuration)
    .AddServices(configuration)
    .AddHealthCheck()
    .AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    })
    .AddNewtonsoftJsonProtocol();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseExceptionHandler();
app.UseAntiforgery();
app.MapHealthChecks("/health");
app.MapHub<EmbeddingHub>("/embeddings-hub");

EndPoints.Map(app);
app.Run();
