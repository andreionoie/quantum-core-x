namespace QuantumCore.API.Systems.Formulas;

/// <summary>
/// Making available legacy formula functions by loading this class into Flee parser using
/// <see cref="Flee.PublicTypes.ExpressionImports.AddType(Type)"/>. SkillFormula expressions will match the public static
/// method names here case insensitively.
/// </summary>
public static class FormulaFunctions
{
    public static double Floor(double x) => Math.Floor(x);
    public static double Abs(double x) => Math.Abs(x);

    public static double Sign(double x) => x == 0.0 ? 0.0 : (x < 0.0 ? -1.0 : 1.0);

    public static double Min(double x, double y) => Math.Min(x, y);
    public static double Max(double x, double y) => Math.Max(x, y);

    public static double Sqrt(double x) => x < 0.0 ? 0.0 : Math.Sqrt(x);
    public static double Rt(double x) => Sqrt(x);

    public static double Cos(double x) => Math.Cos(x);
    public static double Sin(double x) => Math.Sin(x);

    public static double Tan(double x)
    {
        return NearZero(Math.Cos(x)) ? 0.0 : Math.Tan(x);
    }

    public static double Sec(double x)
    {
        var c = Math.Cos(x);
        if (NearZero(c)) return 0.0;
        return 1.0 / c;
    }

    public static double Csc(double x)
    {
        var s = Math.Sin(x);
        if (NearZero(s)) return 0.0;
        return 1.0 / s;
    }

    public static double Cosec(double x) => Csc(x);

    public static double Cot(double x)
    {
        var s = Math.Sin(x);
        if (NearZero(s)) return 0.0;
        return Math.Cos(x) / s;
    }

    public static double Ln(double x) => (x <= 0.0) ? 0.0 : Math.Log(x);
    public static double Log10(double x) => (x <= 0.0) ? 0.0 : Math.Log10(x);

    public static double Log(double @base, double value)
    {
        if (value <= 0.0) return 0.0;
        if (@base is <= 0.0 or 1.0) return 0.0;
        return Math.Log(value) / Math.Log(@base);
    }

    public static double Mod(double x, double y) => x % y;

    public static double Number(double start, double end)
    {
        var (lo, hiInclusive) = LegacyIntRange(start, end);
        return Random.Shared.Next(lo, checked(hiInclusive + 1));
    }

    public static double IRand(double start, double end) => Number(start, end);
    public static double IRandom(double start, double end) => Number(start, end);

    public static double Frand(double start, double end)
    {
        var (a, b) = Normalize(start, end);
        return NearZero(b - a) ? a : RandomBetween(a, b);
    }

    public static double Frandom(double start, double end) => Frand(start, end);

    private static (double A, double B) Normalize(double a, double b) => a <= b ? (a, b) : (b, a);

    private static bool NearZero(double x) => Math.Abs(x) <= 2e-16;

    private static double RandomBetween(double start, double end)
    {
        var (a, b) = Normalize(start, end);
        if (NearZero(b - a)) return a;
        return Random.Shared.NextDouble() * (b - a) + a; // [a, b)
    }

    private static (int StartInt, int EndIntInclusive) LegacyIntRange(double start, double end)
    {
        (start, end) = Normalize(start, end);

        var startInt = (int)Math.Truncate(start + 0.5);
        var length = (int)Math.Truncate(end - start + 0.5) + 1;
        var endIntInclusive = checked(startInt + length - 1);

        if (endIntInclusive < startInt) endIntInclusive = startInt;

        return (startInt, endIntInclusive);
    }
}
