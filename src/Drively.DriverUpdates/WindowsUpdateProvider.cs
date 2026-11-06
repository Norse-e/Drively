using System.Runtime.InteropServices;
using Drively.Core.Interfaces;
using Drively.Core.Models;
using Drively.Core.Utilities;
using Microsoft.Win32;

namespace Drively.DriverUpdates;

/// <summary>
/// Discovers, downloads and installs driver updates through the Windows Update Agent API
/// (Microsoft.Update.Session, documented at learn.microsoft.com/windows/win32/wua_sdk).
/// Everything goes through Windows' own update stack: Microsoft hosts and signs the packages,
/// Windows verifies content hashes and driver signatures, and Windows decides applicability.
/// Uses late-bound COM so no interop assembly is required.
/// </summary>
public sealed class WindowsUpdateProvider(ILog log) : IUpdateProvider
{
    public const string SourceName = "Microsoft Windows Update";
    private const string SearchCriteria = "IsInstalled=0 and Type='Driver' and IsHidden=0";

    // WUA result codes (OperationResultCode).
    private const int OrcSucceeded = 2;
    private const int OrcSucceededWithErrors = 3;

    private object? _session;

    public string Name => SourceName;

    public string NetworkDisclosure =>
        "Microsoft Windows Update service, contacted through the built-in Windows Update Agent — only when you check for, download or install updates.";

    public Task<UpdateSearchResult> SearchAsync(CancellationToken cancellationToken = default) =>
        Task.Factory.StartNew(Search, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public Task<OperationResult> DownloadAsync(UpdateCandidate update, CancellationToken cancellationToken = default) =>
        Task.Factory.StartNew(() => Download(update), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public Task<OperationResult> InstallAsync(UpdateCandidate update, CancellationToken cancellationToken = default) =>
        Task.Factory.StartNew(() => Install(update), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private UpdateSearchResult Search()
    {
        try
        {
            dynamic session = GetSession();
            dynamic searcher = session.CreateUpdateSearcher();
            searcher.Online = true;

            log.Info("Windows Update: searching for driver updates.");
            dynamic result = searcher.Search(SearchCriteria);
            int resultCode = (int)result.ResultCode;
            if (resultCode is not (OrcSucceeded or OrcSucceededWithErrors))
                return UpdateSearchResult.Failure($"Windows Update could not complete the search (result code {resultCode}).");

            dynamic updates = result.Updates;
            int count = updates.Count;
            var candidates = new List<UpdateCandidate>(count);
            for (var i = 0; i < count; i++)
            {
                var candidate = ToCandidate(updates.Item(i));
                if (candidate is not null) candidates.Add(candidate);
            }

            log.Info($"Windows Update: {candidates.Count} driver update(s) offered.");
            return UpdateSearchResult.Success(candidates, DriverPolicyNote());
        }
        catch (COMException ex)
        {
            log.Error($"Windows Update search failed (0x{ex.HResult:X8}).", ex);
            return UpdateSearchResult.Failure(DescribeError(ex.HResult));
        }
        catch (Exception ex) when (ex is InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidOperationException)
        {
            log.Error("Windows Update search returned unexpected data.", ex);
            return UpdateSearchResult.Failure("Windows Update returned data DRIVELY could not read. No results are shown.");
        }
    }

    private UpdateCandidate? ToCandidate(dynamic u)
    {
        string title = Read(() => (string)u.Title) ?? "";
        string id = Read(() => (string)u.Identity.UpdateID) ?? "";
        if (id.Length == 0) return null;

        object? size = Read<object>(() => u.MaxDownloadSize);
        DateTime? driverDate = Read<object>(() => u.DriverVerDate) is DateTime d ? d.Date : null;

        return new UpdateCandidate
        {
            Id = id,
            Title = title,
            Description = Read(() => (string)u.Description) ?? "",
            HardwareId = Read(() => (string)u.DriverHardwareID) ?? "",
            Manufacturer = Read(() => (string)u.DriverManufacturer) ?? "",
            Provider = Read(() => (string)u.DriverProvider) ?? "",
            DriverClass = Read(() => (string)u.DriverClass) ?? "",
            Model = Read(() => (string)u.DriverModel) ?? "",
            Version = DriverVersion.ExtractTrailingVersion(title),
            DriverDate = driverDate,
            DownloadSizeBytes = size is null ? null : Convert.ToInt64(size, System.Globalization.CultureInfo.InvariantCulture),
            SupportUrl = Read(() => (string)u.SupportUrl),
            IsDownloaded = Read<object>(() => u.IsDownloaded) is true,
            EulaAccepted = Read<object>(() => u.EulaAccepted) is not false,
            Source = SourceName,
            SignatureStatus = "Microsoft-signed package from Windows Update (signature enforced by Windows at install)",
            ProviderState = u,
        };
    }

    private OperationResult Download(UpdateCandidate update)
    {
        if (update.ProviderState is null) return OperationResult.Fail("This update is no longer available. Check for updates again.");
        try
        {
            dynamic session = GetSession();
            dynamic collection = CreateCom("Microsoft.Update.UpdateColl");
            collection.Add(update.ProviderState);

            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = collection;
            log.Info($"Windows Update: downloading '{update.Title}'.");
            dynamic result = downloader.Download();
            int code = (int)result.ResultCode;

            return code == OrcSucceeded
                ? OperationResult.Ok("Driver package downloaded and verified by Windows Update.")
                : OperationResult.Fail($"The download did not complete (Windows Update result code {code}).");
        }
        catch (COMException ex)
        {
            log.Error($"Windows Update download failed (0x{ex.HResult:X8}).", ex);
            return OperationResult.Fail(DescribeError(ex.HResult), $"HRESULT 0x{ex.HResult:X8}");
        }
    }

    private OperationResult Install(UpdateCandidate update)
    {
        if (update.ProviderState is null) return OperationResult.Fail("This update is no longer available. Check for updates again.");
        if (!update.EulaAccepted)
        {
            return OperationResult.Fail(
                "This update requires accepting a license agreement. Install it from Windows Settings > Windows Update > Advanced options > Optional updates.");
        }

        try
        {
            dynamic session = GetSession();
            dynamic collection = CreateCom("Microsoft.Update.UpdateColl");
            collection.Add(update.ProviderState);

            dynamic installer = session.CreateUpdateInstaller();
            if ((bool)installer.IsBusy)
                return OperationResult.Fail("Windows Update is busy installing other updates. Try again later.");

            installer.Updates = collection;
            log.Info($"Windows Update: installing '{update.Title}'.");
            dynamic result = installer.Install();
            int code = (int)result.ResultCode;
            bool reboot = (bool)result.RebootRequired;
            int hr = Read<object>(() => result.GetUpdateResult(0).HResult) is int h ? h : 0;

            return code switch
            {
                OrcSucceeded => OperationResult.Ok(
                    reboot ? "Driver installed. A restart is required to finish installation." : "Driver installed successfully.",
                    rebootRequired: reboot),
                OrcSucceededWithErrors => OperationResult.Ok(
                    "Driver installed, but Windows Update reported warnings." + (reboot ? " A restart is required." : ""),
                    rebootRequired: reboot, details: $"HRESULT 0x{hr:X8}"),
                _ => OperationResult.Fail(
                    hr != 0 ? $"Installation failed. {DescribeError(hr)}" : $"Installation failed (Windows Update result code {code}).",
                    $"Result code {code}, HRESULT 0x{hr:X8}"),
            };
        }
        catch (COMException ex)
        {
            log.Error($"Windows Update install failed (0x{ex.HResult:X8}).", ex);
            return OperationResult.Fail($"Installation failed. {DescribeError(ex.HResult)}", $"HRESULT 0x{ex.HResult:X8}");
        }
    }

    /// <summary>Maps common Windows Update error codes to plain-language messages.</summary>
    public static string DescribeError(int hresult) => unchecked((uint)hresult) switch
    {
        0x8024402C or 0x80072EE7 or 0x80072EFD or 0x80072EFE or 0x80072F8F or 0x8024401C or 0x80244022 or 0x8024402F =>
            "No internet connection, or the Windows Update service could not be reached.",
        0x80070422 => "The Windows Update service is disabled on this PC.",
        0x8024002E => "Access to Windows Update is blocked by a policy on this PC.",
        0x80240044 or 0x80070005 => "Administrator permissions are required.",
        0x80240016 => "Another Windows Update installation is in progress. Try again later.",
        0x8024001E => "The Windows Update service is shutting down. Try again later.",
        0x80240022 => "Windows Update reported that all updates in the request failed.",
        0x8024000B => "The Windows Update operation was cancelled.",
        0x80246007 => "The update has not been downloaded yet.",
        0x80240020 => "The update could not be installed because no user is signed in to Windows.",
        var code => $"Windows Update returned error 0x{code:X8}.",
    };

    private static string? DriverPolicyNote()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");
            return key?.GetValue("ExcludeWUDriversInQualityUpdate") is int v && v == 1
                ? "A policy on this PC excludes drivers from Windows Update, so Windows Update may not offer driver updates."
                : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private dynamic GetSession()
    {
        if (_session is null)
        {
            dynamic session = CreateCom("Microsoft.Update.Session");
            session.ClientApplicationID = "DRIVELY";
            _session = session;
        }
        return _session;
    }

    private static dynamic CreateCom(string progId)
    {
#pragma warning disable CA2201 // Surfaced through the same COMException path as other WUA failures.
        var type = Type.GetTypeFromProgID(progId)
                   ?? throw new COMException($"{progId} is not available on this PC.", unchecked((int)0x80040154));
#pragma warning restore CA2201
        return Activator.CreateInstance(type)!;
    }

    private static string? Read(Func<string> getter)
    {
        try { return getter(); } catch (Exception) { return null; }
    }

    private static T? Read<T>(Func<T> getter) where T : class
    {
        try { return getter(); } catch (Exception) { return null; }
    }
}
