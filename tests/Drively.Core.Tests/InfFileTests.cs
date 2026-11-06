using Drively.Core.Utilities;

namespace Drively.Core.Tests;

public class InfFileTests
{
    private const string SampleInf = """
        ; Sample driver INF
        [Version]
        Signature   = "$WINDOWS NT$"
        Class       = Net
        ClassGuid   = {4d36e972-e325-11ce-bfc1-08002be10318}
        Provider    = %ManufacturerName%   ; resolved from [Strings]
        CatalogFile.NTamd64 = contoso_x64.cat
        CatalogFile = contoso.cat
        DriverVer   = 08/15/2024,10.68.815.2024

        [Manufacturer]
        %ManufacturerName% = Standard,NTamd64

        [Strings]
        ManufacturerName = "Contoso; Ltd."
        """;

    [Fact]
    public void Parses_version_section_and_resolves_strings()
    {
        var inf = InfFile.Parse(SampleInf);

        Assert.True(inf.HasValidSignatureDirective);
        Assert.Equal("Net", inf.Class);
        Assert.Equal("Contoso; Ltd.", inf.Provider);   // ';' inside quotes is not a comment
        Assert.Equal("contoso_x64.cat", inf.CatalogFile); // x64-specific catalog wins
    }

    [Fact]
    public void Parses_driver_date_and_version()
    {
        var (date, version) = InfFile.Parse(SampleInf).DriverVer;
        Assert.Equal(new DateTime(2024, 8, 15), date);
        Assert.Equal("10.68.815.2024", version);
    }

    [Fact]
    public void Missing_catalog_is_reported_as_null()
    {
        var inf = InfFile.Parse("[Version]\nSignature=\"$Windows NT$\"\nClass=System\n");
        Assert.Null(inf.CatalogFile);
    }

    [Fact]
    public void Non_driver_file_has_no_valid_signature_directive()
    {
        Assert.False(InfFile.Parse("[Settings]\nfoo=bar\n").HasValidSignatureDirective);
    }

    [Fact]
    public void Loads_utf16_files()
    {
        var path = Path.Combine(Path.GetTempPath(), $"drively-test-{Guid.NewGuid():N}.inf");
        try
        {
            File.WriteAllText(path, SampleInf, System.Text.Encoding.Unicode);
            Assert.Equal("Contoso; Ltd.", InfFile.Load(path).Provider);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
