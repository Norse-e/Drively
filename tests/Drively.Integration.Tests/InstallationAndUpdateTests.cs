using Drively.Core.Models;
using Drively.Core.Utilities;
using Drively.Data;
using Drively.Data.Repositories;
using Drively.DriverInstallation;
using Drively.DriverUpdates;

namespace Drively.Integration.Tests;

public class InstallationErrorHandlingTests
{
    private static DriverUpdate Update(string source = "Fake source") => new()
    {
        Candidate = new UpdateCandidate { Id = "u1", Title = "Contoso - Net - 2.0.0.0", Version = "2.0.0.0", HardwareId = "PCI\\X", Source = source },
        Device = new DeviceInfo { InstanceId = "PCI\\X\\1", Name = "Contoso NIC" },
        MatchedId = "PCI\\X",
    };

    [Fact]
    public async Task Install_without_administrator_fails_and_never_calls_provider()
    {
        var provider = new FakeUpdateProvider();
        var history = new InMemoryHistory();
        var manager = new InstallationManager([provider], new FakeElevation(false), history, NullLog.Instance);

        var result = await manager.DownloadAndInstallAsync(Update());

        Assert.False(result.Success);
        Assert.Contains("Administrator permissions are required", result.Message);
        Assert.Equal(0, provider.InstallCalls);
    }

    [Fact]
    public async Task Provider_failure_is_reported_as_failure_and_recorded()
    {
        var provider = new FakeUpdateProvider { InstallResult = OperationResult.Fail("Installation failed. Windows Update returned error 0x80240022.") };
        var history = new InMemoryHistory();
        var manager = new InstallationManager([provider], new FakeElevation(true), history, NullLog.Instance);

        var result = await manager.DownloadAndInstallAsync(Update());

        Assert.False(result.Success);
        Assert.Equal([HistoryOperation.Download, HistoryOperation.Install], history.Entries.Select(e => e.Operation));
        Assert.Equal(HistoryResult.Failed, history.Entries.Last().Result);
    }

    [Fact]
    public async Task Unexpected_exception_is_never_reported_as_success()
    {
        var provider = new FakeUpdateProvider { InstallException = new InvalidOperationException("boom") };
        var history = new InMemoryHistory();
        var result = await new InstallationManager([provider], new FakeElevation(true), history, NullLog.Instance).DownloadAndInstallAsync(Update());

        Assert.False(result.Success);
        Assert.Equal("Installation failed.", result.Message);
        Assert.Equal(HistoryResult.Failed, history.Entries.Last().Result);
    }

    [Fact]
    public async Task Failed_download_stops_before_install()
    {
        var provider = new FakeUpdateProvider { DownloadResult = OperationResult.Fail("No internet connection.") };
        var result = await new InstallationManager([provider], new FakeElevation(true), new InMemoryHistory(), NullLog.Instance).DownloadAndInstallAsync(Update());

        Assert.False(result.Success);
        Assert.Equal(0, provider.InstallCalls);
    }

    [Fact]
    public async Task Successful_install_reports_restart_requirement()
    {
        var provider = new FakeUpdateProvider { InstallResult = OperationResult.Ok("Driver installed. A restart is required.", rebootRequired: true) };
        var history = new InMemoryHistory();
        var result = await new InstallationManager([provider], new FakeElevation(true), history, NullLog.Instance).DownloadAndInstallAsync(Update());

        Assert.True(result.Success);
        Assert.True(result.RebootRequired);
        Assert.Contains("restart required", history.Entries.Last().Details);
    }

    [Fact]
    public async Task Unknown_source_fails()
    {
        var result = await new InstallationManager([new FakeUpdateProvider()], new FakeElevation(true), new InMemoryHistory(), NullLog.Instance)
            .DownloadAndInstallAsync(Update("Some other source"));
        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(3010, true, true)]
    [InlineData(259, true, false)]
    [InlineData(5, false, false)]
    [InlineData(1, false, false)]
    public void Pnputil_exit_codes_are_interpreted_honestly(int exitCode, bool success, bool reboot)
    {
        var result = PnpUtilResults.InterpretAddDriver(new ProcessResult(exitCode, "", ""));
        Assert.Equal(success, result.Success);
        Assert.Equal(reboot, result.RebootRequired);
    }

    [Fact]
    public async Task Pnputil_client_rejects_unsafe_arguments()
    {
        var client = new PnpUtilClient(new FakeProcessRunner());
        await Assert.ThrowsAsync<ArgumentException>(() => client.ExportDriverAsync("usbport.inf", Path.GetTempPath()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ExportDriverAsync("oem1.inf & del *", Path.GetTempPath()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.AddDriverAsync("relative.inf", true));
    }

    [Theory]
    [InlineData(unchecked((int)0x8024402C), "No internet connection")]
    [InlineData(unchecked((int)0x80240044), "Administrator permissions are required")]
    [InlineData(unchecked((int)0x80070422), "service is disabled")]
    [InlineData(unchecked((int)0x80241234), "0x80241234")]
    public void Windows_update_errors_are_explained(int hresult, string expected)
    {
        Assert.Contains(expected, WindowsUpdateProvider.DescribeError(hresult));
    }
}

public class UpdateCheckerTests
{
    private static readonly DeviceInfo Nic = new()
    {
        InstanceId = "PCI\\VEN_FFFF&DEV_0003\\1", Name = "Contoso NIC", HardwareIds = ["PCI\\VEN_FFFF&DEV_0003"],
        Driver = new DriverDetails { Version = "1.0.0.0", Provider = "Contoso" },
    };

    private static UpdateCandidate Offer(string hwid, string? version) => new()
    {
        Id = Guid.NewGuid().ToString(), Title = "offer", HardwareId = hwid, Version = version, Source = "Fake source",
    };

    [Fact]
    public async Task Offline_check_fails_with_explanation_instead_of_no_updates()
    {
        var result = await new UpdateChecker([new FakeUpdateProvider()], new DriverRepository(), new FakeNetwork(false), NullLog.Instance).CheckAsync([Nic]);
        Assert.False(result.Succeeded);
        Assert.Equal("No internet connection.", result.Message);
    }

    [Fact]
    public async Task Source_failure_is_explained()
    {
        var provider = new FakeUpdateProvider { SearchResult = UpdateSearchResult.Failure("The Windows Update service is disabled on this PC.") };
        var result = await new UpdateChecker([provider], new DriverRepository(), new FakeNetwork(true), NullLog.Instance).CheckAsync([Nic]);
        Assert.False(result.Succeeded);
        Assert.Contains("could not be completed", result.Message);
        Assert.Contains("disabled", result.Message);
    }

    [Fact]
    public async Task Only_compatible_newer_updates_are_shown()
    {
        var provider = new FakeUpdateProvider
        {
            SearchResult = UpdateSearchResult.Success(
            [
                Offer("PCI\\VEN_FFFF&DEV_0003", "2.0.0.0"),   // valid
                Offer("PCI\\VEN_FFFF&DEV_0003", "0.5.0.0"),   // older
                Offer("PCI\\VEN_EEEE&DEV_9999", "9.0.0.0"),   // hardware not present
            ]),
        };
        var repo = new DriverRepository();
        var result = await new UpdateChecker([provider], repo, new FakeNetwork(true), NullLog.Instance).CheckAsync([Nic]);

        Assert.True(result.Succeeded);
        Assert.Equal("2.0.0.0", result.Updates.Single().NewVersion);
        Assert.Equal(2, result.RejectedCount);
        Assert.Same(result, repo.Latest);
    }

    [Fact]
    public async Task No_updates_uses_the_verified_message()
    {
        var result = await new UpdateChecker([new FakeUpdateProvider()], new DriverRepository(), new FakeNetwork(true), NullLog.Instance).CheckAsync([Nic]);
        Assert.Equal(UpdateCheckResult.NoUpdatesMessage, result.Message);
    }
}

public class ManufacturerLinkTests
{
    [Fact]
    public void Links_chip_vendor_and_pc_maker_with_official_https_urls()
    {
        var device = new DeviceInfo { InstanceId = "x", Name = "GPU", VendorId = "10DE" };
        var links = new ManufacturerProvider().GetSupportLinks(device, new SystemInfo { Manufacturer = "Dell Inc." });

        Assert.Contains(links, l => l.Title.StartsWith("NVIDIA"));
        Assert.Contains(links, l => l.Title.StartsWith("Dell"));
        Assert.All(links, l => Assert.True(l.Url.StartsWith("https://") || l.Url.StartsWith("ms-settings:")));
    }

    [Fact]
    public void Does_not_match_vendor_names_inside_other_words()
    {
        // "hp" must not match inside unrelated words.
        var device = new DeviceInfo { InstanceId = "x", Name = "d", Manufacturer = "Chipworks Ltd" };
        var links = new ManufacturerProvider().GetSupportLinks(device, null);
        Assert.DoesNotContain(links, l => l.Title.StartsWith("HP"));
    }
}

public class DataStorageTests
{
    [Fact]
    public async Task History_round_trips_and_clears()
    {
        using var temp = new TempDir();
        var repo = new HistoryRepository(new DatabaseContext(Path.Combine(temp.Path, "t.db")));

        await repo.AddAsync(new HistoryEntry { Device = "NIC", DriverVersion = "2.0", Operation = HistoryOperation.Install, Result = HistoryResult.Succeeded, Details = "ok" });
        await repo.AddAsync(new HistoryEntry { Device = "GPU", DriverVersion = "3.0", Operation = HistoryOperation.Download, Result = HistoryResult.Failed, Details = "x" });

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.Device == "NIC" && e.Operation == HistoryOperation.Install && e.Result == HistoryResult.Succeeded);

        await repo.ClearAsync();
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public void Settings_round_trip()
    {
        using var temp = new TempDir();
        var db = new DatabaseContext(Path.Combine(temp.Path, "t.db"));
        var repo = new SettingsRepository(db);

        var now = DateTimeOffset.Now;
        repo.Save(new AppSettings { Theme = ThemePreference.Dark, BackupDirectory = @"D:\B", CheckForUpdatesOnStartup = true, LastScan = now });
        var loaded = new SettingsRepository(new DatabaseContext(Path.Combine(temp.Path, "t.db"))).Load();

        Assert.Equal(ThemePreference.Dark, loaded.Theme);
        Assert.Equal(@"D:\B", loaded.BackupDirectory);
        Assert.True(loaded.CheckForUpdatesOnStartup);
        Assert.Equal(now.ToUnixTimeSeconds(), loaded.LastScan!.Value.ToUnixTimeSeconds());
    }
}
