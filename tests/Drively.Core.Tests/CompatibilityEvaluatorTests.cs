using Drively.Core.Models;
using Drively.Core.Services;

namespace Drively.Core.Tests;

public class CompatibilityEvaluatorTests
{
    private static DeviceInfo Device(string hwid, string? version, string[]? compatible = null) => new()
    {
        InstanceId = hwid + @"\1",
        Name = "Test device " + hwid,
        HardwareIds = [hwid + "&SUBSYS_00001234&REV_01", hwid],
        CompatibleIds = compatible ?? [],
        Driver = version is null ? null : new DriverDetails { Provider = "Contoso", Version = version, InfName = "oem1.inf" },
    };

    private static UpdateCandidate Candidate(string hwid, string? version) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Title = "Contoso update",
        HardwareId = hwid,
        Version = version,
        Source = "Test",
    };

    [Fact]
    public void Accepts_newer_version_for_matching_hardware_id_case_insensitively()
    {
        var devices = new[] { Device(@"PCI\VEN_FFFF&DEV_0001", "1.0.0.0") };
        var decision = CompatibilityEvaluator.Evaluate(Candidate(@"pci\ven_ffff&dev_0001", "1.1.0.0"), devices);

        Assert.True(decision.IsCompatible);
        Assert.Same(devices[0], decision.Device);
        Assert.Equal(MatchKind.HardwareId, decision.MatchKind);
    }

    [Theory]
    [InlineData("1.0.0.0")]
    [InlineData("0.9.0.0")]
    public void Rejects_same_or_older_version(string offered)
    {
        var devices = new[] { Device(@"PCI\VEN_FFFF&DEV_0001", "1.0.0.0") };
        Assert.False(CompatibilityEvaluator.Evaluate(Candidate(@"PCI\VEN_FFFF&DEV_0001", offered), devices).IsCompatible);
    }

    [Fact]
    public void Rejects_update_for_hardware_not_present()
    {
        var devices = new[] { Device(@"PCI\VEN_FFFF&DEV_0001", "1.0.0.0") };
        var decision = CompatibilityEvaluator.Evaluate(Candidate(@"PCI\VEN_FFFF&DEV_0002", "9.0.0.0"), devices);
        Assert.False(decision.IsCompatible);
        Assert.Contains("no device", decision.Reason);
    }

    [Fact]
    public void Rejects_update_without_hardware_id()
    {
        Assert.False(CompatibilityEvaluator.Evaluate(Candidate("", "1.0"), [Device(@"PCI\VEN_FFFF&DEV_0001", null)]).IsCompatible);
    }

    [Fact]
    public void Accepts_update_for_device_with_missing_driver()
    {
        var devices = new[] { Device(@"PCI\VEN_FFFF&DEV_0003", null) };
        Assert.True(CompatibilityEvaluator.Evaluate(Candidate(@"PCI\VEN_FFFF&DEV_0003", "1.0.0.0"), devices).IsCompatible);
    }

    [Fact]
    public void Prefers_hardware_id_match_over_compatible_id_match()
    {
        var generic = Device(@"PCI\VEN_AAAA&DEV_0001", "1.0", compatible: [@"PCI\CC_0C0330"]);
        var exact = Device(@"PCI\CC_0C0330", "1.0");
        var decision = CompatibilityEvaluator.Evaluate(Candidate(@"PCI\CC_0C0330", "2.0"), [generic, exact]);

        Assert.Same(exact, decision.Device);
        Assert.Equal(MatchKind.HardwareId, decision.MatchKind);
    }

    [Fact]
    public void Unknown_offered_version_is_not_guessed_and_relies_on_hardware_match()
    {
        var devices = new[] { Device(@"PCI\VEN_FFFF&DEV_0001", "1.0.0.0") };
        var decision = CompatibilityEvaluator.Evaluate(Candidate(@"PCI\VEN_FFFF&DEV_0001", null), devices);
        Assert.True(decision.IsCompatible);
    }

    [Fact]
    public void Driver_date_is_never_used_to_decide()
    {
        // Older date but higher version: still an update. (Intel chipset INFs use 1968 dates on purpose.)
        var devices = new[] { Device(@"PCI\VEN_8086&DEV_3EC2", "10.0.26100.1150") };
        var candidate = new UpdateCandidate
        {
            Id = "x", Title = "INTEL - System - 10.1.14.7", HardwareId = @"PCI\VEN_8086&DEV_3EC2",
            Version = "10.1.14.7", DriverDate = new DateTime(1968, 7, 18), Source = "Test",
        };
        Assert.True(CompatibilityEvaluator.Evaluate(candidate, devices).IsCompatible);
    }
}
