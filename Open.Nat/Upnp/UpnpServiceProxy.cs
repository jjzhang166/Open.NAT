//
// Authors:
//   Lucas Ontivero lucasontivero@gmail.com 
//
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
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Open.Nat
{
    internal class UpnpServiceProxy
    {
        private readonly UpnpNatDeviceInfo _deviceInfo;
        private readonly SoapClient _soapClient;

        public UpnpServiceProxy(UpnpNatDeviceInfo deviceInfo)
        {
            _deviceInfo = deviceInfo;
            _soapClient = new SoapClient(_deviceInfo.ServiceControlUri, _deviceInfo.ServiceType);
        }

        public async Task<IPAddress> GetExternalIPAsync()
        {
            var message = new GetExternalIPAddressRequestMessage();
            var responseData = await _soapClient.InvokeAsync("GetExternalIPAddress", message.ToXml());
            var response = new GetExternalIPAddressResponseMessage(responseData, _deviceInfo.ServiceType);
            return response.ExternalIPAddress;
        }

        public async Task CreatePortMapAsync(Mapping mapping)
        {
            var message = new CreatePortMappingRequestMessage(mapping, _deviceInfo.LocalAddress);
            await _soapClient.InvokeAsync("AddPortMapping", message.ToXml());
        }

        public async Task DeletePortMapAsync(Mapping mapping)
        {
            var message = new DeletePortMappingRequestMessage(mapping);
            await _soapClient.InvokeAsync("DeletePortMapping", message.ToXml());
        }

        public async Task<Mapping[]> GetAllMappingsAsync()
        {
            var mappings = new List<Mapping>();
            var index = 0;

            while(true)
            {
                try
                {
                    var message = new GetGenericPortMappingEntry(index);

                    var responseData = await _soapClient.InvokeAsync("GetGenericPortMappingEntry", message.ToXml());
                    var responseMessage = new GetGenericPortMappingEntryResponseMessage(responseData, _deviceInfo.ServiceType, true);

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
                    if(e.ErrorCode == 713) break;
                    throw;
                }
            }
            return mappings.ToArray();
        }


        public async Task<Mapping> GetSpecificMappingAsync(Protocol protocol, int port)
        {
            try
            {
                var message = new GetSpecificPortMappingEntryRequestMessage(protocol, port);
                var responseData = await _soapClient.InvokeAsync("GetSpecificPortMappingEntry", message.ToXml());
                var messageResponse = new GetGenericPortMappingEntryResponseMessage(responseData, _deviceInfo.ServiceType, false);

                return new Mapping(messageResponse.Protocol
                    , messageResponse.InternalPort
                    , messageResponse.ExternalPort
                    , messageResponse.LeaseDuration
                    , messageResponse.PortMappingDescription);
            }
            catch (MappingException e)
            {
                if(e.ErrorCode != 714) throw;
                return new Mapping(Protocol.Tcp, -1, -1);
            }
        }

        private WebRequest BuildRequestServiceDescription()
        {
            var request = WebRequest.CreateHttp(_deviceInfo.ServiceDescriptionUri);
            request.Headers.Add("ACCEPT-LANGUAGE", "en");
            request.Method = "GET";
            return request;
        }

        public async Task GetServicesListAsync()
        {
            WebResponse response = null;
            try
            {
                var request = BuildRequestServiceDescription();
                response = await request.GetResponseAsync();

                var httpresponse = response as HttpWebResponse;

                if (httpresponse != null && httpresponse.StatusCode != HttpStatusCode.OK)
                {
                    NatUtility.Log("Couldn't get services list: {0}", httpresponse.StatusCode);
                    return; // FIXME: This the best thing to do??
                }

                var xmldoc = ReadXmlResponse(response);

                NatUtility.Log("{0}: Parsed services list", _deviceInfo.HostEndPoint);
                var ns = new XmlNamespaceManager(xmldoc.NameTable);
                ns.AddNamespace("ns", "urn:schemas-upnp-org:device-1-0");
                var nodes = xmldoc.SelectNodes("//*/ns:serviceList", ns);

                foreach (XmlNode node in nodes)
                {
                    //Go through each service there
                    foreach (XmlNode service in node.ChildNodes)
                    {
                        //If the service is a WANIPConnection, then we have what we want
                        var type = service.GetXmlElementText("serviceType");
                        NatUtility.Log("{0}: Found service: {1}", _deviceInfo.HostEndPoint, type);

                        if (!type.Equals(_deviceInfo.ServiceType, StringComparison.OrdinalIgnoreCase)) continue;

                        _deviceInfo.ServiceControlPart = service.GetXmlElementText("controlURL");
                        NatUtility.Log("{0}: Found upnp service at: {1}", _deviceInfo.HostEndPoint, _deviceInfo.ServiceControlPart);

                        if (Uri.IsWellFormedUriString(_deviceInfo.ServiceControlPart, UriKind.Absolute))
                        {
                            var u = new Uri(_deviceInfo.ServiceControlPart);
                            var old = _deviceInfo.HostEndPoint;
                            _deviceInfo.Ip = u.Host;
                            _deviceInfo.Port = u.Port;
                            NatUtility.Log("{0}: Absolute URI detected. Host address is now: {1}", old, _deviceInfo.HostEndPoint);
                            _deviceInfo.ServiceControlPart = _deviceInfo.ServiceControlPart.Substring(u.GetLeftPart(UriPartial.Authority).Length);
                            NatUtility.Log("{0}: New control url: {1}", _deviceInfo.HostEndPoint, _deviceInfo.ServiceControlUri);
                        }
                        NatUtility.Log("{0}: Handshake Complete", _deviceInfo.HostEndPoint);
                    }
                }
            }
            catch (WebException ex)
            {
                // Just drop the connection, FIXME: Should i retry?
                NatUtility.Log("{0}: Device denied the connection attempt: {1}", _deviceInfo.HostEndPoint, ex);
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        private XmlDocument ReadXmlResponse(WebResponse response)
        {
            using(var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                var servicesXml = reader.ReadToEnd();
                var xmldoc = new XmlDocument();
                xmldoc.LoadXml(servicesXml);
                return xmldoc;
            }
        }
    }


}