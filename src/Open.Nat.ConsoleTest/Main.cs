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
using Open.Nat;

namespace Open.Nat.ConsoleTest
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
			NatUtility.Initialize();
			NatUtility.StartDiscovery ();
			
			Console.WriteLine ("Discovery started");
			
			Thread.Sleep (7000);
            NatUtility.StopDiscovery();
            Console.WriteLine("Discovery finished");
		    Console.ReadKey();

		}
		
		private static async void DeviceFound (object sender, DeviceEventArgs args)
        {
			var device = args.Device;
    			
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine ("Device found");
			Console.WriteLine ("Type: {0}", device.GetType().Name);
            Console.ResetColor();

            var ip = await device.GetExternalIPAsync();

			Console.WriteLine ("IP: {0}", ip);
            await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700));
			Console.WriteLine ("Maped");

            var mappings =  await device.GetAllMappingsAsync();
            foreach (var mapping in mappings)
            {
                Console.WriteLine(mapping + Environment.NewLine);
            }
        }
	}
}