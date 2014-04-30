using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mono.Nat
{
    internal class PmpSearcher : Searcher
    {
        static Dictionary<UdpClient, List<IPEndPoint>> _gatewayLists;
        private int _timeout;

        internal PmpSearcher()
        {
            _timeout = 250;
            CreateSocketsAndAddGateways();
        }

		private void CreateSocketsAndAddGateways()
		{
            Sockets = new List<UdpClient>();
            _gatewayLists = new Dictionary<UdpClient,List<IPEndPoint>>();

            try
            {
                foreach (NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up && n.OperationalStatus != OperationalStatus.Unknown)
                        continue;

                    var properties = n.GetIPProperties();
                    var gatewayList = new List<IPEndPoint>();

                    foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                    {
                        if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            gatewayList.Add(new IPEndPoint(gateway.Address, PmpConstants.ServerPort));
                        }
                    }

                    if (gatewayList.Count == 0)
                    {
                        /* Mono on OSX doesn't give any gateway addresses, so check DNS entries */
                        foreach (var gateway in properties.DnsAddresses)
                        {
                            if (gateway.AddressFamily == AddressFamily.InterNetwork)
                            {
                                gatewayList.Add(new IPEndPoint(gateway, PmpConstants.ServerPort));
                            }
                        }
                    }

                    if (gatewayList.Count > 0)
                    {
                        foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                        {
                            if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                UdpClient client;

                                try
                                {
                                    client = new UdpClient(new IPEndPoint(address.Address, 0));
                                }
                                catch (SocketException)
                                {
                                    continue; // Move on to the next address.
                                }

                                _gatewayLists.Add(client, gatewayList); Sockets.Add(client);
                            }
                        }
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
            var buffer = new[] { PmpConstants.Version, PmpConstants.OperationCode };
            foreach (var gatewayEndpoint in _gatewayLists[client])
                client.Send(buffer, buffer.Length, gatewayEndpoint);
        }

        private static bool IsSearchAddress(IPAddress address)
        {
            foreach (List<IPEndPoint> gatewayList in _gatewayLists.Values)
                foreach (IPEndPoint gatewayEndpoint in gatewayList)
                    if (gatewayEndpoint.Address.Equals(address))
                        return true;
            return false;
        }

        public override void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint)
        {
            if (!IsSearchAddress(endpoint.Address))
                return;
            if (response.Length != 12)
                return;
            if (response[0] != PmpConstants.Version)
                return;
            if (response[1] != PmpConstants.ServerNoop)
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
