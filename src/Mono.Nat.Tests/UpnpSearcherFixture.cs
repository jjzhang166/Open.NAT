using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Mono.Nat.Utils;
using Moq;
using NUnit.Framework;

namespace Mono.Nat.Tests
{
    [TestFixture]
    public class UpnpSearcherFixture
    {
        [Test]
        public void x()
        {
            var loopback = IPAddress.Parse("127.0.0.1");
            var ipp = new Mock<IIPAddressesProvider>();
            ipp.Setup(x => x.GetIPAddresses()).Returns(new[] { loopback });
            var searcher = new UpnpSearcher(ipp.Object);
            searcher.Search();

            //var x = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        }
    }
}
