using Microsoft.AspNetCore.DataProtection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Vector.Files.Chunking;
using Vector.Search.Health;
using Vector.Search.Services;
using Vector.Search.Settings;

namespace Vector.Search.Configuration;

public static class ServiceExtensions
{
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services
           .AddOptions<OllamaSettings>()
           .BindConfiguration(OllamaSettings.SectionName)
           .ValidateDataAnnotations()
           .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddHttp(
        this IServiceCollection services,
        IConfiguration config)
    {
        var ollamaSettings = config
            .GetSection(OllamaSettings.SectionName)
            .Get<OllamaSettings>()!;

        services
            .AddHttpClient<IOllamaClientFactory, OllamaClientFactory>("ollama", (httpClient) =>
            {
                httpClient.BaseAddress = new Uri(ollamaSettings.Url);
                httpClient.Timeout = TimeSpan.FromMinutes(ollamaSettings.TimeoutFromMinutes);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });

        return services;
    }

    public static IServiceCollection ConfigureAntiForgery(this IServiceCollection services)
    {
        var isDevelopment = services
            .BuildServiceProvider()
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "X-XSRF-TOKEN";
            options.Cookie.HttpOnly = !isDevelopment;
            options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DatabaseContext")!;

        services
            .AddScoped<IChunk, CodeChunking>()
            .AddPostgresVectorStore(connectionString);

        services.AddScoped<IOllamaClientFactory, OllamaClientFactory>();
        services.AddScoped(sp =>
        {
            var factory = sp.GetRequiredService<IOllamaClientFactory>();
            return factory.CreateClient();
        });

        services.AddScoped<OllamaClient>();
        services.AddScoped<CodeVectorStore>();
        services.AddHostedService<OllamaModelInitializer>()
                .AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo("/app/DataProtectionKeys/"))
                .SetApplicationName("Vector.Search");

        services.AddCors(options =>
        {
            options.AddPolicy("AllowReactApp", policy =>
            {
                var corsSettings = config.GetSection("CorsSettings");
                var allowedOrigins = corsSettings.GetValue<string>("AllowedOrigins")?.Split(',') ?? [];
                policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddTelemetry(
    this IServiceCollection services,
    IConfiguration config)
    {
        var seqSettings = config.GetSection("SeqSettings")
            .Get<SeqSettings>()!;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("Vector.Search")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.EnrichWithIDbCommand = (activity, command) =>
                    {
                        activity.SetTag("db.statement", command.CommandText);
                    };
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.FilterHttpRequestMessage = (httpRequestMessage) =>
                    {
                        // Ensure Ollama requests are captured
                        return true;
                    };
                })
                .AddSource("OllamaSharp")
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }))
            .WithLogging(logging => logging
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(seqSettings.ServerUrl);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"X-Seq-ApiKey={seqSettings.ApiKey}";
                }));

        return services;
    }

    public static WebApplicationBuilder AddConfiguration(this WebApplicationBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        builder.Configuration.AddConfiguration(configuration);

        return builder;
    }

    public static IServiceCollection AddHealthCheck(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<AppHealthCheck>("app_health_check");

        return services;
    }
}
