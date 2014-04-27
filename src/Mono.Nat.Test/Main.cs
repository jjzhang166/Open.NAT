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
using System.Threading;

namespace Mono.Nat.Test
{
	class NatTest
	{
		public static void Main(string[] args)
		{
			new NatTest ();
		}
		
		public NatTest ()
		{
		    NatUtility.Logger = Console.Out ;
		    NatUtility.Verbose = true;
			NatUtility.DeviceFound += DeviceFound;
			NatUtility.DeviceLost += DeviceLost;
			
			NatUtility.StartDiscovery ();
			
			Console.WriteLine ("Discovery started");
			
			Thread.Sleep (7000);
            NatUtility.StopDiscovery();
            Console.WriteLine("Discovery finished");
		    Console.ReadKey();

		}
		
		private void DeviceFound (object sender, DeviceEventArgs args)
        {
            try
            {
			    var device = args.Device;
    			
			    Console.ForegroundColor = ConsoleColor.Red;
			    Console.WriteLine ("Device found");
			    Console.ResetColor();
			    Console.WriteLine ("Type: {0}", device.GetType().Name);

                var t = device.GetExternalIPAsync();
                t.Wait();

			    Console.WriteLine ("IP: {0}", t.Result);
                var t1 = device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700));
                t1.Wait();
			    Console.WriteLine ("Maped");

                var mappingsT =  device.GetAllMappingsAsync();
                mappingsT.Wait();
                var mappings = mappingsT.Result;
                foreach (var mapping in mappings)
                {
                    Console.WriteLine(mapping.ToString());
                }
			
				return;
                /*
                    Mapping mapping = new Mapping(Protocol.Tcp, 6001, 6001);
                    device.CreatePortMap(mapping);
                    Console.WriteLine("Create Mapping: protocol={0}, public={1}, private={2}", mapping.Protocol, mapping.PublicPort, mapping.PrivatePort);

                    try
                    {
                        Mapping m = device.GetSpecificMapping(Protocol.Tcp, 6001);
                        Console.WriteLine("Specific Mapping: protocol={0}, public={1}, private={2}", m.Protocol, m.PublicPort, m.PrivatePort);
                    }
                    catch
                    {
                        Console.WriteLine("Couldnt get specific mapping");
                    }
                    foreach (Mapping mp in device.GetAllMappings())
                    {
                        Console.WriteLine("Existing Mapping: protocol={0}, public={1}, private={2}", mp.Protocol, mp.PublicPort, mp.PrivatePort);
                        device.DeletePortMap(mp);
                    }

                    Console.WriteLine("External IP: {0}", device.GetExternalIP());
                    Console.WriteLine("Done...");
                 */

            } catch (Exception ex) {
				Console.WriteLine (ex.Message);
				Console.WriteLine (ex.StackTrace);
			}
		}
		
		private void DeviceLost (object sender, DeviceEventArgs args)
		{
			NatDevice device = args.Device;
			
			Console.WriteLine ("Device Lost");
			Console.WriteLine ("Type: {0}", device.GetType().Name);
		}
	}
}