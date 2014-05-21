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
    /// 
    /// </summary>
	public static class NatUtility
	{
        private static readonly List<NatDevice> Devices = new List<NatDevice>();
        private static readonly Finalizer Finalizer = new Finalizer();
        private static readonly ManualResetEvent Searching;
        private static int _discoveryTimeout;

        internal static List<ISearcher> Searchers = new List<ISearcher>
        {
             new UpnpSearcher(new IPAddressesProvider()),
             new PmpSearcher(new IPAddressesProvider())
        };

        internal static DateTime DiscoveryTime { get; set; }

        public static int DiscoveryTimeout 
        { 
            get { return _discoveryTimeout; }
            set
            {
                if(value <= 0) throw new ArgumentOutOfRangeException("SearchTimeout must be greater than 0");
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
        /// Occurs when occurs an exception that was not expected <see cref="http://msdn.microsoft.com/en-us/library/system.unhandledexceptioneventargs(v=vs.110).aspx"/>
        /// </summary>
        /// <example>
        /// NatUtility.UnhandledException += (s, e)=>
        ///     Console.WriteLine("Houston we have a problem: {0}", e.ExceptionObject);
        /// </example>
        /// <remarks>
        /// This event should never be raised except for really exceptional situations and this only thrown
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
            Searching = new ManualResetEvent(false);
            DiscoveryTimeout = 4000;
        }

        public static void Initialize()
        {
            TraceSource.LogInfo("Initializing");
            foreach (var searcher in Searchers)
            {
                searcher.DeviceFound += OnDeviceFound;
            }

            Task.Factory.StartNew(SearchAndListen, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Starts the discovery process.
        /// </summary>
        /// <remarks>
        /// Once started, it continues searching for NAT devices and never stops. It has to be stopped
        /// invoking <see cref="StopDiscovery">StopDiscovery</see>. It is a good idea to let it run for a 
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
            TraceSource.LogInfo("Searching");
            while (true)
            {
                if (CheckSearchTimeout()) return;
                try
                {
                    foreach (var searcher in Searchers)
                    {
                        Searching.WaitOne();
                        searcher.Receive();
                    }

                    foreach (var searcher in Searchers)
                    {
                        Searching.WaitOne();
                        searcher.Search();
                    }
                }
                catch (Exception e)
                {
                    if (UnhandledException != null)
                        UnhandledException(typeof(NatUtility), new UnhandledExceptionEventArgs(e, false));
                }
                Thread.Sleep(10);
            }
        }

        private static bool CheckSearchTimeout()
        {
            var milliseconds = (DateTime.UtcNow - DiscoveryTime).TotalMilliseconds;

            if (milliseconds > DiscoveryTimeout)
            {
                TraceSource.LogInfo("Search timeout.");
                if (Devices.Count == 0)
                {
                    TraceSource.LogWarn(
                        "No devices found which means that the network is broken or there is no UPnP capable router");
                }
                StopDiscovery();
                return true;
            }
            return false;
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

        public static void ReleaseAll()
        {
            foreach (var device in Devices)
            {
                device.ReleaseAll();
            }
        }
	}

    sealed class Finalizer 
    {
        ~Finalizer() 
        {
            NatUtility.TraceSource.LogInfo("Closing ports opened in this session");
            NatUtility.ReleaseAll();
        }
    }
}
