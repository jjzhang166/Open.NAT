using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Open.Nat.Tests
{
    public class UpnpMockServer : IDisposable
    {
        private readonly string _response;
        private readonly HttpListener _listener;
        private UdpClient _udpClient;

        public UpnpMockServer(string response)
        {
            _response = response;
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:5431/");
            _listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
        }

        public void Start()
        {
            Task.Factory.StartNew(() =>
                {
                    var remoteIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    using(_udpClient = new UdpClient(1900))
                    {
                        while (true)
                        {
                            var bytes = _udpClient.Receive(ref remoteIPEndPoint);
                            if(bytes == null || bytes.Length == 0) return;

                            var response = "HTTP/1.1 200 OK\r\n"
                                           + "Server: Custom/1.0 UPnP/1.0 Proc/Ver\r\n"
                                           + "EXT:\r\n"
                                           +
                                           "Location: http://127.0.0.1:5431/dyndev/uuid:0000e068-20a0-00e0-20a0-48a8000808e0\r\n"
                                           + "Cache-Control:max-age=1800\r\n"
                                           + "ST:urn:schemas-upnp-org:service:WANPPPConnection:1\r\n"
                                           +
                                           "USN:uuid:0000e068-20a0-00e0-20a0-48a802086048::urn:schemas-upnp-org:service:WANPPPConnection:1";

                            var responseBytes = Encoding.UTF8.GetBytes(response);
                            _udpClient.Send(responseBytes, responseBytes.Length, remoteIPEndPoint);
                        }
                    }
                });

            _listener.Start();
            Task.Factory.StartNew(() =>
                {
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

            if (!string.IsNullOrEmpty(_response))
            {
                var statusCode = 0;
                if(_response.Length <= 4 && int.TryParse(_response, out statusCode))
                {
                    context.Response.StatusCode = statusCode;
                    context.Response.StatusDescription = "UMMM";
                    context.Response.Close();
                    return;
                }
                var responseBytes = Encoding.UTF8.GetBytes(_response);
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.OutputStream.Flush();
            }
            
            context.Response.StatusCode = 200;
            context.Response.StatusDescription = "OK";
            context.Response.Close();
        }

        public void Dispose()
        {
            _listener.Close();
            _udpClient.Close();
        }
    }
}
