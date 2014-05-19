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
	public static class NatUtility
	{
		public static event EventHandler<DeviceEventArgs> DeviceFound;
        public static event EventHandler<UnhandledExceptionEventArgs> UnhandledException;

        private static readonly ManualResetEvent Searching;
        internal static List<ISearcher> Searchers = new List<ISearcher>
        {
              new UpnpSearcher(new IPAddressesProvider())
            , new PmpSearcher(new IPAddressesProvider())
        };

        public readonly static TraceSource TraceSource = new TraceSource("OpenNat");

	    static NatUtility()
        {
            Searching = new ManualResetEvent(false);
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

        private static void SearchAndListen()
        {
            TraceSource.LogInfo("Searching");
            while (true)
            {
                Searching.WaitOne();

                try
                {
                    foreach (var searcher in Searchers)
                    {
                        searcher.Receive();
                    }

                    foreach (var searcher in Searchers)
                    {
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

	    private static void OnDeviceFound(object sender, DeviceEventArgs args)
	    {
	        var handler = DeviceFound;
            TraceSource.LogInfo("{0} device found. ", args.Device.GetType().Name);
            TraceSource.LogInfo("---------------------VVV\n{0}", args.Device.ToString());
            if (handler != null)
            {
                handler(sender, args);
            }
            else
            {
                TraceSource.LogWarn(
                 "*** There is no handler to notify about the finding! ***\n" +
                "Go to https://github.com/lontivero/Open.Nat/wiki/Warnings#there-is-no-handler-to-notify-about-the-finding");
            }
	    }

		public static void StartDiscovery ()
		{
            TraceSource.LogInfo("StartDiscovery");
            Searching.Set();
		}

		public static void StopDiscovery ()
		{
            TraceSource.LogInfo("StopDiscovery");
            Searching.Reset();
		}
	}
}
