//
// Authors:
//   Ben Motmans <ben.motmans@gmail.com>
//
// Copyright (C) 2007 Ben Motmans
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
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;

namespace Mono.Nat
{
	public static class NatUtility
	{
        private static readonly ManualResetEvent Searching;
		public static event EventHandler<DeviceEventArgs> DeviceFound;
		public static event EventHandler<DeviceEventArgs> DeviceLost;
        
        public static event EventHandler<UnhandledExceptionEventArgs> UnhandledException;

	    private static readonly List<ISearcher> Controllers;

	    public static TextWriter Logger { get; set; }

	    public static bool Verbose { get; set; }

	    static NatUtility()
        {
            Searching = new ManualResetEvent(false);

            Controllers = new List<ISearcher>{
                UpnpSearcher.Instance//, 
                //PmpSearcher.Instance
            };

            foreach (var searcher in Controllers)
            {
                searcher.DeviceFound += OnDeviceFound;
                searcher.DeviceLost += OnDeviceLost;
            }
            var t = new Thread(SearchAndListen);
            t.IsBackground = true;
            t.Start();
        }

	    private static void OnDeviceLost(object sender, DeviceEventArgs args)
	    {
	        var handler = DeviceLost;
            if (handler != null) handler(sender, args);
	    }

	    private static void OnDeviceFound(object sender, DeviceEventArgs args)
	    {
	        var handler = DeviceFound;
            if (handler != null) handler(sender, args);
	    }

	    internal static void Log(string format, params object[] args)
		{
			var logger = Logger;
			if (logger != null) logger.WriteLine(format, args);
		}

        private static void SearchAndListen()
        {
            while (true)
            {
                Searching.WaitOne();

                try
                {
					Receive(UpnpSearcher.Instance, UpnpSearcher.Sockets);
					//Receive(PmpSearcher.Instance, PmpSearcher.Sockets);

                    foreach (var s in Controllers.Where(s => s.NextSearch < DateTime.Now))
                    {
                        Log("Searching for: {0}", s.GetType().Name);
                        s.Search();
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

		static void Receive (ISearcher searcher, IEnumerable<UdpClient> clients)
		{
			var received = new IPEndPoint(IPAddress.Parse("192.168.0.1"), 5351);
			foreach (var client in clients.Where(c=>c.Available>0))
			{
				var localAddress = ((IPEndPoint)client.Client.LocalEndPoint).Address;
				var data = client.Receive(ref received);
				searcher.Handle(localAddress, data, received);
            }
        }
		
		public static void StartDiscovery ()
		{
            Searching.Set();
		}

		public static void StopDiscovery ()
		{
            Searching.Reset();
		}
	}
}
