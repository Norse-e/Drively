namespace Drively.Core.Models;

/// <summary>Result of a download / install / restore operation. Never reports success on failure.</summary>
public sealed record OperationResult
{
    public bool Success { get; init; }
    public bool RebootRequired { get; init; }
    public string Message { get; init; } = "";

    /// <summary>Technical details for logs (exit codes, tool output). Not shown by default.</summary>
    public string Details { get; init; } = "";

    public static OperationResult Ok(string message, bool rebootRequired = false, string details = "") =>
        new() { Success = true, Message = message, RebootRequired = rebootRequired, Details = details };

    public static OperationResult Fail(string message, string details = "") =>
        new() { Success = false, Message = message, Details = details };
}
