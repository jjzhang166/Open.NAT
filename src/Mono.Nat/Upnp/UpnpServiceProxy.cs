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
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Mono.Nat
{
    internal class UpnpServiceProxy
    {
        private readonly UpnpNatDeviceInfo _deviceInfo;

        public UpnpServiceProxy(UpnpNatDeviceInfo deviceInfo)
        {
            _deviceInfo = deviceInfo;
        }

        public async Task<IPAddress> GetExternalIPAsync()
        {
            var message = new GetExternalIPAddressRequestMessage(_deviceInfo.ServiceType);
            var response =
                await RequestAsync<GetExternalIPAddressResponseMessage>(message);
            return response.ExternalIPAddress;
        }

        public async Task CreatePortMapAsync(Mapping mapping)
        {
            var message = new CreatePortMappingRequestMessage(mapping, _deviceInfo.LocalAddress, _deviceInfo.ServiceType);
            await RequestAsync<NoResponseMessage>(message);
        }

        public async Task DeletePortMapAsync(Mapping mapping)
        {
            var message = new DeletePortMappingRequestMessage(mapping, _deviceInfo.ServiceType);
            await RequestAsync<NoResponseMessage>(message);
        }

        public async Task<Mapping[]> GetAllMappingsAsync()
        {
            var mappings = new List<Mapping>();
            var index = 0;

            while(true)
            {
                try
                {
                    var message = new GetGenericPortMappingEntry(index, _deviceInfo.ServiceType);

                    var responseMessage = await RequestAsync<GetGenericPortMappingEntryResponseMessage>(message);

                    if (responseMessage == null) break;

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
                var message = new GetSpecificPortMappingEntryRequestMessage(protocol, port, _deviceInfo.ServiceType);
                var messageResponse = await RequestAsync<GetGenericPortMappingEntryResponseMessage>(message);

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

        private WebRequest BuildRequestServiceControl(string action)
        {
            NatUtility.Log("Initiating request to: {0}", _deviceInfo.ServiceControlUri);

            var request = WebRequest.CreateHttp(_deviceInfo.ServiceControlUri);
            request.KeepAlive = false;
            request.Method = "POST";
            request.ContentType = "text/xml; charset=\"utf-8\"";
            request.Headers.Add("SOAPACTION", "\"" + _deviceInfo.ServiceType + "#" + action + "\"");

            return request;
        }

        private WebRequest BuildRequestServiceDescription()
        {
            var request = WebRequest.CreateHttp(_deviceInfo.ServiceDescriptionUri);
            request.Headers.Add("ACCEPT-LANGUAGE", "en");
            request.Method = "GET";
            return request;
        }



        private async Task<T> RequestAsync<T>(RequestMessageBase requestMessage) where T: ResponseMessageBase
        {
            var messageBody = requestMessage.Envelop();
            var request = BuildRequestServiceControl(requestMessage.Action);

            request.ContentLength = messageBody.Length;

            if (messageBody.Length > 0)
            {
                using (var stream = await request.GetRequestStreamAsync())
                {
                    stream.Write(messageBody, 0, messageBody.Length);
                }
            }

            WebResponse response = null;
            try
            {
                try
                {
                    response = await request.GetResponseAsync();
                }
                catch (WebException ex)
                {
                    // Even if the request "failed" i want to continue on to read out the response from the router
                    response = ex.Response as HttpWebResponse;
                    if (response == null)
                        throw;
                }
                return response != null 
                    ? DecodeMessageFromResponse<T>(response.GetResponseStream(), response.ContentLength)
                    : null;
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        private T DecodeMessageFromResponse<T>(Stream s, long length) where T: ResponseMessageBase
        {
            var data = new StringBuilder();

            // Read out the content of the message, hopefully picking 
            // everything up in the case where we have no contentlength
            if (length != -1)
            {
                int bytesRead;
                var buffer = new byte[length];
                for (var i = 0; i < length; i += bytesRead)
                {
                    bytesRead = s.Read(buffer, 0, buffer.Length);
                    data.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }
            }
            else
            {
                data.Append(Encoding.UTF8.GetString(s.ReadToEnd()));
            }

            // Once we have our content, we need to see what kind of message it is. It'll either a an error
            // or a response based on the action we performed.
            return (T)Decode(data.ToString());
        }

        private ResponseMessageBase Decode(string message)
        {
            XmlNode node;
            var doc = new XmlDocument();
            doc.LoadXml(message);

            var nsm = new XmlNamespaceManager(doc.NameTable);

            // Error messages should be found under this namespace
            nsm.AddNamespace("errorNs", "urn:schemas-upnp-org:control-1-0");
            nsm.AddNamespace("responseNs", _deviceInfo.ServiceType);

            // Check to see if we have a fault code message.
            if ((node = doc.SelectSingleNode("//errorNs:UPnPError", nsm)) != null)
            {
                var code = Convert.ToInt32(node.GetXmlElementText("errorCode"), CultureInfo.InvariantCulture);
                var errorMessage = node.GetXmlElementText("errorDescription");
                throw new MappingException(code, errorMessage);
            }

            if (doc.SelectSingleNode("//responseNs:AddPortMappingResponse", nsm) != null)
                return new NoResponseMessage();

            if (doc.SelectSingleNode("//responseNs:DeletePortMappingResponse", nsm) != null)
                return new NoResponseMessage();

            if ((node = doc.SelectSingleNode("//responseNs:GetExternalIPAddressResponse", nsm)) != null)
                return new GetExternalIPAddressResponseMessage(node.GetXmlElementText("NewExternalIPAddress"));

            if ((node = doc.SelectSingleNode("//responseNs:GetGenericPortMappingEntryResponse", nsm)) != null)
                return new GetGenericPortMappingEntryResponseMessage(node, true);

            if ((node = doc.SelectSingleNode("//responseNs:GetSpecificPortMappingEntryResponse", nsm)) != null)
                return new GetGenericPortMappingEntryResponseMessage(node, false);

            NatUtility.Log("Unknown message returned. Please send me back the following XML:");
            NatUtility.Log(message);
            throw new ArgumentException("message");
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
            using(var stream = response.GetResponseStream())
            {
                var responseBody = stream.ReadToEnd();
                var servicesXml = Encoding.UTF8.GetString(responseBody);
                var xmldoc = new XmlDocument();
                xmldoc.LoadXml(servicesXml);
                return xmldoc;
            }
        }
    }
}