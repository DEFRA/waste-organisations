using AwesomeAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Tests;

public class StartupTests(TestWebApplicationFactory<Program> factory)
    : IClassFixture<TestWebApplicationFactory<Program>>
{
    [Fact]
    public void WhenFaultOnStartup_ShouldThrow()
    {
        var builder = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services => services.AddHostedService<FaultyStartupService>());
        });

        var act = () => builder.CreateClient();

        act.Should().Throw<ObjectDisposedException>();
    }

    public class FaultyStartupService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated startup crash");

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
