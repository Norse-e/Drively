using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.Core.Utilities;

namespace Drively.DriverInstallation;

/// <summary>
/// Validates a local driver package before it is handed to Windows:
///  - the INF exists, parses and declares a Windows driver signature directive;
///  - it references a catalog (.cat) file located inside the package folder;
///  - the catalog's digital signature is valid and trusted.
/// Unsigned packages are rejected: 64-bit Windows requires signed drivers and DRIVELY never bypasses that.
/// </summary>
public sealed class DriverValidator(ISignatureVerifier signatureVerifier)
{
    public const string SignatureFailedMessage = "Driver package signature verification failed.";

    public PackageValidationResult Validate(string infPath)
    {
        var errors = new List<string>();

        if (!File.Exists(infPath))
            return new PackageValidationResult { InfPath = infPath, Errors = ["The driver INF file was not found."] };

        InfFile inf;
        try
        {
            inf = InfFile.Load(infPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PackageValidationResult { InfPath = infPath, Errors = [$"The driver INF file could not be read: {ex.Message}"] };
        }

        if (!inf.HasValidSignatureDirective)
            errors.Add("The file is not a valid Windows driver INF (missing [Version] Signature).");

        var (_, version) = inf.DriverVer;
        string? catalogPath = null;
        SignatureCheckResult? signature = null;

        var catalogName = inf.CatalogFile;
        if (catalogName is null)
        {
            errors.Add("The driver package does not reference a catalog (.cat) file, so it is unsigned. Unsigned drivers cannot be installed on 64-bit Windows.");
        }
        else
        {
            var packageDir = Path.GetFullPath(Path.GetDirectoryName(infPath)!);
            catalogPath = Path.GetFullPath(Path.Combine(packageDir, catalogName));

            if (!catalogPath.StartsWith(packageDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("The catalog file path points outside the driver package folder.");
            }
            else if (!File.Exists(catalogPath))
            {
                errors.Add($"The catalog file '{catalogName}' referenced by the INF is missing.");
            }
            else
            {
                signature = signatureVerifier.Verify(catalogPath);
                if (!signature.IsTrusted) errors.Add($"{SignatureFailedMessage} {signature.Message}");
            }
        }

        return new PackageValidationResult
        {
            InfPath = infPath,
            Provider = inf.Provider,
            Version = version ?? "",
            Class = inf.Class,
            CatalogPath = catalogPath,
            Signature = signature,
            Errors = errors,
        };
    }
}
