using System;
using System.Net;

namespace Open.Nat
{
    internal class UpnpNatDeviceInfo
    {
        public UpnpNatDeviceInfo(IPAddress localAddress, string locationDetails, string serviceType)
        {
            var locationUri = new Uri(locationDetails);

            NatUtility.Log("Found device at: {0}", locationUri.ToString());

            LocalAddress = localAddress;
            Ip = locationUri.Host;
            Port = locationUri.Port;
            ServiceDescriptionPart = locationUri.AbsolutePath;
            ServiceType = serviceType;
        }

        public string Ip { get; internal set; }
        public int Port { get; internal set; }

        public IPAddress LocalAddress { get; internal set; }

        public string ServiceDescriptionPart { get; internal set; }

        public string ServiceType { get; internal set; }

        public string ServiceControlPart { get; internal set; }

        public IPEndPoint HostEndPoint
        {
            get { return new IPEndPoint(IPAddress.Parse(Ip), Port); }
        }

        public Uri ServiceControlUri
        {
            get
            {
                var builder = new UriBuilder("http", Ip, Port, ServiceControlPart);
                return builder.Uri;
            }
        }

        public Uri ServiceDescriptionUri
        {
            get
            {
                var builder = new UriBuilder("http", Ip, Port, ServiceDescriptionPart);
                return builder.Uri;
            }
        } 
    }
}