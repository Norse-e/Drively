using Drively.DriverInstallation;

namespace Drively.Integration.Tests;

public class SignatureAndValidationTests
{
    private static string? FindMicrosoftCatalog()
    {
        var catRoot = Path.Combine(Environment.SystemDirectory, "CatRoot", "{F750E6C3-38EE-11D1-85E5-00C04FC295EE}");
        return Directory.Exists(catRoot) ? Directory.EnumerateFiles(catRoot, "*.cat").FirstOrDefault() : null;
    }

    [Fact]
    public void Windows_catalog_signature_is_trusted()
    {
        var catalog = FindMicrosoftCatalog();
        Assert.NotNull(catalog);

        var result = new WinTrustSignatureVerifier().Verify(catalog);

        Assert.True(result.IsTrusted, result.Message);
        Assert.Contains("Microsoft", result.Signer);
    }

    [Fact]
    public void Unsigned_file_is_not_trusted()
    {
        using var temp = new TempDir();
        var file = Path.Combine(temp.Path, "fake.cat");
        File.WriteAllText(file, "not a catalog");

        var result = new WinTrustSignatureVerifier().Verify(file);

        Assert.False(result.IsTrusted);
    }

    [Fact]
    public void Package_with_trusted_catalog_passes_validation()
    {
        using var temp = new TempDir();
        File.Copy(FindMicrosoftCatalog()!, Path.Combine(temp.Path, "pkg.cat"));
        var inf = Path.Combine(temp.Path, "pkg.inf");
        File.WriteAllText(inf, "[Version]\nSignature=\"$Windows NT$\"\nClass=System\nProvider=Test\nCatalogFile=pkg.cat\nDriverVer=01/01/2024,1.0.0.0\n");

        var result = new DriverValidator(new WinTrustSignatureVerifier()).Validate(inf);

        Assert.True(result.IsValid, string.Join(" ", result.Errors));
        Assert.True(result.Signature!.IsTrusted);
    }

    [Fact]
    public void Package_with_unsigned_catalog_fails_validation()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "pkg.cat"), "tampered");
        var inf = Path.Combine(temp.Path, "pkg.inf");
        File.WriteAllText(inf, "[Version]\nSignature=\"$Windows NT$\"\nCatalogFile=pkg.cat\n");

        var result = new DriverValidator(new WinTrustSignatureVerifier()).Validate(inf);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.StartsWith(DriverValidator.SignatureFailedMessage));
    }

    [Theory]
    [InlineData("[Version]\nSignature=\"$Windows NT$\"\n", "unsigned")]
    [InlineData("[Version]\nSignature=\"$Windows NT$\"\nCatalogFile=..\\outside.cat\n", "outside")]
    [InlineData("[Version]\nSignature=\"$Windows NT$\"\nCatalogFile=missing.cat\n", "missing")]
    [InlineData("[Strings]\nA=B\n", "not a valid")]
    public void Invalid_packages_are_rejected_with_reason(string content, string expectedFragment)
    {
        using var temp = new TempDir();
        var inf = Path.Combine(temp.Path, "pkg.inf");
        File.WriteAllText(inf, content);

        var result = new DriverValidator(new FakeSignatureVerifier(trusted: true)).Validate(inf);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
    }
}
