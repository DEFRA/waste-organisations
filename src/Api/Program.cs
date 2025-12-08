using System.Text.Json;
using Api.Authentication;
using Api.Data;
using Api.Endpoints;
using Api.Endpoints.Organisations;
using Api.Endpoints.Organisations.Registrations;
using Api.Services;
using Api.Utils;
using Api.Utils.Health;
using Api.Utils.Logging;
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
                    detail = badHttpRequestException.Message;

                    if (error.InnerException is JsonException jsonException)
                    {
                        detail += $" - {jsonException.Message.Replace("Api.Dtos.", "")} {jsonException.Path}";
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
