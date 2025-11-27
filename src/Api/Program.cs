using Api.Dtos;
using Api.Utils;
using Api.Utils.Health;
using Api.Utils.Logging;
using Elastic.CommonSchema.Serilog;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console(new EcsTextFormatter()).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var integrationTest = args.Contains("--integrationTest=true");

    builder.Configuration.AddEnvironmentVariables();

    builder.Services.AddCustomTrustStore(); // This must happen before Mongo and Http client connections
    builder.ConfigureLoggingAndTracing(integrationTest);
    builder.Services.Configure<RouteHandlerOptions>(o =>
    {
        // Without this, bad request detail will only be thrown in DEVELOPMENT mode
        o.ThrowOnBadRequest = true;
    });
    builder.Services.AddProblemDetails();
    builder.Services.AddHealth();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseHeaderPropagation();
    app.MapHealth();
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet(
            "/organisations/{id:guid}",
            (Guid id) =>
                id == new Guid("b6f76437-65b6-4ed2-a7d5-c50e9af76201")
                    ? Results.Ok(new Organisation(id))
                    : Results.NotFound()
        )
        .WithName("GetOrganisation");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    await Log.CloseAndFlushAsync();
}
