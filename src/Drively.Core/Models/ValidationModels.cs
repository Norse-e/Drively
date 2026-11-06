namespace Drively.Core.Models;

/// <summary>Result of verifying a file's digital signature.</summary>
public sealed record SignatureCheckResult(bool IsTrusted, string? Signer, string Message)
{
    public static SignatureCheckResult Trusted(string? signer) => new(true, signer, "Signature is valid and trusted.");
    public static SignatureCheckResult Untrusted(string message, string? signer = null) => new(false, signer, message);
}

/// <summary>Result of validating a local driver package (INF + catalog + signature).</summary>
public sealed class PackageValidationResult
{
    public required string InfPath { get; init; }
    public string Provider { get; init; } = "";
    public string Version { get; init; } = "";
    public string Class { get; init; } = "";
    public string? CatalogPath { get; init; }
    public SignatureCheckResult? Signature { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool IsValid => Errors.Count == 0;
}

/// <summary>Exit code and captured output of an external process (e.g. pnputil).</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>A link to an official support page.</summary>
public sealed record SupportLink(string Title, string Url, string Description);
