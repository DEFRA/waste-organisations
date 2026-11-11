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
using Defra.WasteOrganisations.Api.Utils.Security;
using Defra.WasteOrganisations.Api.Versioning;
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
    builder.Services.AddHsts(options =>
    {
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365);
    });
    builder.Services.AddProblemDetails();
    builder.Services.AddHealth();
    // The documents in the ReDoc version dropdown, the default first; a new API version adds its document here
    var openApiDocuments = new[]
    {
        new { name = "v1", url = "/openapi/v1.json" },
        new { name = "v2", url = "/openapi/v2.json" },
    };
    builder.Services.AddOpenApi(options =>
    {
        // The default document is v1: it describes only the operations that support version 1.
        options.ShouldInclude = description =>
            ApiVersionsMetadata
                .For(description.ActionDescriptor.EndpointMetadata)
                .Contains(AcceptHeaderVersionResolver.DefaultVersion);
        options.AddDocumentTransformer<OpenApiDocumentTransformer>();
        options.AddOperationTransformer<RegistrationKeyOperationTransformer>();
        options.AddOperationTransformer<SearchQueryOperationTransformer>();
    });
    builder.Services.AddOpenApi("v2", options =>
    {
        // Describes only the operations that support version 2, each with the Accept header that selects it
        options.ShouldInclude = description =>
            ApiVersionsMetadata.For(description.ActionDescriptor.EndpointMetadata).Contains(2);
        options.AddDocumentTransformer<OpenApiDocumentTransformer>();
        options.AddDocumentTransformer<RequiredMembersDocumentTransformer>();
        options.AddOperationTransformer<AcceptHeaderOperationTransformer>();
        options.AddSchemaTransformer<CreateOrganisationSchemaTransformer>();
    });
    builder.Services.AddAuthenticationAuthorization();
    builder.Services.AddApiVersioning();
    builder.Services.AddRequestMetrics();
    builder.Services.AddDbContext(builder.Configuration, integrationTest);
    builder.Services.AddPostgres(builder.Configuration);
    builder.Services.AddValidation();
    builder.Services.AddTransient<IOrganisationService, OrganisationService>();
    builder.Services.AddTransient<OrganisationRegistrationService>();
    builder.Services.AddScoped<IPostgresOrganisationService, PostgresOrganisationService>();

    var app = builder.Build();

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
    app.UseHstsUnconditionally();
    app.UseHeaderPropagation();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseApiVersioning();
    app.MapHealth();
    app.UseRequestMetrics();
    app.MapOpenApi();
    app.UseReDoc(options =>
    {
        options.RoutePrefix = "redoc";
        options.SpecUrl = openApiDocuments[0].url;
        // A custom page, so one ReDoc page serves every document through a version dropdown
        options.IndexStream = () =>
            typeof(Program).Assembly.GetManifestResourceStream("Documentation.ReDoc.index.html")
            ?? throw new InvalidOperationException("The embedded ReDoc page is missing");
        options.HeadContent =
            $"<script>window.openApiDocuments = {JsonSerializer.Serialize(openApiDocuments)};</script>";
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
