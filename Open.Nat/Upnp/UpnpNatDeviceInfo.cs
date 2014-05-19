using System;
using System.Net;

namespace Open.Nat
{
    internal class UpnpNatDeviceInfo
    {
        public UpnpNatDeviceInfo(IPAddress localAddress, Uri locationUri, string serviceControlUrl, string serviceType)
        {
            LocalAddress = localAddress;
            ServiceType = serviceType;
            HostEndPoint = new IPEndPoint(IPAddress.Parse(locationUri.Host), locationUri.Port);

            if (Uri.IsWellFormedUriString(serviceControlUrl, UriKind.Absolute))
            {
                var u = new Uri(serviceControlUrl);
                var old = HostEndPoint;
                serviceControlUrl = serviceControlUrl.Substring(u.GetLeftPart(UriPartial.Authority).Length);

                NatUtility.TraceSource.LogInfo("{0}: Absolute URI detected. Host address is now: {1}", old, HostEndPoint);
                NatUtility.TraceSource.LogInfo("{0}: New control url: {1}", HostEndPoint, serviceControlUrl);
            }

            var builder = new UriBuilder("http", locationUri.Host, locationUri.Port, serviceControlUrl);
            ServiceControlUri = builder.Uri;
        }

        public IPEndPoint HostEndPoint { get; private set; }
        public IPAddress LocalAddress { get; private set; }
        public string ServiceType { get; private set; }
        public Uri ServiceControlUri { get; private set; }
    }
}