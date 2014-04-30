using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mono.Nat
{
    public class IPAddressesProvider : IIPAddressesProvider
    {
        public IEnumerable<IPAddress> GetIPAddresses()
        {
            return from networkInterface in NetworkInterface.GetAllNetworkInterfaces()
                      where networkInterface.OperationalStatus == OperationalStatus.Up || networkInterface.OperationalStatus == OperationalStatus.Unknown
                      from addressInfo in networkInterface.GetIPProperties().UnicastAddresses
                      where addressInfo.Address.AddressFamily == AddressFamily.InterNetwork
                      select addressInfo.Address;

        }
    }
}
