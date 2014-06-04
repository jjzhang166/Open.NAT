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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Open.Nat
{
    /// <summary>
    /// 
    /// </summary>
    public class NatDiscoverer
    {
        /// <summary>
        /// The <see cref="http://msdn.microsoft.com/en-us/library/vstudio/system.diagnostics.tracesource">TraceSource</see> instance
        /// used for debugging and <see cref="https://github.com/lontivero/Open.Nat/wiki/Troubleshooting">Troubleshooting</see>
        /// </summary>
        /// <example>
        /// NatUtility.TraceSource.Switch.Level = SourceLevels.Verbose;
        /// NatUtility.TraceSource.Listeners.Add(new ConsoleListener());
        /// </example>
        /// <remarks>
        /// At least one trace listener has to be added to the Listeners collection if a trace is required; if no listener is added
        /// there will no be tracing to analyse.
        /// </remarks>
        /// <remarks>
        /// Open.NAT only supports SourceLevels.Verbose, SourceLevels.Error, SourceLevels.Warning and SourceLevels.Information.
        /// </remarks>
        public readonly static TraceSource TraceSource = new TraceSource("OpenNat");

        private static readonly ConcurrentBag<NatDevice> Devices = new ConcurrentBag<NatDevice>();
        private static readonly Finalizer Finalizer = new Finalizer();
        internal static readonly Timer RenewTimer = new Timer(RenewMappings, null, 1000, 30000);

        private CancellationToken _cancellationToken;

        public async Task<NatDevice> DiscoverDeviceAsync(PortMapper portMapper, CancellationTokenSource cts)
        {
            var devices = await DiscoverAsync(portMapper, true, cts);
            return devices.FirstOrDefault();
        }

        public async Task<IEnumerable<NatDevice>> DiscoverDevicesAsync(PortMapper portMapper, CancellationTokenSource cts)
        {
            var devices = await DiscoverAsync(portMapper, false, cts);
            return Devices.ToArray();
        }

        private async Task<IEnumerable<NatDevice>> DiscoverAsync(PortMapper portMapper, bool onlyOne, CancellationTokenSource cts)
        {
            TraceSource.LogInfo("StartDiscovery");
            _cancellationToken = cts.Token;

            await Task.Factory.StartNew(async _ => 
            {
                TraceSource.LogInfo("Searching");
                var ips = new IPAddressesProvider();
                foreach (var ip in ips.UnicastAddresses()) TraceSource.LogInfo("Unicast Address: " + ip);
                foreach (var ip in ips.GatewayAddresses()) TraceSource.LogInfo("Gateway Address: " + ip);
                foreach (var ip in ips.DnsAddresses()) TraceSource.LogInfo("DNS Address    : " + ip);

                var pmpSearcher = new PmpSearcher(ips);
                var pmpDevices = await pmpSearcher.Search(onlyOne, _cancellationToken);
                foreach (var device in pmpDevices)
                {
                    Devices.Add(device);
                }

                var upnpSearcher = new UpnpSearcher(ips);
                var upnpdevices = await upnpSearcher.Search(onlyOne, _cancellationToken);
                foreach (var device in upnpdevices)
                {
                    Devices.Add(device);
                }
            }, TaskCreationOptions.LongRunning) ;
            return Devices.ToArray();
        }

        //private static void OnDeviceFound(object sender, DeviceEventArgs args)
        //{
        //    var handler = DeviceFound;
        //    TraceSource.LogInfo("{0} device found. ", args.Device.GetType().Name);
        //    TraceSource.LogInfo("---------------------VVV\n{0}", args.Device.ToString());
        //    if (handler != null)
        //    {
        //        Devices.Add(args.Device);
        //        handler(sender, args);
        //    }
        //    else
        //    {
        //        TraceSource.LogWarn(
        //         "*** There is no handler to notify about the finding! ***\n" +
        //        "Go to https://github.com/lontivero/Open.Nat/wiki/Warnings#there-is-no-handler-to-notify-about-the-finding");
        //    }
        //}

        /// <summary>
        /// Release all ports opened by Open.NAT. 
        /// </summary>
        /// <remarks>
        /// If ReleaseOnShutdown value is true, it release all the mappings created through the library.
        /// </remarks>
        public static void ReleaseAll()
        {
            foreach (var device in Devices)
            {
                device.ReleaseAll();
            }
        }

        private static void RenewMappings(object state)
        {
            foreach (var device in Devices)
            {
                device.RenewMappings();
            }
        }
	}

    static class TaskExtensions
    {
        internal static void WaitFor(Task[] tasks, bool onlyOne)
        {
            if (onlyOne)
            {
                Task.WaitAny(tasks);
            }
            else
            {
                Task.WaitAll(tasks);
            }            
        }
    }
}
