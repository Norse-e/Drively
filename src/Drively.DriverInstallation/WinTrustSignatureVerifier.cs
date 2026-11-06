using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Drively.Core.Interfaces;
using Drively.Core.Models;

namespace Drively.DriverInstallation;

/// <summary>
/// Verifies Authenticode signatures (including driver catalog .cat files) with the Windows
/// WinVerifyTrust API. Revocation is checked from the local cache only, so verification never
/// triggers network requests. Windows performs its own full driver signature enforcement at install time.
/// </summary>
public sealed class WinTrustSignatureVerifier : ISignatureVerifier
{
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    public SignatureCheckResult Verify(string filePath)
    {
        if (!File.Exists(filePath)) return SignatureCheckResult.Untrusted("The file to verify does not exist.");

        var signer = TryGetSigner(filePath);
        var hr = WinVerifyTrustFile(filePath);
        return hr == 0
            ? SignatureCheckResult.Trusted(signer)
            : SignatureCheckResult.Untrusted(DescribeTrustError(hr), signer);
    }

    public static string DescribeTrustError(int hr) => unchecked((uint)hr) switch
    {
        0x800B0100 => "The file is not digitally signed.",
        0x800B0109 => "The signature chains to a root certificate that is not trusted.",
        0x800B0101 => "The signing certificate has expired and the signature has no valid timestamp.",
        0x800B010C => "The signing certificate has been revoked.",
        0x800B0111 => "The signer is explicitly distrusted on this PC.",
        0x80096010 => "The file was modified after it was signed (digest mismatch).",
        0x800B0003 => "The file format is not supported for signature verification.",
        0x80092003 => "The file could not be read for signature verification.",
        var other => $"Signature verification failed (0x{other:X8}).",
    };

    private static string? TryGetSigner(string filePath)
    {
        try
        {
            var cms = new SignedCms();
            cms.Decode(File.ReadAllBytes(filePath));
            var cert = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;
            return cert?.GetNameInfo(X509NameType.SimpleName, false);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return null; // Not a PKCS#7 file (e.g. an embedded-signature PE); trust result is still authoritative.
        }
    }

    private static int WinVerifyTrustFile(string filePath)
    {
        var fileInfo = new WINTRUST_FILE_INFO
        {
            cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
            pcwszFilePath = filePath,
        };
        var fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
        var dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_DATA>());
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = fileInfoPtr,
                dwStateAction = WTD_STATEACTION_VERIFY,
                dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL,
            };
            Marshal.StructureToPtr(data, dataPtr, false);

            var result = WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, dataPtr);

            // Release the state data allocated by the verify call.
            data = Marshal.PtrToStructure<WINTRUST_DATA>(dataPtr);
            data.dwStateAction = WTD_STATEACTION_CLOSE;
            Marshal.StructureToPtr(data, dataPtr, true);
            _ = WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, dataPtr);

            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(dataPtr);
            Marshal.DestroyStructure<WINTRUST_FILE_INFO>(fileInfoPtr);
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }

    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;
    private const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, IntPtr pWVTData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }
}
