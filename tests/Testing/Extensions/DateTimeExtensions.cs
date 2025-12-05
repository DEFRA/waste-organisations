namespace Testing.Extensions;

public static class DateTimeExtensions
{
    public static DateTime TruncateToMilliseconds(this DateTime dateTime)
    {
        return new DateTime(dateTime.Ticks / 10000 * 10000, dateTime.Kind);
    }
}
