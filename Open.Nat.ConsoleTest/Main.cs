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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Open.Nat.ConsoleTest
{
	class NatTest
	{
        public static void Main(string[] args)
		{
            NatUtility.TraceSource.Switch.Level = SourceLevels.Verbose;
            NatUtility.TraceSource.Listeners.Add(new ColorConsoleListener());
            NatUtility.DeviceFound += DeviceFound;
            NatUtility.Initialize();
            NatUtility.StartDiscovery();

            Thread.Sleep(5000);
            Console.WriteLine("Press any kay to exit...");
            Console.ReadKey();
        }
		
		private static async void DeviceFound (object sender, DeviceEventArgs args)
        {
            NatUtility.StartDiscovery();
			var device = args.Device;
		    
		    var sb = new StringBuilder();
		    var ip = await device.GetExternalIPAsync();
            sb.AppendFormat("\n\nYour IP: {0}\n", ip);

            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700, "Open.Nat Testing"));
            sb.AppendFormat("Added mapping: {0}:1700 -> 127.0.0.1:1600\n\n", ip);

            sb.AppendFormat("+------+-------------------------------+--------------------------------+----------------------------------+\n");
            sb.AppendFormat("| PROT | PUBLIC (Reacheable)           | PRIVATE (Your computer)        | Descriptopn                      |\n");
            sb.AppendFormat("+------+----------------------+--------+-----------------------+--------+----------------------------------+\n");
            sb.AppendFormat("|      | IP Address           | Port   | IP Address            | Port   |                                  |\n");
            sb.AppendFormat("+------+----------------------+--------+-----------------------+--------+----------------------------------+\n");
            foreach (var mapping in await device.GetAllMappingsAsync())
            {
                sb.AppendFormat("|  {5} | {0,-20} | {1,6} | {2,-21} | {3,6} | {4,-33}|\n", 
                    ip, mapping.PublicPort, mapping.PrivateIP, mapping.PrivatePort, mapping.Description, mapping.Protocol == Protocol.Tcp ? "TCP" : "UDP");
            }
            sb.AppendFormat("+------+----------------------+--------+-----------------------+--------+----------------------------------+\n");

            sb.AppendFormat("[Removing TCP mapping] {0}:1700 -> 127.0.0.1:1600\n", ip);
            await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700));
            sb.AppendFormat("[Done]\n");

		    var mappings = await device.GetAllMappingsAsync();
		    var deleted = !mappings.Any(x => x.Description == "Open.Nat Testing");
            sb.AppendFormat(deleted 
                ? "[SUCCESS]: Test mapping effectively removed ;)" 
                : "[FAILURE]: Test mapping wan not removed!");
            Console.WriteLine(sb.ToString());
        }
	}
}