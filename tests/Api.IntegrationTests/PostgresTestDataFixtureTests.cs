using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.IntegrationTests;

public class PostgresTestDataFixtureTests : IntegrationTestBase
{
    private const string Host = "127.0.0.1";

    public static TheoryData<string[]> TagsWithoutBaseline => new([], ["v1.1.0"]);

    [Fact]
    public void EnsureComposeDatabase_WithTheComposeDatabaseAndBaselineTag_ShouldNotThrow()
    {
        var act = () =>
            PostgresTestDataFixture.EnsureComposeDatabase("waste_organisations", Host, ["v1.0.0", "v1.1.0"]);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureComposeDatabase_WithAnotherDatabaseName_ShouldThrow()
    {
        var act = () => PostgresTestDataFixture.EnsureComposeDatabase("my_own_database", Host, ["v1.0.0"]);

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain("my_own_database").And.Contain(Host).And.Contain("waste_organisations");
    }

    [Theory]
    [MemberData(nameof(TagsWithoutBaseline))]
    public void EnsureComposeDatabase_WithoutTheBaselineTag_ShouldThrow(string[] tags)
    {
        var act = () => PostgresTestDataFixture.EnsureComposeDatabase("waste_organisations", Host, tags);

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;
        message.Should().Contain("waste_organisations").And.Contain(Host).And.Contain("v1.0.0");
    }
}
