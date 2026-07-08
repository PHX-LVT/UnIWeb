namespace SharedComponents.Helpers;

public static class BlockLayoutResolver
{
    public static int ResolveColumnSpan(
        int requestedSpan,
        int? defaultSpan,
        int gridColumns,
        bool usesGrid)
    {
        var fallback = defaultSpan.HasValue
            ? Math.Clamp(defaultSpan.Value, 1, 12)
            : 12;
        var span = Math.Clamp(requestedSpan == 12 ? fallback : requestedSpan, 1, 12);
        return usesGrid ? Math.Min(span, Math.Clamp(gridColumns, 1, 12)) : span;
    }

    public static double ResolvePatternAngle(
        string layoutMode,
        int index,
        int count,
        int startAngle,
        int endAngle,
        string direction)
    {
        if (count <= 1) return layoutMode == "semicircle"
            ? (startAngle + endAngle) / 2d
            : startAngle;

        if (layoutMode == "orbit")
        {
            var magnitude = Math.Abs(endAngle - startAngle);
            if (magnitude < 0.001) magnitude = 360;
            var sweep = direction == "counter-clockwise" ? -magnitude : magnitude;
            var divisor = magnitude >= 359.999 ? count : count - 1;
            return startAngle + index * (sweep / divisor);
        }

        if (layoutMode == "semicircle")
            return startAngle + index * ((endAngle - startAngle) / (double)(count - 1));

        return 0;
    }
}
