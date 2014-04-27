using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Mono.Nat.Pmp;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mono.Nat
{
    internal class PmpSearcher : ISearcher
    {
		internal static readonly PmpSearcher Instance = new PmpSearcher();
        public static List<UdpClient> Sockets;
        static Dictionary<UdpClient, List<IPEndPoint>> _gatewayLists;
        private int _timeout;
        private DateTime _nextSearch;
        public event EventHandler<DeviceEventArgs> DeviceFound;
        public event EventHandler<DeviceEventArgs> DeviceLost;

        static PmpSearcher()
        {
            CreateSocketsAndAddGateways();
        }

        PmpSearcher()
        {
            _timeout = 250;
        }

		private static void CreateSocketsAndAddGateways()
		{
            Sockets = new List<UdpClient>();
            _gatewayLists = new Dictionary<UdpClient,List<IPEndPoint>>();

            try
            {
                foreach (NetworkInterface n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    IPInterfaceProperties properties = n.GetIPProperties();
                    List<IPEndPoint> gatewayList = new List<IPEndPoint>();

                    foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                    {
                        if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            gatewayList.Add(new IPEndPoint(gateway.Address, PmpConstants.ServerPort));
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

        public void Search()
		{
			foreach (UdpClient s in Sockets)
			{
				try
				{
					Search(s);
				}
				catch
				{
					// Ignore any search errors
				}
			}
		}

		void Search (UdpClient client)
        {
            // Sort out the time for the next search first. The spec says the 
            // timeout should double after each attempt. Once it reaches 64 seconds
            // (and that attempt fails), assume no devices available
            _nextSearch = DateTime.Now.AddMilliseconds(_timeout);
            _timeout *= 2;

            // We've tried 9 times as per spec, try searching again in 5 minutes
            if (_timeout == 128 * 1000)
            {
                _timeout = 250;
                _nextSearch = DateTime.Now.AddMinutes(10);
                return;
            }

            // The nat-pmp search message. Must be sent to GatewayIP:53531
            byte[] buffer = new byte[] { PmpConstants.Version, PmpConstants.OperationCode };
            foreach (IPEndPoint gatewayEndpoint in _gatewayLists[client])
                client.Send(buffer, buffer.Length, gatewayEndpoint);
        }

        bool IsSearchAddress(IPAddress address)
        {
            foreach (List<IPEndPoint> gatewayList in _gatewayLists.Values)
                foreach (IPEndPoint gatewayEndpoint in gatewayList)
                    if (gatewayEndpoint.Address.Equals(address))
                        return true;
            return false;
        }

        public void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint)
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

            IPAddress publicIp = new IPAddress(new byte[] { response[8], response[9], response[10], response[11] });
            _nextSearch = DateTime.Now.AddMinutes(5);
            _timeout = 250;
            OnDeviceFound(new DeviceEventArgs(new PmpNatDevice(endpoint.Address, publicIp)));
        }

        public DateTime NextSearch
        {
            get { return _nextSearch; }
        }
        private void OnDeviceFound(DeviceEventArgs args)
        {
            if (DeviceFound != null)
                DeviceFound(this, args);
        }
    }
}
