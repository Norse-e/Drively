using System.Globalization;
using System.Text.RegularExpressions;

namespace Drively.Core.Utilities;

/// <summary>
/// A Windows driver version (w.x.y.z, as in the INF DriverVer directive). Missing parts compare as 0.
/// Note: a higher version is the only signal DRIVELY uses for "newer"; driver dates are never used
/// to decide that a driver is outdated.
/// </summary>
public readonly partial struct DriverVersion : IComparable<DriverVersion>, IEquatable<DriverVersion>
{
    private readonly long _a, _b, _c, _d;

    private DriverVersion(long a, long b, long c, long d)
    {
        _a = a; _b = b; _c = c; _d = d;
    }

    public long Major => _a;
    public long Minor => _b;
    public long Build => _c;
    public long Revision => _d;

    public static bool TryParse(string? text, out DriverVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var segments = text.Trim().Split('.');
        if (segments.Length is < 1 or > 4) return false;

        var parts = new long[4];
        for (var i = 0; i < segments.Length; i++)
        {
            if (!long.TryParse(segments[i], NumberStyles.None, CultureInfo.InvariantCulture, out parts[i])) return false;
            if (parts[i] > uint.MaxValue) return false;
        }

        version = new DriverVersion(parts[0], parts[1], parts[2], parts[3]);
        return true;
    }

    public static DriverVersion Parse(string text) =>
        TryParse(text, out var v) ? v : throw new FormatException($"'{text}' is not a valid driver version.");

    /// <summary>
    /// Extracts a trailing version token from text such as a Windows Update title
    /// ("Intel - Display - 31.0.101.4502"). Returns null if none is present; never guesses.
    /// </summary>
    public static string? ExtractTrailingVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = TrailingVersionRegex().Match(text.Trim());
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Compares two version strings. Returns null when either cannot be parsed.</summary>
    public static int? Compare(string? left, string? right) =>
        TryParse(left, out var l) && TryParse(right, out var r) ? l.CompareTo(r) : null;

    public int CompareTo(DriverVersion other)
    {
        var c = _a.CompareTo(other._a);
        if (c != 0) return c;
        c = _b.CompareTo(other._b);
        if (c != 0) return c;
        c = _c.CompareTo(other._c);
        return c != 0 ? c : _d.CompareTo(other._d);
    }

    public bool Equals(DriverVersion other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is DriverVersion v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);
    public override string ToString() => $"{_a}.{_b}.{_c}.{_d}";

    public static bool operator ==(DriverVersion l, DriverVersion r) => l.Equals(r);
    public static bool operator !=(DriverVersion l, DriverVersion r) => !l.Equals(r);
    public static bool operator <(DriverVersion l, DriverVersion r) => l.CompareTo(r) < 0;
    public static bool operator >(DriverVersion l, DriverVersion r) => l.CompareTo(r) > 0;
    public static bool operator <=(DriverVersion l, DriverVersion r) => l.CompareTo(r) <= 0;
    public static bool operator >=(DriverVersion l, DriverVersion r) => l.CompareTo(r) >= 0;

    [GeneratedRegex(@"(?:^|[\s\-_v(])(\d+(?:\.\d+){1,3})\)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingVersionRegex();
}
