using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using Moq;
using NUnit.Framework;

namespace Mono.Nat.Tests
{
    [TestFixture]
    public class UpnpSearcherFixture
    {
        private readonly IPAddress _loopback = IPAddress.Parse("127.0.0.1");
        
        public static IEnumerable EndpointExpectations
        {
            get
            {
                yield return new TestCaseData(File.ReadAllText("..\\..\\Responses\\ServiceList.txt"), Evaluator.True);
                yield return new TestCaseData("500", Evaluator.False);
            }
        }

        class Evaluator
        {
            public Func<UpnpNatDeviceInfo, bool> Exp;
            public static Evaluator True { get { return new Evaluator{Exp = (d)=> true };}}
            public static Evaluator False { get { return new Evaluator { Exp = (d) => false }; } }
        }

        [TestFixtureSetUp]
        public void SetUp()
        {
            var ipp = new Mock<IIPAddressesProvider>();
            ipp.Setup(x => x.UnicastAddresses()).Returns(new[] { _loopback });
            NatUtility.Searchers = new List<ISearcher> { new UpnpSearcher(ipp.Object)};
        }

        [Test, TestCaseSource(typeof(UpnpSearcherFixture), "EndpointExpectations")]
        public void TestIt(string response, Func<NatDevice, bool> expected)
        {
            var found = false;
            using(var upnpServer = new UpnpMockServer(response))
            {
                upnpServer.Start();

                NatUtility.DeviceFound += (sender, args) => found =  true;
                NatUtility.UnhandledException += (sender, args) => Assert.Fail(args.ExceptionObject.ToString());
                NatUtility.Initialize();
                NatUtility.StartDiscovery();
                Thread.Sleep(500);
                Assert.IsTrue(found);
            }
        }
    }
}
