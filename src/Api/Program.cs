using System.Text.Json;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Endpoints;
using Defra.WasteOrganisations.Api.Endpoints.Organisations;
using Defra.WasteOrganisations.Api.Endpoints.Organisations.Registrations;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Api.Utils;
using Defra.WasteOrganisations.Api.Utils.Health;
using Defra.WasteOrganisations.Api.Utils.Logging;
using Defra.WasteOrganisations.Api.Utils.Metrics;
using Elastic.CommonSchema.Serilog;
using Microsoft.AspNetCore.Diagnostics;
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
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<OpenApiDocumentTransformer>();
        options.AddOperationTransformer<RegistrationKeyOperationTransformer>();
        options.AddOperationTransformer<SearchQueryOperationTransformer>();
    });
    builder.Services.AddAuthenticationAuthorization();
    builder.Services.AddRequestMetrics();
    builder.Services.AddDbContext(builder.Configuration, integrationTest);
    builder.Services.AddValidation();
    builder.Services.AddTransient<IOrganisationService, OrganisationService>();

    var app = builder.Build();

    app.UseHeaderPropagation();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealth();
    app.UseExceptionHandler(
        new ExceptionHandlerOptions
        {
            AllowStatusCode404Response = true,
            ExceptionHandler = async context =>
            {
                var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
                var error = exceptionHandlerFeature?.Error;
                string? detail = null;

                if (error is BadHttpRequestException badHttpRequestException)
                {
                    context.Response.StatusCode = badHttpRequestException.StatusCode;
                    detail = RegistrationKeyErrorHandler.ReplaceSchemaReference(badHttpRequestException.Message);

                    if (error.InnerException is JsonException jsonException)
                    {
                        var ns = $"{typeof(Organisation).Namespace}.";
                        detail += $" - {jsonException.Message.Replace(ns, "")} {jsonException.Path}";
                    }
                }

                await context
                    .RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(
                        new ProblemDetailsContext
                        {
                            HttpContext = context,
                            AdditionalMetadata = exceptionHandlerFeature?.Endpoint?.Metadata,
                            ProblemDetails = { Status = context.Response.StatusCode, Detail = detail },
                        }
                    );
            },
        }
    );
    app.UseRequestMetrics();
    app.MapOpenApi();
    app.UseReDoc(options =>
    {
        options.RoutePrefix = "redoc";
        options.SpecUrl = "/openapi/v1.json";
    });

    app.MapApiEndpoints();

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
