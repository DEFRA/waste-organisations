using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Defra.WasteOrganisations.Api.Tests;

// ReSharper disable once ClassNeverInstantiated.Global
public class TestWebApplicationFactory<T> : WebApplicationFactory<T>, ITestOutputHelperAccessor
    where T : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(config => config.AddXUnit(this));
        builder.UseSetting("integrationTest", "true");
        builder.UseEnvironment("IntegrationTests");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(_ => { });

        return base.CreateHost(builder);
    }

    public ITestOutputHelper? OutputHelper { get; set; }
}
