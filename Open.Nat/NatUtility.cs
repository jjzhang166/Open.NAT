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
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Open.Nat
{
    /// <summary>
    /// 
    /// </summary>
    public class NatUtility
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

        private static readonly List<NatDevice> Devices = new List<NatDevice>();
        private static readonly Finalizer Finalizer = new Finalizer();
        private static readonly Searcher UpnpSearcher = new UpnpSearcher(new IPAddressesProvider());
        private static readonly Searcher PmpSearcher = new PmpSearcher(new IPAddressesProvider());
        internal static readonly Timer RenewTimer = new Timer(RenewMappings, null, 1000, 30000);

        private CancellationToken _cancellationToken;
        private List<Searcher> _searchers;

        public async Task<IList<NatDevice>> DiscoverAsync(PortMapper portMapper, CancellationTokenSource cts)
        {
            TraceSource.LogInfo("StartDiscovery");
            _cancellationToken = cts.Token;

            _searchers = new List<Searcher>();
            if (portMapper.HasFlag(PortMapper.Pmp))  _searchers.Add(PmpSearcher);
            if (portMapper.HasFlag(PortMapper.Upnp)) _searchers.Add(UpnpSearcher);

            return await Task.Factory.StartNew(_ =>
            {
                TraceSource.LogInfo("Searching");

                while (!_cancellationToken.IsCancellationRequested)
                {
                    foreach (var searcher in _searchers)
                    {
                        if (_cancellationToken.IsCancellationRequested) break;
                        searcher.Search();
                        Thread.Sleep(10);
                        if (_cancellationToken.IsCancellationRequested) break;
                        var d = searcher.Receive();
                        if (d != null) Devices.Add(d);
                    }
                }
                return Devices;
            }, _cancellationToken, TaskCreationOptions.LongRunning);
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
}
