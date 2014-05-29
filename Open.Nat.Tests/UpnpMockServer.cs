using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Open.Nat.Tests
{
    public class Server
    {
        public static void Main()
        {
            var server = new UpnpMockServer();
            server.Start();
            Console.ReadKey();
        }
    }

    public class UpnpMockServer : IDisposable
    {
        private readonly string _response;
        private readonly HttpListener _listener;
        private UdpClient _udpClient;
        private const string ServiceUrl = "http://127.0.0.1:5431/dyndev/uuid:0000e068-20a0-00e0-20a0-48a8000808e0";
        private const string ControlUrl = "http://127.0.0.1:5431/uuid:0000e068-20a0-00e0-20a0-48a802086048/WANPPPConnection:1";

        public UpnpMockServer()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:5431/");
            _listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        }

        public void Start()
        {
            StartAnnouncer();

            StartServer();
        }

        private void StartAnnouncer()
        {
            Task.Factory.StartNew(() =>{
                var remoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
                using (_udpClient = new UdpClient(1900))
                {
                    while (true)
                    {
                        var bytes = _udpClient.Receive(ref remoteIPEndPoint);
                        if (bytes == null || bytes.Length == 0) return;

                        var response = "HTTP/1.1 200 OK\r\n"
                                        + "Server: Custom/1.0 UPnP/1.0 Proc/Ver\r\n"
                                        + "EXT:\r\n"
                                        + "Location: " + ServiceUrl + "\r\n"
                                        + "Cache-Control:max-age=1800\r\n"
                                        + "ST:urn:schemas-upnp-org:service:WANPPPConnection:1\r\n"
                                        +
                                        "USN:uuid:0000e068-20a0-00e0-20a0-48a802086048::urn:schemas-upnp-org:service:WANPPPConnection:1";

                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        _udpClient.Send(responseBytes, responseBytes.Length, remoteIPEndPoint);
                    }
                }
            });
        }

        private void StartServer()
        {
            _listener.Start();
            Task.Factory.StartNew(() => {
                while (true)
                {
                    ProcessRequest();
                }
            });
        }


        private void ProcessRequest()
        {
            var result = _listener.BeginGetContext(ListenerCallback, _listener);
            result.AsyncWaitHandle.WaitOne();
        }

        private void ListenerCallback(IAsyncResult result)
        {
            if(!_listener.IsListening) return;
            var context = _listener.EndGetContext(result);
            var request = context.Request;
            if(request.Url.AbsoluteUri == ServiceUrl)
            {
                var responseBytes = File.OpenRead("..\\..\\Responses\\ServiceDescription.txt");
                responseBytes.CopyTo(context.Response.OutputStream);
                context.Response.OutputStream.Flush();

                context.Response.StatusCode = 200;
                context.Response.StatusDescription = "OK";
                context.Response.Close();
                return;
            }
            
            if(request.Url.AbsoluteUri == ControlUrl)
            {

                var soapActionHeader = request.Headers["SOAPACTION"];
                soapActionHeader = soapActionHeader.Substring(1, soapActionHeader.Length - 2);

                var soapActionHeaderParts = soapActionHeader.Split(new[] { '#' });
                var serviceType = soapActionHeaderParts[0];
                var soapAction = soapActionHeaderParts[1];
                var buffer = new byte[request.ContentLength64-4];
                request.InputStream.Read(buffer, 0, buffer.Length);
                var body = Encoding.UTF8.GetString(buffer);
                var envelop = XElement.Parse(body);

                switch (soapAction)
                {
                    case "GetExternalIPAddress":
                        processGetExternalIPAddress(envelop, context.Response);
                        return;
                    case "AddPortMapping":
                        processAddPortMapping(envelop, context.Response);
                        return;
                        break;
                    case "GetGenericPortMappingEntry":
                        break;
                    case "DeletePortMapping":
                        break;

                }
                var responseBytes = File.OpenRead("..\\..\\Responses\\ServiceDescription.txt");
                responseBytes.CopyTo(context.Response.OutputStream);
                context.Response.OutputStream.Flush();

                context.Response.StatusCode = 200;
                context.Response.StatusDescription = "OK";
                context.Response.Close();
                return;
            }
            var statusCode = 0;
            context.Response.StatusCode = statusCode;
            context.Response.StatusDescription = "UMMM";
            context.Response.Close();
        }

        private void processAddPortMapping(XElement envelop, HttpListenerResponse response)
        {
            var e = envelop.Descendants(XName.Get("{urn:schemas-upnp-org:service:WANPPPConnection:1}AddPortMapping")).First();
            var vals = e.Elements().ToDictionary(x => x.Name.LocalName, x=> x.Value);
            if(vals["NewLeaseDuration"]!="0")
            {
                Error(725, "OnlyPermanentLeaseSupported", response);
                return;
            }
            var responseXml = @"<?xml version=""1.0""?>
                <s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" 
                            s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
                    <s:Body>
                    <m:AddPortMappingResponse xmlns:m=""urn:schemas-upnp-org:service:WANPPPConnection:1"">
                    </m:AddPortMappingResponse>
                    </s:Body>
                </s:Envelope>";
            var bytes = Encoding.UTF8.GetBytes(responseXml);
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Flush();
            response.StatusCode = 200;
            response.StatusDescription = "OK";
            response.Close();
        }

        private void Error(int code, string description, HttpListenerResponse response)
        {
            var errTpl = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" 
                                    s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
                     <s:Body>
                      <s:Fault>
                       <faultcode>s:Client</faultcode>
                       <faultstring>UPnPError</faultstring>
                       <detail>
                        <UPnPError xmlns=""urn:schemas-upnp-org:control-1-0"">
                         <errorCode>{0}</errorCode>
                         <errorDescription>{1}</errorDescription>
                        </UPnPError>
                       </detail>
                      </s:Fault>
                     </s:Body>
                    </s:Envelope>";
            var errorXml = string.Format(errTpl, code, description);
            var bytes = Encoding.UTF8.GetBytes(errorXml);
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Flush();
            response.StatusCode = 500;
            response.StatusDescription = "Internal Server Error";
            response.Close();
        }

        private void processGetExternalIPAddress(XElement envelop, HttpListenerResponse response)
        {
            var responseXml = @"<?xml version=""1.0""?>
                <s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" 
                            s:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"">
                  <s:Body>
                    <m:GetExternalIPAddressResponse xmlns:m=""urn:schemas-upnp-org:service:WANPPPConnection:1"">
                      <NewExternalIPAddress>222.222.111.111</NewExternalIPAddress>
                    </m:GetExternalIPAddressResponse>
                  </s:Body>
                </s:Envelope>";
            var bytes = Encoding.UTF8.GetBytes(responseXml);
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Flush();
            response.StatusCode = 200;
            response.StatusDescription = "OK";
            response.Close();
        }

        public void Dispose()
        {
            _listener.Close();
            _udpClient.Close();
        }
    }
}
