using System;
using System.Net;

namespace Mono.Nat
{
    internal interface ISearcher
    {
        event EventHandler<DeviceEventArgs> DeviceFound;

        void Search();
        void Receive();
        void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint);
    }
}
