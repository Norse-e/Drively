using Drively.Core.Utilities;

namespace Drively.Core.Tests;

public class DriverVersionTests
{
    [Theory]
    [InlineData("31.0.101.4502", "31.0.101.4091", 1)]
    [InlineData("10.0.26100.1", "10.0.26100.1", 0)]
    [InlineData("1.2", "1.2.0.0", 0)]
    [InlineData("1.10.0.0", "1.9.0.0", 1)]          // numeric, not lexical
    [InlineData("6.0.9500.1", "6.0.10000.1", -1)]
    [InlineData("10.1.14.7", "10.0.26100.1150", 1)]
    public void Compare_orders_versions_numerically(string left, string right, int expected)
    {
        Assert.Equal(expected, Math.Sign(DriverVersion.Compare(left, right)!.Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1..2")]
    [InlineData("-1.0")]
    [InlineData("99999999999.0")]
    public void TryParse_rejects_invalid_versions(string? text)
    {
        Assert.False(DriverVersion.TryParse(text, out _));
    }

    [Fact]
    public void Compare_returns_null_when_either_side_is_unknown()
    {
        Assert.Null(DriverVersion.Compare("1.0", null));
        Assert.Null(DriverVersion.Compare("n/a", "1.0"));
    }

    [Theory]
    [InlineData("INTEL - System - 7/18/1968 12:00:00 AM - 10.1.14.7", "10.1.14.7")]
    [InlineData("Microsoft Corporation AudioProcessingObject Driver Update (1.0.4.55794)", "1.0.4.55794")]
    [InlineData("NVIDIA - Display - 32.0.15.6094", "32.0.15.6094")]
    public void ExtractTrailingVersion_reads_versions_from_windows_update_titles(string title, string expected)
    {
        Assert.Equal(expected, DriverVersion.ExtractTrailingVersion(title));
    }

    [Theory]
    [InlineData("Intel - Other hardware - Intel(R) Xeon(R) E3 - 1200/1500 v5/6th Gen Intel(R) Core(TM) PCIe Controller (x16) - 1901")]
    [InlineData("Realtek Audio Driver")]
    [InlineData("")]
    public void ExtractTrailingVersion_never_guesses(string title)
    {
        Assert.Null(DriverVersion.ExtractTrailingVersion(title));
    }
}
