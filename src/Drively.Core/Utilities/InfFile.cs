using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Drively.Core.Utilities;

/// <summary>
/// A minimal, read-only INF parser covering what DRIVELY needs: the [Version] section
/// (Signature, Class, Provider, DriverVer, CatalogFile) with %string% token resolution.
/// </summary>
public sealed partial class InfFile
{
    private readonly Dictionary<string, List<KeyValuePair<string, string>>> _sections;
    private readonly Dictionary<string, string> _strings;

    private InfFile(Dictionary<string, List<KeyValuePair<string, string>>> sections)
    {
        _sections = sections;
        _strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Prefer the neutral [Strings] section, then fill gaps from localized ones (e.g. [Strings.0409]).
        foreach (var name in sections.Keys.OrderBy(k => k.Equals("Strings", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
        {
            if (!name.Equals("Strings", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("Strings.", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var (key, value) in sections[name]) _strings.TryAdd(key, value);
        }
    }

    public IReadOnlyCollection<string> SectionNames => _sections.Keys;

    public static InfFile Load(string path)
    {
        // INF files are usually UTF-16LE (with BOM) or ANSI. Latin-1 is a lossless fallback for ANSI bytes.
        using var reader = new StreamReader(path, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);
        return Parse(reader.ReadToEnd());
    }

    public static InfFile Parse(string content)
    {
        var sections = new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);
        List<KeyValuePair<string, string>>? current = null;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0) continue;

            if (line[0] == '[')
            {
                var end = line.IndexOf(']');
                if (end <= 1) { current = null; continue; }
                var name = line[1..end].Trim();
                if (!sections.TryGetValue(name, out current))
                {
                    current = [];
                    sections[name] = current;
                }
                continue;
            }

            if (current is null) continue;

            var eq = line.IndexOf('=');
            if (eq < 0)
            {
                current.Add(new(line, ""));
                continue;
            }

            var key = Unquote(line[..eq].Trim());
            var value = Unquote(line[(eq + 1)..].Trim());
            current.Add(new(key, value));
        }

        return new InfFile(sections);
    }

    /// <summary>Returns the first value for a key in a section, with %tokens% resolved.</summary>
    public string? GetValue(string section, string key)
    {
        if (!_sections.TryGetValue(section, out var entries)) return null;
        foreach (var (k, v) in entries)
        {
            if (k.Equals(key, StringComparison.OrdinalIgnoreCase)) return Resolve(v);
        }
        return null;
    }

    public string Signature => GetValue("Version", "Signature") ?? "";
    public string Class => GetValue("Version", "Class") ?? "";
    public string ClassGuid => GetValue("Version", "ClassGuid") ?? "";
    public string Provider => GetValue("Version", "Provider") ?? "";

    /// <summary>The catalog for x64, falling back to the generic NT / undecorated entries.</summary>
    public string? CatalogFile =>
        NullIfEmpty(GetValue("Version", "CatalogFile.NTamd64"))
        ?? NullIfEmpty(GetValue("Version", "CatalogFile.NT"))
        ?? NullIfEmpty(GetValue("Version", "CatalogFile"));

    /// <summary>True if the [Version] Signature identifies a Windows driver INF.</summary>
    public bool HasValidSignatureDirective =>
        Signature.Equals("$Windows NT$", StringComparison.OrdinalIgnoreCase) ||
        Signature.Equals("$Chicago$", StringComparison.OrdinalIgnoreCase) ||
        Signature.Equals("$Windows 95$", StringComparison.OrdinalIgnoreCase);

    /// <summary>Parses DriverVer = mm/dd/yyyy[,w.x.y.z].</summary>
    public (DateTime? Date, string? Version) DriverVer
    {
        get
        {
            var raw = GetValue("Version", "DriverVer");
            if (string.IsNullOrWhiteSpace(raw)) return (null, null);

            var parts = raw.Split(',', 2, StringSplitOptions.TrimEntries);
            DateTime? date = DateTime.TryParseExact(parts[0], ["MM/dd/yyyy", "M/d/yyyy"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d) ? d : null;
            var version = parts.Length > 1 && DriverVersion.TryParse(parts[1], out _) ? parts[1] : null;
            return (date, version);
        }
    }

    private string Resolve(string value) =>
        TokenRegex().Replace(value, m =>
        {
            var token = m.Groups[1].Value;
            if (token.Length == 0) return "%";
            return _strings.TryGetValue(token, out var s) ? s : m.Value;
        });

    private static string StripComment(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') inQuotes = !inQuotes;
            else if (line[i] == ';' && !inQuotes) return line[..i];
        }
        return line;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1].Replace("\"\"", "\"");
        return value;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    [GeneratedRegex("%([^%]*)%")]
    private static partial Regex TokenRegex();
}
