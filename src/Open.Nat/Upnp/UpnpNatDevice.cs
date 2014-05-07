//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//   Ben Motmans <ben.motmans@gmail.com>
//   Lucas Ontivero lucasontivero@gmail.com 
//
// Copyright (C) 2006 Alan McGovern
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Open.Nat
{
    internal sealed class UpnpNatDevice : NatDevice, IEquatable<UpnpNatDevice>
	{
	    internal readonly UpnpNatDeviceInfo DeviceInfo;
        private readonly UpnpServiceProxy _proxy;

		internal UpnpNatDevice (IPAddress localAddress, string deviceDetails, string serviceType)
		{
            Touch();

            const string locationKey = "Location:";

            var start = deviceDetails.IndexOf(locationKey, StringComparison.InvariantCultureIgnoreCase) + locationKey.Length;
            var end = deviceDetails.IndexOf("\n", start, StringComparison.InvariantCultureIgnoreCase);
            var locationDetails = deviceDetails.Substring(start, end - start).Trim();

            DeviceInfo = new UpnpNatDeviceInfo(localAddress, locationDetails, serviceType);
            _proxy = new UpnpServiceProxy(DeviceInfo);
		}


		public override async Task<IPAddress> GetExternalIPAsync()
	    {
	        return await _proxy.GetExternalIPAsync();
		}

        public override async Task CreatePortMapAsync(Mapping mapping)
		{
		    await _proxy.CreatePortMapAsync(mapping);
		}

		public override async Task DeletePortMapAsync(Mapping mapping)
		{
		    await _proxy.DeletePortMapAsync(mapping);
        }

		public override async Task<Mapping[]> GetAllMappingsAsync()
		{
            try
            {
                return await _proxy.GetAllMappingsAsync();
            }
            catch (WebException ex)
            {
                // Even if the request "failed" i want to continue on to read out the response from the router
                var httpresponse = ex.Response as HttpWebResponse;
                if (httpresponse == null && (int)ex.Status != 713)
                    throw;

                return Enumerable.Empty<Mapping>().ToArray();
            }
        }

		public override async Task<Mapping> GetSpecificMappingAsync (Protocol protocol, int port)
		{
		    return await _proxy.GetSpecificMappingAsync(protocol, port);
        }

		public override bool Equals(object obj)
		{
			var device = obj as UpnpNatDevice;
			return (device != null) && Equals(device);
		}

		public bool Equals(UpnpNatDevice other)
		{
			return (other != null) 
                && (DeviceInfo.HostEndPoint.Equals(other.DeviceInfo.HostEndPoint)
                && DeviceInfo.ServiceDescriptionPart == other.DeviceInfo.ServiceDescriptionPart);
		}

		public override int GetHashCode()
		{
            return (DeviceInfo.HostEndPoint.GetHashCode()
                ^ DeviceInfo.ServiceControlPart.GetHashCode()
                ^ DeviceInfo.ServiceDescriptionPart.GetHashCode());
		}


        internal void GetServicesList()
        {
            var task = Task.Run(async () => { await _proxy.GetServicesListAsync(); });
            task.Wait();
        }

        public override string ToString( )
        {
            //GetExternalIP is blocking and can throw exceptions, can't use it here.
            return String.Format( 
                "UpnpNatDevice - EndPoint: {0}, External IP: {1}, Control Url: {2}, Service Description Url: {3}, Service Type: {4}, Last Seen: {5}",
                DeviceInfo.HostEndPoint, "Manually Check" /*this.GetExternalIP()*/, DeviceInfo.ServiceControlPart, DeviceInfo.ServiceDescriptionPart, DeviceInfo.ServiceType, LastSeen);
        }
	}
}