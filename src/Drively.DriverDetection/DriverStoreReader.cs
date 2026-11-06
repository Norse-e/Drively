using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.Core.Utilities;

namespace Drively.DriverDetection;

/// <summary>
/// Lists third-party driver packages by reading the published oemNN.inf files in %windir%\INF.
/// This includes packages not currently used by a connected device. Read-only.
/// </summary>
public sealed class DriverStoreReader(ILog log, string? infDirectory = null) : IDriverStoreReader
{
    private readonly string _infDirectory = infDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");

    public IReadOnlyList<DriverPackageInfo> GetThirdPartyPackages()
    {
        if (!Directory.Exists(_infDirectory)) return [];

        var packages = new List<DriverPackageInfo>();
        foreach (var path in Directory.EnumerateFiles(_infDirectory, "oem*.inf"))
        {
            try
            {
                var inf = InfFile.Load(path);
                var (date, version) = inf.DriverVer;
                packages.Add(new DriverPackageInfo
                {
                    PublishedName = Path.GetFileName(path).ToLowerInvariant(),
                    InfPath = path,
                    Provider = inf.Provider,
                    Version = version ?? "",
                    Date = date,
                    Class = inf.Class,
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Warn($"Could not read {Path.GetFileName(path)}.", ex);
            }
        }

        return packages
            .OrderBy(p => OemNumber(p.PublishedName))
            .ToList();
    }

    private static int OemNumber(string publishedName) =>
        int.TryParse(publishedName.AsSpan(3, publishedName.Length - 7), out var n) ? n : int.MaxValue;
}
