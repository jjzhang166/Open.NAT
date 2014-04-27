using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Mono.Nat.Upnp
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
            var message = new GetExternalIPAddressRequestMessage();
            var response =
                await PerformRequestAsync<GetExternalIPAddressResponseMessage>(message);
            return response.ExternalIPAddress;
        }

        public async Task CreatePortMapAsync(Mapping mapping)
        {
            var message = new CreatePortMappingRequestMessage(mapping, _deviceInfo.LocalAddress);
            await PerformRequestAsync<NoResponseMessage>(message);
        }

        public async Task DeletePortMapAsync(Mapping mapping)
        {
            var message = new DeletePortMappingRequestMessage(mapping);
            await PerformRequestAsync<NoResponseMessage>(message);
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

                    var responseMessage = await PerformRequestAsync<GetGenericPortMappingEntryResponseMessage>(message);

                    if (responseMessage == null) break;

                    var mapping = new Mapping(responseMessage.Protocol, responseMessage.InternalPort,
                                              responseMessage.ExternalPort, responseMessage.LeaseDuration);
                    mapping.Description = responseMessage.PortMappingDescription;

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
                var messageResponse = await PerformRequestAsync<GetGenericPortMappingEntryResponseMessage>(message);

                var mapping = new Mapping(messageResponse.Protocol, messageResponse.InternalPort, messageResponse.ExternalPort, messageResponse.LeaseDuration);
                mapping.Description = messageResponse.PortMappingDescription;
                return mapping;
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

            var request = (HttpWebRequest)HttpWebRequest.Create(_deviceInfo.ServiceControlUri);
            request.KeepAlive = false;
            request.Method = "POST";
            request.ContentType = "text/xml; charset=\"utf-8\"";
            request.Headers.Add("SOAPACTION", "\"" + _deviceInfo.ServiceType + "#" + action + "\"");

            return request;
        }

        private WebRequest BuildRequestServiceDescription()
        {
            var request = (HttpWebRequest)WebRequest.Create(_deviceInfo.ServiceDescriptionUri);
            request.Headers.Add("ACCEPT-LANGUAGE", "en");
            request.Method = "GET";
            return request;
        }


        private byte[] Envelop(RequestMessageBase requestMessage)
        {
            string bodyString = "<s:Envelope "
                                + "xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" "
                                + "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">"
                                + "<s:Body>"
                                + "<u:" + requestMessage.Action + " "
                                + "xmlns:u=\"" + _deviceInfo.ServiceType + "\">"
                                + requestMessage.GetBody()
                                + "</u:" + requestMessage.Action + ">"
                                + "</s:Body>"
                                + "</s:Envelope>\r\n\r\n";

            return Encoding.UTF8.GetBytes(bodyString);
        }

        private async Task<T> PerformRequestAsync<T>(RequestMessageBase requestMessage) where T: ResponseMessageBase
        {
            var messageBody = Envelop(requestMessage);
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
                if (response == null) return null;

                return DecodeMessageFromResponse<T>(response.GetResponseStream(), response.ContentLength);
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
            var bytesRead = 0;
            var totalBytesRead = 0;
            var buffer = new byte[10240];

            // Read out the content of the message, hopefully picking everything up in the case where we have no contentlength
            if (length != -1)
            {
                while (totalBytesRead < length)
                {
                    bytesRead = s.Read(buffer, 0, buffer.Length);
                    data.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    totalBytesRead += bytesRead;
                }
            }
            else
            {
                while ((bytesRead = s.Read(buffer, 0, buffer.Length)) != 0)
                    data.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }

            // Once we have our content, we need to see what kind of message it is. It'll either a an error
            // or a response based on the action we performed.
            return (T)Decode(data.ToString());
        }

        private ResponseMessageBase Decode(string message)
        {
            XmlNode node = null;
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
            var request = BuildRequestServiceDescription();
            var response = await request.GetResponseAsync();

            try
            {
                var abortCount = 0;
                var buffer = new byte[10240];
                var servicesXml = new StringBuilder();
                var xmldoc = new XmlDocument();
                var httpresponse = response as HttpWebResponse;
                var s = response.GetResponseStream();

                if (httpresponse.StatusCode != HttpStatusCode.OK)
                {
                    NatUtility.Log("Couldn't get services list: {0}", httpresponse.StatusCode);
                    return; // FIXME: This the best thing to do??
                }

                while (true)
                {
                    var bytesRead = s.Read(buffer, 0, buffer.Length);
                    servicesXml.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    try
                    {
                        xmldoc.LoadXml(servicesXml.ToString());
                        response.Close();
                        break;
                    }
                    catch (XmlException)
                    {
                        // If we can't receive the entire XML within 500ms, then drop the connection
                        // Unfortunately not all routers supply a valid ContentLength (mine doesn't)
                        // so this hack is needed to keep testing our recieved data until it gets successfully
                        // parsed by the xmldoc. Without this, the code will never pick up my router.
                        if (abortCount++ > 50)
                        {
                            response.Close();
                            return;
                        }
                        NatUtility.Log("{0}: Couldn't parse services list", _deviceInfo.HostEndPoint);
                        System.Threading.Thread.Sleep(10);
                    }
                }

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
            catch(Exception e)
            {
                NatUtility.Log(e.ToString());
            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }
    }
}