using Drively.Backup;
using Drively.Core.Models;
using Drively.Core.Utilities;
using Drively.DriverInstallation;

namespace Drively.Integration.Tests;

/// <summary>Backup and restore with a fake pnputil: exercises the real logic without touching drivers.</summary>
public class BackupRestoreTests
{
    private static readonly DriverPackageInfo Package = new()
    {
        PublishedName = "oem7.inf", Provider = "Contoso", Version = "1.0.0.0", Class = "Net", Date = new DateTime(2024, 1, 1),
    };

    private static readonly DeviceInfo Device = new()
    {
        InstanceId = "PCI\\X", Name = "Contoso Ethernet",
        Driver = new DriverDetails { InfName = "oem7.inf", Provider = "Contoso", Version = "1.0.0.0" },
    };

    private static BackupManager CreateBackupManager(FakeProcessRunner runner, bool elevated, InMemoryHistory history) =>
        new(new FakeDriverStore(Package), new PnpUtilClient(runner), new FakeElevation(elevated), history, NullLog.Instance);

    [Fact]
    public async Task Backup_exports_each_package_and_writes_manifest()
    {
        using var temp = new TempDir();
        var runner = new FakeProcessRunner(FakeProcessRunner.SimulateExport);
        var history = new InMemoryHistory();

        var result = await CreateBackupManager(runner, true, history).CreateBackupAsync(temp.Path, [Device], null, "0.1.0");

        Assert.True(result.Success, result.Message);
        Assert.Equal(["/export-driver", "oem7.inf", Path.Combine(result.Backup!.FolderPath, "oem7")], runner.Calls.Single());
        var entry = result.Backup.Manifest.Entries.Single();
        Assert.True(entry.Exported);
        Assert.Equal("contoso.inf", entry.OriginalInf);
        Assert.Equal(["Contoso Ethernet"], entry.DeviceNames);
        Assert.True(File.Exists(Path.Combine(result.Backup.FolderPath, BackupManager.ManifestFileName)));
        Assert.Equal(HistoryOperation.Backup, history.Entries.Single().Operation);
    }

    [Fact]
    public async Task Backup_requires_administrator()
    {
        using var temp = new TempDir();
        var runner = new FakeProcessRunner();
        var result = await CreateBackupManager(runner, false, new InMemoryHistory()).CreateBackupAsync(temp.Path, [], null, "0.1.0");

        Assert.False(result.Success);
        Assert.Contains("Administrator", result.Message);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Failed_export_is_reported_not_hidden()
    {
        using var temp = new TempDir();
        var runner = new FakeProcessRunner(_ => new ProcessResult(5, "", "Access is denied."));
        var result = await CreateBackupManager(runner, true, new InMemoryHistory()).CreateBackupAsync(temp.Path, [], null, "0.1.0");

        Assert.False(result.Success);
        Assert.False(result.Backup!.Manifest.Entries.Single().Exported);
        Assert.NotNull(result.Backup.Manifest.Entries.Single().Error);
    }

    [Fact]
    public async Task Backups_can_be_listed_loaded_and_deleted()
    {
        using var temp = new TempDir();
        var manager = CreateBackupManager(new FakeProcessRunner(FakeProcessRunner.SimulateExport), true, new InMemoryHistory());
        var created = (await manager.CreateBackupAsync(temp.Path, [Device], null, "0.1.0")).Backup!;
        Directory.CreateDirectory(Path.Combine(temp.Path, "unrelated-folder"));

        var listed = manager.ListBackups(temp.Path);
        Assert.Equal(created.FolderPath, listed.Single().FolderPath);

        manager.DeleteBackup(listed[0]);
        Assert.False(Directory.Exists(created.FolderPath));
        Assert.True(Directory.Exists(Path.Combine(temp.Path, "unrelated-folder")));
    }

    [Fact]
    public void Delete_refuses_folders_that_are_not_backups()
    {
        using var temp = new TempDir();
        var manager = CreateBackupManager(new FakeProcessRunner(), true, new InMemoryHistory());
        Assert.Throws<InvalidOperationException>(() => manager.DeleteBackup(new BackupInfo(temp.Path, new BackupManifest())));
        Assert.True(Directory.Exists(temp.Path));
    }

    [Fact]
    public async Task Restore_validates_then_installs_with_pnputil()
    {
        using var temp = new TempDir();
        var runner = new FakeProcessRunner(FakeProcessRunner.SimulateExport);
        var backup = (await CreateBackupManager(runner, true, new InMemoryHistory()).CreateBackupAsync(temp.Path, [Device], null, "0.1.0")).Backup!;

        var verifier = new FakeSignatureVerifier(trusted: true);
        var history = new InMemoryHistory();
        var restore = CreateRestoreManager(runner, verifier, history, elevated: true);

        var results = await restore.RestoreAsync(backup, backup.Manifest.Entries);

        Assert.True(results.Single().Result.Success, results.Single().Result.Message);
        Assert.Single(verifier.Verified);
        var install = runner.Calls.Last();
        Assert.Equal("/add-driver", install[0]);
        Assert.EndsWith(Path.Combine("oem7", "contoso.inf"), install[1]);
        Assert.Equal("/install", install[2]);
        Assert.Equal(HistoryOperation.Restore, history.Entries.Single().Operation);
    }

    [Fact]
    public async Task Restore_refuses_packages_that_fail_signature_validation()
    {
        using var temp = new TempDir();
        var runner = new FakeProcessRunner(FakeProcessRunner.SimulateExport);
        var backup = (await CreateBackupManager(runner, true, new InMemoryHistory()).CreateBackupAsync(temp.Path, [Device], null, "0.1.0")).Backup!;
        var callsBefore = runner.Calls.Count;

        var history = new InMemoryHistory();
        var results = await CreateRestoreManager(runner, new FakeSignatureVerifier(trusted: false), history, true)
            .RestoreAsync(backup, backup.Manifest.Entries);

        Assert.False(results.Single().Result.Success);
        Assert.StartsWith(DriverValidator.SignatureFailedMessage, results.Single().Result.Message);
        Assert.Equal(callsBefore, runner.Calls.Count); // pnputil was never called
        Assert.Equal(HistoryResult.Failed, history.Entries.Single().Result);
    }

    [Fact]
    public void Restore_rejects_manifest_paths_that_escape_the_backup()
    {
        var backup = new BackupInfo(@"C:\Backups\B1", new BackupManifest());
        var evil = new BackupEntry { Exported = true, Folder = @"..\..\Windows", OriginalInf = "x.inf" };
        Assert.Null(RestoreManager.ResolveInfPath(backup, evil));
    }

    private static RestoreManager CreateRestoreManager(FakeProcessRunner runner, FakeSignatureVerifier verifier, InMemoryHistory history, bool elevated)
    {
        var validator = new DriverValidator(verifier);
        var installer = new DriverInstaller(validator, new PnpUtilClient(runner), new FakeElevation(elevated), NullLog.Instance);
        return new RestoreManager(validator, installer, history, NullLog.Instance);
    }
}
