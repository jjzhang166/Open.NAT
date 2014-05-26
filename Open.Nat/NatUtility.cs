//
// Authors:
//   Ben Motmans <ben.motmans@gmail.com>
//   Lucas Ontivero lucasontivero@gmail.com
//
// Copyright (C) 2007 Ben Motmans
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
    /// Protocol that should be used for searching a NAT device. 
    /// </summary>
    public enum PortMapper
    {
        /// <summary>
        /// Use only Port Mapping Protocol
        /// </summary>
        Pmp,
        /// <summary>
        /// Use only Universal Plug and Play
        /// </summary>
        Upnp,
        /// <summary>
        /// User both, Port Mapping Protocol and Universal Plug and Play
        /// </summary>
        Both
    }
    /// <summary>
    /// 
    /// </summary>
	public static class NatUtility
	{
        private static readonly List<NatDevice> Devices = new List<NatDevice>();
        private static readonly Finalizer Finalizer = new Finalizer();
        private static readonly ManualResetEvent Searching;
        private static readonly Searcher UpnpSearcher = new UpnpSearcher(new IPAddressesProvider());
        private static readonly Searcher PmpSearcher = new PmpSearcher(new IPAddressesProvider());
        
        private static int _discoveryTimeout;
        internal static readonly Timer RenewTimer = new Timer(RenewMappings, null, 1000, 30000);
        internal static DateTime DiscoveryTime { get; set; }

        /// <summary>
        /// Specifies if ports opened by Open.NAT should be closed as part of the
        /// process shutdown. All mappings will be released except those created as permanet.
        /// A permanent mapping can be created with Mapping.Lifetime=0. They will also be created 
        /// when the router only support Permanent mappings.
        /// Default: true
        /// </summary>
        public static bool ReleaseOnShutdown { get; set; }

        /// <summary>
        /// Specifies the protocol that should be used for searching a NAT device. <see cref="PortMapper">PortMapper enum</see>
        /// For example, if the value is Upnp, the discovery process will search only for Upnp devices.
        /// </summary>
        public static PortMapper PortMapper { get; set; } 

        /// <summary>
        /// Specifies the maximun time (in milliseconds) that the discovery process can run before stop and fail. Searching timeout
        /// are notified using event <see cref="#DiscoveryTimeout">DiscoveryTimeout</see>
        /// Default: 5000 (5 seconds)
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">if value is less or equal to 0.</exception>
        public static int DiscoveryTimeout 
        { 
            get { return _discoveryTimeout; }
            set
            {
                if(value <= 0) throw new ArgumentOutOfRangeException("value", "SearchTimeout must be greater than 0");
                _discoveryTimeout = value;
            }
        }
        /// <summary>
        /// Occurs when a NAT device able to manage ports is found.
        /// </summary>
        /// <example>
        /// NatUtility.DeviceFound += (s, e)=>
        ///     Console.WriteLine("Found. Ext IP {0}", await e.Device.GetExternalIPAsync());
        /// </example>
        /// <remarks>
        /// Currently Open.Nat is able to discover and map ports using UPNP and PMP however, 
        /// those protocols don't provide the same set of features. While UPNP supports ports
        /// listing, PMP doesn't. If you try to list the opened ports with a NAT-PMP device a 
        /// NotSupportedException will be thrown. Developers have to handle this situations by
        /// catching them. 
        /// </remarks>
        /// <remarks>
        /// The event is raised as many times as NAT devices are found in the LAN, most of 
        /// the cases there is just one however, if two or more devices are discovered, it
        /// is important to identify which one should be used,
        /// </remarks>
		public static event EventHandler<DeviceEventArgs> DeviceFound;

        /// <summary>
        /// Occurs when a NAT device is not found before the elapsed time specified by <see cref="#DiscoveryTimeout">DiscoveryTimeout</see>
        /// </summary>
        /// <example>
        /// NatUtility.DiscoveryTimedout += (s, e)=>
        ///     Console.WriteLine("No NAT device found after {0} milliseconds", NatUtility.DiscoveryTimeout);
        /// </example>
        /// <remarks>
        /// Before to raise this event Open.NAT stops the discovery process. Developers can increase the 
        /// <see cref="#DiscoveryTimeout">DiscoveryTimeout</see> value and try again restarting the discovery with
        /// <see cref="#StartDiscovery">StartDiscovery</see> method.
        /// </remarks>
        public static event EventHandler<DiscoveryTimeoutEventArgs> DiscoveryTimedout;

        /// <summary>
        /// Occurs when occurs an exception that was not expected <see cref="http://msdn.microsoft.com/en-us/library/system.unhandledexceptioneventargs(v=vs.110).aspx"/>
        /// </summary>
        /// <example>
        /// NatUtility.UnhandledException += (s, e)=>
        ///     Console.WriteLine("Houston we have a problem: {0}", e.ExceptionObject);
        /// </example>
        /// <remarks>
        /// This event should never be raised except for really exceptional situations and this iis only thrown
        /// while in the discovery stage.
        /// </remarks>
        public static event EventHandler<UnhandledExceptionEventArgs> UnhandledException;

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

        static NatUtility()
        {
            PortMapper = PortMapper.Both;
            ReleaseOnShutdown = true;
            Searching = new ManualResetEvent(false);
            DiscoveryTimeout = 5000;
        }

        /// <summary>
        /// 
        /// </summary>
        public static void Initialize()
        {
            TraceSource.LogInfo("Initializing");

            var task = Task.Factory.StartNew(SearchAndListen, TaskCreationOptions.LongRunning);
            task.ContinueWith(o =>
                {
                    var exceptionType = task.Exception.GetType();
                    if (exceptionType == typeof(TimeoutException) && DiscoveryTimedout != null)
                    {
                        DiscoveryTimedout(typeof (NatUtility), new DiscoveryTimeoutEventArgs());
                        return;
                    }
                    if (UnhandledException != null)
                        UnhandledException(typeof(NatUtility), new UnhandledExceptionEventArgs(task.Exception, false));
                }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Starts the discovery process.
        /// </summary>
        /// <remarks>
        /// Once started, it continues searching for NAT devices and never stops. It has to be stopped
        /// invoking <see cref="#StopDiscovery">StopDiscovery</see>. It is a good idea to let it run for a 
        /// couple of seconds before to stop it.
        /// </remarks>
		public static void StartDiscovery ()
		{
            TraceSource.LogInfo("StartDiscovery");
            DiscoveryTime = DateTime.UtcNow; 
            Searching.Set();
		}

        /// <summary>
        /// Stops the discovery process.
        /// </summary>
        /// <remarks>
        /// Stopping the discovery process that no new attempts to search NAT devices will be performed
        /// by Open.NAT. However if once stopped, a NAT responses to a previous discovery request, that 
        /// response is processed. It means after stopping discovery, it is possible to have a DeviceFound evented.
        /// </remarks>
        public static void StopDiscovery()
		{
            TraceSource.LogInfo("StopDiscovery");
            Searching.Reset();
		}

        private static void SearchAndListen()
        {
            var searchers = new List<Searcher>();
            if (PortMapper == PortMapper.Pmp || PortMapper == PortMapper.Both)
                searchers.Add(PmpSearcher);

            if (PortMapper == PortMapper.Upnp || PortMapper == PortMapper.Both)
                searchers.Add(UpnpSearcher);

            foreach (var searcher in searchers)
            {
                searcher.DeviceFound += OnDeviceFound;
            }

            TraceSource.LogInfo("Searching");
            while (true)
            {
//#if(!DEBUG)
                CheckSearchTimeout();
//#endif
                foreach (var searcher in searchers)
                {
                    Searching.WaitOne();
                    searcher.Receive();
                }

                foreach (var searcher in searchers)
                {
                    Searching.WaitOne();
                    searcher.Search();
                }
                Thread.Sleep(10);
            }
        }

        private static void CheckSearchTimeout()
        {
            var milliseconds = (DateTime.UtcNow - DiscoveryTime).TotalMilliseconds;

            if (milliseconds > DiscoveryTimeout)
            {
                TraceSource.LogInfo("Search timeout.");
                if (Devices.Count == 0)
                {
                    TraceSource.LogWarn(
                        "No devices found which means that the network is broken or there is no UPnP/PMP capable router");
                }
                StopDiscovery();
                throw new TimeoutException("SearchTimeout");
            }
        }

        private static void OnDeviceFound(object sender, DeviceEventArgs args)
        {
            var handler = DeviceFound;
            TraceSource.LogInfo("{0} device found. ", args.Device.GetType().Name);
            TraceSource.LogInfo("---------------------VVV\n{0}", args.Device.ToString());
            if (handler != null)
            {
                Devices.Add(args.Device);
                handler(sender, args);
            }
            else
            {
                TraceSource.LogWarn(
                 "*** There is no handler to notify about the finding! ***\n" +
                "Go to https://github.com/lontivero/Open.Nat/wiki/Warnings#there-is-no-handler-to-notify-about-the-finding");
            }
        }

        /// <summary>
        /// Release all ports opened by Open.NAT. 
        /// </summary>
        /// <remarks>
        /// If ReleaseOnShutdown value is true, it release all the mappings created through the library.
        /// </remarks>
        public static void ReleaseAll()
        {
            if(!ReleaseOnShutdown) return;
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
