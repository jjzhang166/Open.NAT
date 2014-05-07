//
// Authors:
//   Ben Motmans <ben.motmans@gmail.com>
//   Lucas Ontivero lucasontivero@gmail.com
//
// Copyright (C) 2007 Ben Motmans
// Copyright (C) 2014 Lucas Ontivero
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mono.Nat
{
    internal class PmpSearcher : Searcher
    {
        private readonly IIPAddressesProvider _ipprovider;
        private Dictionary<UdpClient, IEnumerable<IPEndPoint>> _gatewayLists;
        private int _timeout;

        internal PmpSearcher(IIPAddressesProvider ipprovider)
        {
            _ipprovider = ipprovider;
            _timeout = 250;
            CreateSocketsAndAddGateways();
        }

		private void CreateSocketsAndAddGateways()
		{
            Sockets = new List<UdpClient>();
            _gatewayLists = new Dictionary<UdpClient, IEnumerable<IPEndPoint>>();

            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up && n.OperationalStatus != OperationalStatus.Unknown)
                        continue;

                    var properties = n.GetIPProperties();
                    var gatewayList = new List<IPEndPoint>();

                    foreach (var gateway in properties.GatewayAddresses)
                    {
                        if (gateway.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        gatewayList.Add(new IPEndPoint(gateway.Address, PmpConstants.ServerPort));
                    }

                    if (gatewayList.Count == 0)
                    {
                        /* Mono on OSX doesn't give any gateway addresses, so check DNS entries */
                        foreach (var gateway in properties.DnsAddresses)
                        {
                            if (gateway.AddressFamily != AddressFamily.InterNetwork) continue;
                            gatewayList.Add(new IPEndPoint(gateway, PmpConstants.ServerPort));
                        }
                    }

                    if (gatewayList.Count == 0) continue;

                    foreach (var address in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                        UdpClient client;

                        try
                        {
                            client = new UdpClient(new IPEndPoint(address.Address, 0));
                        }
                        catch (SocketException)
                        {
                            continue; // Move on to the next address.
                        }

                        _gatewayLists.Add(client, gatewayList); 
                        Sockets.Add(client);
                    }
                }
            }
            catch (Exception)
            {
                // NAT-PMP does not use multicast, so there isn't really a good fallback.
            }
		}

		protected override void Search (UdpClient client)
        {
            // Sort out the time for the next search first. The spec says the 
            // timeout should double after each attempt. Once it reaches 64 seconds
            // (and that attempt fails), assume no devices available
            NextSearch = DateTime.Now.AddMilliseconds(_timeout);
            _timeout *= 2;

            // We've tried 9 times as per spec, try searching again in 5 minutes
            if (_timeout == 128 * 1000)
            {
                _timeout = 250;
                NextSearch = DateTime.Now.AddMinutes(10);
                return;
            }

            // The nat-pmp search message. Must be sent to GatewayIP:53531
            var buffer = new[] { PmpConstants.Version, PmpConstants.OperationExternalAddressRequest };
            foreach (var gatewayEndpoint in _gatewayLists[client])
                client.Send(buffer, buffer.Length, gatewayEndpoint);
        }

        private bool IsSearchAddress(IPAddress address)
        {
            return _gatewayLists.Values.SelectMany(x => x)
                .Any(x => x.Address.Equals(address));
        }

        public override void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint)
        {
            if (!IsSearchAddress(endpoint.Address)
                || response.Length != 12 
                || response[0] != PmpConstants.Version
                || response[1] != PmpConstants.ServerNoop)
                return;

            int errorcode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(response, 2));
            if (errorcode != 0)
                NatUtility.Log("Non zero error: {0}", errorcode);

            var publicIp = new IPAddress(new[] { response[8], response[9], response[10], response[11] });
            NextSearch = DateTime.Now.AddMinutes(5);

            _timeout = 250;
            OnDeviceFound(new DeviceEventArgs(new PmpNatDevice(endpoint.Address, publicIp)));
        }
    }
}
