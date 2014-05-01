using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Mono.Nat
{
    internal abstract class Searcher : ISearcher
    {
        public event EventHandler<DeviceEventArgs> DeviceFound;
        protected List<UdpClient> Sockets;

        public void Receive()
        {
            var received = WellKnownConstants.NatPmpEndPoint;
            foreach (var client in Sockets.Where(c => c.Available > 0))
            {
                var localAddress = ((IPEndPoint)client.Client.LocalEndPoint).Address;
                var data = client.Receive(ref received);
                Handle(localAddress, data, received);
            }
        }

        public void Search()
        {
            NatUtility.Log("Searching for: {0}", GetType().Name);

            foreach (var socket in Sockets)
            {
                try
                {
                    Search(socket);
                }
                catch (Exception)
                {
                    continue; // Ignore any search errors
                }
            }
        }

        protected void OnDeviceFound(DeviceEventArgs args)
        {
            var handler = DeviceFound;
            if (handler != null)
                handler(this, args);
        }

        protected abstract void Search(UdpClient client);

        public abstract void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint);

        protected DateTime NextSearch { get; set; }

        public bool IsSearchTime
        {
            get { return NextSearch < DateTime.Now; }
        }
    }
}