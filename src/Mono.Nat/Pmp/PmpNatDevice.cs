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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mono.Nat.Pmp
{
	internal sealed class PmpNatDevice : NatDevice, IEquatable<PmpNatDevice> 
	{
	    private readonly IPAddress _publicAddress;

        internal IPAddress LocalAddress { get; private set; }
		
		internal PmpNatDevice (IPAddress localAddress, IPAddress publicAddress)
		{
			LocalAddress = localAddress;
			_publicAddress = publicAddress;
		}

        public override async Task CreatePortMapAsync(Mapping mapping)
		{
			await CreatePortMapAsync(mapping, true);
		}

		public override async Task DeletePortMapAsync (Mapping mapping)
		{
            await CreatePortMapAsync(mapping, false);
		}

		
		public override Task<Mapping[]> GetAllMappingsAsync ()
		{
			throw new NotSupportedException ();
		}

		public override Task<IPAddress> GetExternalIPAsync ()
		{
		    return Task.Run(() => _publicAddress);
		}

		public override Task<Mapping> GetSpecificMappingAsync (Protocol protocol, int port)
		{
			//NAT-PMP does not specify a way to get a specific port map
			throw new NotSupportedException ();
		}
		

		public override bool Equals(object obj)
		{
			var device = obj as PmpNatDevice;
			return (device != null) && Equals(device);
		}
		
		public override int GetHashCode ()
		{
			return _publicAddress.GetHashCode();
		}

		public bool Equals (PmpNatDevice other)
		{
			return (other != null) && _publicAddress.Equals(other._publicAddress);
		}

		private async Task<Mapping> CreatePortMapAsync(Mapping mapping, bool create)
		{
			var package = new List<byte> ();
			
			package.Add (PmpConstants.Version);
			package.Add (mapping.Protocol == Protocol.Tcp ? PmpConstants.OperationCodeTcp : PmpConstants.OperationCodeUdp);
			package.Add (0); //reserved
			package.Add (0); //reserved
			package.AddRange (BitConverter.GetBytes (IPAddress.HostToNetworkOrder((short)mapping.PrivatePort)));
			package.AddRange (BitConverter.GetBytes (create ? IPAddress.HostToNetworkOrder((short)mapping.PublicPort) : (short)0));
			package.AddRange (BitConverter.GetBytes (IPAddress.HostToNetworkOrder(mapping.Lifetime)));

            try
            {
                await CreatePortMapAsync2(package.ToArray(), mapping);
            }
            catch
		    {
				var type = create ? "create" : "delete";
				throw new MappingException (String.Format ("Failed to {0} portmap (protocol={1}, private port={2}", type, mapping.Protocol, mapping.PrivatePort));
			}
			
			return mapping;
		}
		
		private async Task CreatePortMapAsync2 (byte[] buffer, Mapping mapping)
		{
			var attempt = 0;
			var delay = PmpConstants.RetryDelay;

            using(var udpClient = new UdpClient())
            {
                await Task.Factory.StartNew(() => CreatePortMapListen(udpClient, mapping));

                while (attempt < PmpConstants.RetryAttempts /*&& !listenState.Success*/)
                {
                    udpClient.Send(buffer, buffer.Length, new IPEndPoint(LocalAddress, PmpConstants.ServerPort));

                    attempt++;
                    delay *= 2;
                    Thread.Sleep(delay);
                }
            }
		}
		
		private void CreatePortMapListen (UdpClient udpClient, Mapping mapping )
		{
			var endPoint = new IPEndPoint (LocalAddress, PmpConstants.ServerPort);
 
			while (true)
            {
                var data = udpClient.Receive(ref endPoint);
			
				if (data.Length < 16)
					continue;

				if (data[0] != PmpConstants.Version)
					continue;
			
				var opCode = (byte)(data[1] & 127);
				
				var protocol = Protocol.Tcp;
				if (opCode == PmpConstants.OperationCodeUdp)
					protocol = Protocol.Udp;

				var resultCode = IPAddress.NetworkToHostOrder (BitConverter.ToInt16 (data, 2));
				var epoch = (uint)IPAddress.NetworkToHostOrder (BitConverter.ToInt32 (data, 4));

				var privatePort = (ushort)IPAddress.NetworkToHostOrder (BitConverter.ToInt16 (data, 8));
				var publicPort = (ushort)IPAddress.NetworkToHostOrder (BitConverter.ToInt16 (data, 10));

				var lifetime = (uint)IPAddress.NetworkToHostOrder (BitConverter.ToInt32 (data, 12));
				
				if (resultCode != PmpConstants.ResultCodeSuccess) {
                    throw new Exception("Invalid result code");
				}
				
				if (lifetime == 0) {
					//mapping was deleted
					return;
				} 

                //mapping was created
				//TODO: verify that the private port+protocol are a match
				mapping.PublicPort = publicPort;
                mapping.Protocol = protocol;
				mapping.Expiration = DateTime.Now.AddSeconds (lifetime);
			}
		}


        /// <summary>
        /// Overridden.
        /// </summary>
        /// <returns></returns>
        public override string ToString( )
        {
            return String.Format( "PmpNatDevice - Local Address: {0}, Public IP: {1}, Last Seen: {2}",
                LocalAddress, _publicAddress, LastSeen );
        }
	}
}