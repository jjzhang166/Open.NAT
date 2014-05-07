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
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Net.Sockets;

namespace Mono.Nat
{
    internal class UpnpSearcher : Searcher
    {
        private readonly IIPAddressesProvider _ipprovider;
        private readonly List<NatDevice> _devices;
		private readonly Dictionary<IPAddress, DateTime> _lastFetched;

        internal UpnpSearcher(IIPAddressesProvider ipprovider)
        {
            _ipprovider = ipprovider;
            Sockets = CreateSockets();
            _devices = new List<NatDevice>();
			_lastFetched = new Dictionary<IPAddress, DateTime>();
        }

		private List<UdpClient> CreateSockets()
		{
			var clients = new List<UdpClient>();
			try
			{
                var ips = _ipprovider.UnicastAddresses();

                foreach (var ipAddress in ips)
				{
					try
					{
                        clients.Add(new UdpClient(new IPEndPoint(ipAddress, 0)));
					}
					catch (Exception)
					{
					    continue; // Move on to the next address.
					}
				}
			}
			catch (Exception)
			{
				clients.Add(new UdpClient(0));
			}
			return clients;
		}

        protected override void Search(UdpClient client)
        {
            NextSearch = DateTime.Now.AddMinutes(5);

            var data = DiscoverDeviceMessage.Encode();
            var searchEndpoint = new IPEndPoint(IPAddress.Broadcast, 1900);

            // UDP is unreliable, so send 3 requests at a time (per Upnp spec, sec 1.1.2)
            for (var i = 0; i < 3; i++)
            {
                client.Send(data, data.Length, searchEndpoint);
            }
        }

        public override void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint)
        {
            // Convert it to a string for easy parsing
            string dataString = null;

            // No matter what, this method should never throw an exception. If something goes wrong
            // we should still be in a position to handle the next reply correctly.
            try
            {
                dataString = Encoding.UTF8.GetString(response);

				if (NatUtility.Verbose)
					NatUtility.Log("UPnP Response: {0}", dataString);

                // If this device does not have a WANIPConnection service, then ignore it
                // Technically i should be checking for WANIPConnection:1 and InternetGatewayDevice:1
                // but there are some routers missing the '1'.
                var serviceNames = new[] {"WANIPConnection", "InternetGatewayDevice", "WANPPPConnection"};
                var service = (from serviceName in serviceNames
                                let serviceUrn = string.Format("urn:schemas-upnp-org:service:{0}:1", serviceName)
                                where dataString.ContainsIgnoreCase(serviceUrn)
                                select new {ServiceName = serviceName, ServiceUrn = serviceUrn}).FirstOrDefault();

                if (service == null) return;
                NatUtility.Log("UPnP Response: Router advertised a '{0}' service", service.ServiceName);

                // We have an internet gateway device now
                var device = new UpnpNatDevice(localAddress, dataString, service.ServiceUrn);

                if (_devices.Contains(device))
                {
                    // We already have found this device, so we just refresh it to let people know it's
                    // Still alive. If a device doesn't respond to a search, we dump it.
                    _devices[_devices.IndexOf(device)].Touch();
                    return;
                }
				// If we send 3 requests at a time, ensure we only fetch the services list once
				// even if three responses are received
				if (_lastFetched.ContainsKey(endpoint.Address))
				{
					var last = _lastFetched[endpoint.Address];
					if ((DateTime.Now - last) < TimeSpan.FromSeconds(20))
						return;
				}
				_lastFetched[endpoint.Address] = DateTime.Now;
					
                // Once we've parsed the information we need, we tell the device to retrieve it's service list
                // Once we successfully receive the service list, the callback provided will be invoked.
				NatUtility.Log("Fetching service list: {0}", device.DeviceInfo.HostEndPoint);
                device.GetServicesList();
                DeviceSetupComplete(device);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Unhandled exception when trying to decode a device's response Send me the following data: ");
                Trace.WriteLine("ErrorMessage:");
                Trace.WriteLine(ex.Message);
                Trace.WriteLine("Data string:");
                Trace.WriteLine(dataString);
            }
        }


        private void DeviceSetupComplete(NatDevice device)
        {
            lock (_devices)
            {
                // We don't want the same device in there twice
                if (_devices.Contains(device))
                    return;

                _devices.Add(device);
            }

            OnDeviceFound(new DeviceEventArgs(device));
        }
    }

    static class StringExtensions
    {
        internal static bool ContainsIgnoreCase(this string s, string pattern)
        {
            return s.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
