using System.Globalization;

namespace KanbanCord.Core.Models;

public static class CardReference
{
    public static string Format(long number)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        return $"BOO-{number.ToString("D3", CultureInfo.InvariantCulture)}";
    }

    public static bool TryParse(string reference, out long number)
    {
        number = 0;
        var value = reference.Trim();
        return value.StartsWith("BOO-", StringComparison.OrdinalIgnoreCase)
               && long.TryParse(value.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out number)
               && number > 0;
    }
}
