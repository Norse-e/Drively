namespace Drively.Core.Utilities;

/// <summary>Plain-language descriptions of Windows Device Manager problem codes.</summary>
public static class DeviceProblemCodes
{
    private static readonly Dictionary<int, string> Descriptions = new()
    {
        [0] = "This device is working properly.",
        [1] = "This device is not configured correctly.",
        [3] = "The driver for this device might be corrupted, or the system may be low on memory.",
        [10] = "This device cannot start.",
        [12] = "This device cannot find enough free resources.",
        [14] = "This device cannot work properly until you restart your computer.",
        [16] = "Windows cannot identify all the resources this device uses.",
        [18] = "The drivers for this device need to be reinstalled.",
        [19] = "Windows cannot start this device because its configuration information is incomplete or damaged.",
        [21] = "Windows is removing this device.",
        [22] = "This device is disabled.",
        [24] = "This device is not present, is not working properly, or does not have all its drivers installed.",
        [28] = "The drivers for this device are not installed.",
        [29] = "This device is disabled because its firmware did not provide the required resources.",
        [31] = "This device is not working properly because Windows cannot load the drivers it requires.",
        [32] = "A driver (service) for this device has been disabled.",
        [33] = "Windows cannot determine which resources are required for this device.",
        [34] = "Windows cannot determine the settings for this device.",
        [35] = "The system firmware does not include enough information to configure this device.",
        [37] = "Windows cannot initialize the device driver for this hardware.",
        [38] = "Windows cannot load the driver because a previous instance is still in memory.",
        [39] = "Windows cannot load the device driver. The driver may be corrupted or missing.",
        [40] = "Windows cannot access this hardware because its registry service key information is missing or incorrect.",
        [41] = "Windows loaded the device driver but cannot find the hardware device.",
        [42] = "Windows cannot load the driver because a duplicate device is already running.",
        [43] = "Windows has stopped this device because it has reported problems.",
        [44] = "An application or service has shut down this hardware device.",
        [45] = "This hardware device is not currently connected to the computer.",
        [46] = "Windows cannot access this device because the operating system is shutting down.",
        [47] = "This device has been prepared for safe removal.",
        [48] = "The driver for this device has been blocked from starting because it is known to have problems with Windows.",
        [49] = "Windows cannot start new hardware devices because the system hive is too large.",
        [50] = "Windows cannot apply all of the properties for this device.",
        [51] = "This device is waiting on another device to start.",
        [52] = "Windows cannot verify the digital signature for the drivers required for this device.",
        [53] = "This device has been reserved for use by the Windows kernel debugger.",
        [54] = "This device has failed and is undergoing a reset.",
    };

    public static string Describe(int code) =>
        Descriptions.TryGetValue(code, out var text) ? text : $"Windows reported problem code {code} for this device.";
}
