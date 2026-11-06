using Drively.Core.Models;

namespace Drively.Core.Interfaces;

/// <summary>Detects Plug and Play devices and their drivers. Read-only.</summary>
public interface IHardwareProvider
{
    Task<ScanResult> ScanAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads basic PC information (Windows version, manufacturer, model).</summary>
public interface ISystemInfoProvider
{
    Task<SystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default);
}

/// <summary>Lists third-party driver packages in the Windows driver store.</summary>
public interface IDriverStoreReader
{
    IReadOnlyList<DriverPackageInfo> GetThirdPartyPackages();
}

/// <summary>A trusted source of driver updates (e.g. Microsoft Windows Update).</summary>
public interface IUpdateProvider
{
    /// <summary>Unique, user-facing source name. Shown as "Download source".</summary>
    string Name { get; }

    /// <summary>Hosts contacted by this provider, disclosed in Settings.</summary>
    string NetworkDisclosure { get; }

    Task<UpdateSearchResult> SearchAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> DownloadAsync(UpdateCandidate update, CancellationToken cancellationToken = default);

    Task<OperationResult> InstallAsync(UpdateCandidate update, CancellationToken cancellationToken = default);
}

/// <summary>Verifies Authenticode / catalog signatures.</summary>
public interface ISignatureVerifier
{
    SignatureCheckResult Verify(string filePath);
}

/// <summary>Runs an external process and captures its output.</summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

/// <summary>Reports whether DRIVELY has administrator rights and can relaunch elevated.</summary>
public interface IElevationService
{
    bool IsElevated { get; }

    /// <summary>Relaunches DRIVELY elevated (UAC prompt). Returns false if the user cancelled.</summary>
    bool TryRestartElevated();
}

/// <summary>Reports local network availability without contacting any server.</summary>
public interface INetworkStatus
{
    bool IsNetworkAvailable { get; }
}

public interface IHistoryRepository
{
    Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public interface ISettingsRepository
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>Minimal local diagnostic log. Never sent anywhere.</summary>
public interface ILog
{
    void Info(string message);
    void Warn(string message, Exception? exception = null);
    void Error(string message, Exception? exception = null);
}
