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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Open.Nat
{
    internal sealed class UpnpNatDevice : NatDevice
	{
	    internal readonly UpnpNatDeviceInfo DeviceInfo;
        private readonly SoapClient _soapClient;

		internal UpnpNatDevice (UpnpNatDeviceInfo deviceInfo)
		{
            Touch();
            DeviceInfo = deviceInfo;
            DeviceInfo.UpdateInfo();
            _soapClient = new SoapClient(DeviceInfo.ServiceControlUri, DeviceInfo.ServiceType);
		}

		public override async Task<IPAddress> GetExternalIPAsync()
	    {
            var message = new GetExternalIPAddressRequestMessage();
            var responseData = await _soapClient.InvokeAsync("GetExternalIPAddress", message.ToXml());
            var response = new GetExternalIPAddressResponseMessage(responseData, DeviceInfo.ServiceType);
            return response.ExternalIPAddress;
        }

        public override async Task CreatePortMapAsync(Mapping mapping)
		{
            var message = new CreatePortMappingRequestMessage(mapping, DeviceInfo.LocalAddress);
            await _soapClient.InvokeAsync("AddPortMapping", message.ToXml());
        }

		public override async Task DeletePortMapAsync(Mapping mapping)
		{
            var message = new DeletePortMappingRequestMessage(mapping);
            await _soapClient.InvokeAsync("DeletePortMapping", message.ToXml());
        }

		public override async Task<IEnumerable<Mapping>> GetAllMappingsAsync()
		{
            var index = 0;
		    var mappings = new List<Mapping>();

            while (true)
            {
                try
                {
                    var message = new GetGenericPortMappingEntry(index);

                    var responseData = await _soapClient.InvokeAsync("GetGenericPortMappingEntry", message.ToXml());
                    var responseMessage = new GetGenericPortMappingEntryResponseMessage(responseData, DeviceInfo.ServiceType, true);

                    var mapping = new Mapping(responseMessage.Protocol
                        , responseMessage.InternalPort
                        , responseMessage.ExternalPort
                        , responseMessage.LeaseDuration
                        , responseMessage.PortMappingDescription);
                    mappings.Add(mapping);
                    index++;
                }
                catch (MappingException e)
                {
                    if (e.ErrorCode == 713) break; // there are no more mappings
                    throw;
                }
            }

            return mappings.ToArray();
        }

		public override async Task<Mapping> GetSpecificMappingAsync (Protocol protocol, int port)
		{
            try
            {
                var message = new GetSpecificPortMappingEntryRequestMessage(protocol, port);
                var responseData = await _soapClient.InvokeAsync("GetSpecificPortMappingEntry", message.ToXml());
                var messageResponse = new GetGenericPortMappingEntryResponseMessage(responseData, DeviceInfo.ServiceType, false);

                return new Mapping(messageResponse.Protocol
                    , messageResponse.InternalPort
                    , messageResponse.ExternalPort
                    , messageResponse.LeaseDuration
                    , messageResponse.PortMappingDescription);
            }
            catch (MappingException e)
            {
                if (e.ErrorCode != 714) throw;
                return new Mapping(Protocol.Tcp, -1, -1);
            }
        }

        public override string ToString( )
        {
            //GetExternalIP is blocking and can throw exceptions, can't use it here.
            return String.Format( 
                "EndPoint: {0}\nControl Url: {1}\nService Description Url: {2}\nService Type: {3}\nLast Seen: {4}",
                DeviceInfo.HostEndPoint, DeviceInfo.ServiceControlUri, DeviceInfo.ServiceDescriptionUri, DeviceInfo.ServiceType, LastSeen);
        }
	}
}