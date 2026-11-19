using System.Globalization;

namespace BreakManager.Web.Hosting;

/// <summary>
/// Muestra instantes UTC en la zona operativa (America/Argentina/Buenos_Aires, CLAUDE.md).
/// </summary>
internal static class OperationalTime
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static string Format(DateTimeOffset? instant) =>
        instant is null
            ? "—"
            : TimeZoneInfo.ConvertTime(instant.Value, Zone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            // Imagen sin tzdata: Argentina no aplica horario de verano desde 2009.
            return TimeZoneInfo.CreateCustomTimeZone("ART", TimeSpan.FromHours(-3), "ART", "ART");
        }
    }
}
