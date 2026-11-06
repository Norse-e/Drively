using System.Net.NetworkInformation;
using Drively.Core.Interfaces;

namespace Drively.Core.Utilities;

/// <summary>Checks local adapters only; does not send any network request.</summary>
public sealed class NetworkStatus : INetworkStatus
{
    public bool IsNetworkAvailable
    {
        get
        {
            try
            {
                return NetworkInterface.GetIsNetworkAvailable();
            }
            catch (NetworkInformationException)
            {
                return true; // Unknown: let the update source report the real error.
            }
        }
    }
}
