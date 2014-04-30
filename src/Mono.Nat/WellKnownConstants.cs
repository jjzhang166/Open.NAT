using System.Net;

namespace Mono.Nat
{
    static class WellKnownConstants
    {
        public static IPAddress IPv4MulticastAddress = IPAddress.Parse("239.255.255.250");
        public static IPEndPoint NatPmpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.1"), 5351);
    }
}
