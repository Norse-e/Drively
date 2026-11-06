using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.Integration.Tests;

/// <summary>Records process invocations instead of running them. Never touches real drivers.</summary>
internal sealed class FakeProcessRunner(Func<IReadOnlyList<string>, ProcessResult>? behavior = null) : IProcessRunner
{
    public List<IReadOnlyList<string>> Calls { get; } = [];

    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Calls.Add(arguments);
        return Task.FromResult(behavior?.Invoke(arguments) ?? new ProcessResult(0, "", ""));
    }

    /// <summary>Simulates "pnputil /export-driver oemN.inf dir" by writing a fake package.</summary>
    public static ProcessResult SimulateExport(IReadOnlyList<string> args)
    {
        if (args[0] != "/export-driver") return new ProcessResult(0, "", "");
        var dir = args[2];
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "contoso.inf"),
            "[Version]\nSignature=\"$Windows NT$\"\nClass=Net\nProvider=Contoso\nCatalogFile=contoso.cat\nDriverVer=01/01/2024,1.0.0.0\n");
        File.WriteAllText(Path.Combine(dir, "contoso.cat"), "fake catalog");
        return new ProcessResult(0, "Exported", "");
    }
}

internal sealed class FakeElevation(bool elevated) : IElevationService
{
    public bool IsElevated => elevated;
    public bool TryRestartElevated() => false;
}

internal sealed class FakeSignatureVerifier(bool trusted) : ISignatureVerifier
{
    public List<string> Verified { get; } = [];

    public SignatureCheckResult Verify(string filePath)
    {
        Verified.Add(filePath);
        return trusted ? SignatureCheckResult.Trusted("Test signer") : SignatureCheckResult.Untrusted("The file is not digitally signed.");
    }
}

internal sealed class InMemoryHistory : IHistoryRepository
{
    public List<HistoryEntry> Entries { get; } = [];
    public Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default) { Entries.Add(entry); return Task.CompletedTask; }
    public Task<IReadOnlyList<HistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HistoryEntry>>(Entries);
    public Task ClearAsync(CancellationToken cancellationToken = default) { Entries.Clear(); return Task.CompletedTask; }
}

internal sealed class FakeDriverStore(params DriverPackageInfo[] packages) : IDriverStoreReader
{
    public IReadOnlyList<DriverPackageInfo> GetThirdPartyPackages() => packages;
}

internal sealed class FakeNetwork(bool available) : INetworkStatus
{
    public bool IsNetworkAvailable => available;
}

internal sealed class FakeUpdateProvider : IUpdateProvider
{
    public string Name => "Fake source";
    public string NetworkDisclosure => "none";
    public UpdateSearchResult SearchResult { get; set; } = UpdateSearchResult.Success([]);
    public OperationResult DownloadResult { get; set; } = OperationResult.Ok("downloaded");
    public OperationResult InstallResult { get; set; } = OperationResult.Ok("installed");
    public Exception? InstallException { get; set; }
    public int InstallCalls { get; private set; }

    public Task<UpdateSearchResult> SearchAsync(CancellationToken cancellationToken = default) => Task.FromResult(SearchResult);
    public Task<OperationResult> DownloadAsync(UpdateCandidate update, CancellationToken cancellationToken = default) => Task.FromResult(DownloadResult);

    public Task<OperationResult> InstallAsync(UpdateCandidate update, CancellationToken cancellationToken = default)
    {
        InstallCalls++;
        return InstallException is null ? Task.FromResult(InstallResult) : Task.FromException<OperationResult>(InstallException);
    }
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("drively-test-").FullName;
    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch (IOException) { }
    }
}
