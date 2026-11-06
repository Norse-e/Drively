using Drively.Core.Models;
using Drively.Core.Utilities;

namespace Drively.Core.Services;

public sealed record CompatibilityDecision(
    bool IsCompatible,
    string Reason,
    DeviceInfo? Device = null,
    string? MatchedId = null,
    MatchKind MatchKind = MatchKind.HardwareId);

/// <summary>
/// Decides whether an offered update has enough evidence of compatibility to be shown.
/// Rules (all must hold):
///  1. The update declares a hardware ID.
///  2. That ID exactly matches a hardware ID (preferred) or compatible ID of a device present on this PC.
///  3. If both the installed and offered versions are known, the offered version must be strictly higher.
/// Driver dates are deliberately not used: an older date does not mean a driver is outdated.
/// </summary>
public static class CompatibilityEvaluator
{
    public static CompatibilityDecision Evaluate(UpdateCandidate candidate, IReadOnlyList<DeviceInfo> devices)
    {
        if (string.IsNullOrWhiteSpace(candidate.HardwareId))
            return new(false, $"'{candidate.Title}' does not declare a hardware ID.");

        var hwid = candidate.HardwareId.Trim();
        DeviceInfo? match = null;
        var kind = MatchKind.HardwareId;

        foreach (var device in devices)
        {
            if (device.HardwareIds.Any(id => id.Equals(hwid, StringComparison.OrdinalIgnoreCase)))
            {
                match = device;
                kind = MatchKind.HardwareId;
                break;
            }

            if (match is null && device.CompatibleIds.Any(id => id.Equals(hwid, StringComparison.OrdinalIgnoreCase)))
            {
                match = device;
                kind = MatchKind.CompatibleId;
            }
        }

        if (match is null)
            return new(false, $"'{candidate.Title}': no device on this PC matches hardware ID {hwid}.");

        var installed = match.Driver?.Version;
        if (!string.IsNullOrWhiteSpace(installed) && candidate.Version is not null)
        {
            var cmp = DriverVersion.Compare(candidate.Version, installed);
            if (cmp is <= 0)
                return new(false,
                    $"'{candidate.Title}': installed version {installed} on '{match.Name}' is the same or newer than {candidate.Version}.");
        }

        var reason = kind == MatchKind.HardwareId
            ? $"Hardware ID {hwid} matches '{match.Name}'."
            : $"Compatible ID {hwid} matches '{match.Name}'.";
        return new(true, reason, match, hwid, kind);
    }
}
