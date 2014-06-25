using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
        public readonly static TraceSource TraceSource = new TraceSource("Open.NAT");

        private static readonly Dictionary<string, NatDevice> Devices = new Dictionary<string, NatDevice>();

        // Finalizer is never used however its destructor, that releases the open ports, is invoked by the
        // process as part of the shuting down step. So, don't remove it!
        private static readonly Finalizer Finalizer = new Finalizer();
        internal static readonly Timer RenewTimer = new Timer(RenewMappings, null, 1000, 30000);

        public async Task<NatDevice> DiscoverDeviceAsync()
        {
            var cts = new CancellationTokenSource(3 * 1000);
            return await DiscoverDeviceAsync(PortMapper.Pmp | PortMapper.Upnp, cts);
        }

        public async Task<NatDevice> DiscoverDeviceAsync(PortMapper portMapper, CancellationTokenSource cts)
        {
            var devices = await DiscoverAsync(portMapper, true, cts);
            var device = devices.FirstOrDefault();
            if(device==null)
            {
                throw new NatDeviceNotFoundException();
            }
            return device;
        }

        public async Task<IEnumerable<NatDevice>> DiscoverDevicesAsync(PortMapper portMapper, CancellationTokenSource cts)
        {
            var devices = await DiscoverAsync(portMapper, false, cts);
            return devices.ToArray();
        }

        private async Task<IEnumerable<NatDevice>> DiscoverAsync(PortMapper portMapper, bool onlyOne, CancellationTokenSource cts)
        {
            TraceSource.LogInfo("Start Discovery");
            var searcherTasks = new List<Task<IEnumerable<NatDevice>>>();
            if(portMapper.HasFlag(PortMapper.Upnp))
            {
                var upnpSearcher = new UpnpSearcher(new IPAddressesProvider());
                upnpSearcher.DeviceFound += (sender, args) => { if (onlyOne) cts.Cancel(); };
                searcherTasks.Add(upnpSearcher.Search(cts.Token));
            }
            if(portMapper.HasFlag(PortMapper.Pmp))
            {
                var pmpSearcher = new PmpSearcher(new IPAddressesProvider());
                pmpSearcher.DeviceFound += (sender, args) => { if (onlyOne) cts.Cancel(); };
                searcherTasks.Add(pmpSearcher.Search(cts.Token));
            }

            await Task.WhenAll(searcherTasks);
            TraceSource.LogInfo("Stop Discovery");
            
            var devices = searcherTasks.SelectMany(x => x.Result);
            foreach (var device in devices)
            {
                var key = device.ToString();
                NatDevice nat;
                if(Devices.TryGetValue(key, out nat))
                {
                    nat.Touch();
                }
                else
                {
                    Devices.Add(key, device);
                }
            }
            return devices;
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
            foreach (var device in Devices.Values)
            {
                device.ReleaseAll();
            }
        }

        private static void RenewMappings(object state)
        {
            foreach (var device in Devices.Values)
            {
                device.RenewMappings();
            }
        }
    }
}