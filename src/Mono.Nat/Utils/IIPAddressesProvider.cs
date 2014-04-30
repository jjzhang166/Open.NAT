using System.Collections.Generic;
using System.Net;

namespace Mono.Nat
{
    interface IIPAddressesProvider
    {
        IEnumerable<IPAddress> GetIPAddresses();
    }
}
