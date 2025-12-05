using AwesomeAssertions;
using AwesomeAssertions.Equivalency;

namespace Testing.Extensions;

public static class AwesomeAssertionsExtensions
{
    /// <summary>
    /// Mongo only saves milliseconds, the framework has microseconds, so we
    /// can ignore in any DateTime comparison.
    /// </summary>
    /// <param name="options"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static EquivalencyOptions<T> AllowMongoDateTimePrecision<T>(this EquivalencyOptions<T> options)
    {
        return options
            .Using<DateTime>(x =>
                x.Subject.TruncateToMilliseconds().Should().Be(x.Expectation.TruncateToMilliseconds())
            )
            .WhenTypeIs<DateTime>();
    }
}
