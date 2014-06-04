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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Open.Nat
{
    internal abstract class Searcher
    {
        protected List<UdpClient> Sockets;

        public async Task<IEnumerable<NatDevice>> Search(bool onlyOne, CancellationToken cancelationToken)
        {
            return await Task.Factory.StartNew(_ =>{
                try
                {
                    NatDiscoverer.TraceSource.LogInfo("Searching for: {0}", GetType().Name);
                    Discover(cancelationToken);
                    Thread.Sleep(2000);
                    return Receive(onlyOne, cancelationToken);
                }
                finally
                {
                    CloseSockets();
                }

            }, cancelationToken, TaskCreationOptions.AttachedToParent );

        }

        private IEnumerable<NatDevice> Receive(bool onlyOne, CancellationToken cancelationToken)
        {
            var devices = new List<NatDevice>();
            //var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelationToken);
            foreach (var client in Sockets.Where(x=>x.Available>0))
            {
                var client1 = client;
                var localHost = ((IPEndPoint)client1.Client.LocalEndPoint).Address;
                var receivedFrom = new IPEndPoint(IPAddress.None, 0);
                var buffer = client1.Receive(ref receivedFrom);
                var device = AnalyseReceivedResponse(localHost, buffer, receivedFrom);
                if (device != null)
                    devices.Add(device);
            }

            return devices;
        }

        private void Discover(CancellationToken cancelationToken)
        {
            foreach (var socket in Sockets)
            {
                if (cancelationToken.IsCancellationRequested) break;
                try
                {
                    Search(socket, cancelationToken);
                }
                catch (Exception e)
                {
                    NatDiscoverer.TraceSource.LogError("Error searching {0} - Details:", GetType().Name);
                    NatDiscoverer.TraceSource.LogError(e.ToString());
                    continue; // Ignore any search errors
                }
            }
        }

        protected abstract void Search(UdpClient client, CancellationToken cancellationToken);

        public abstract NatDevice AnalyseReceivedResponse(IPAddress localAddress, byte[] response, IPEndPoint endpoint);

        public void CloseSockets()
        {
            foreach (var udpClient in Sockets)
            {
                udpClient.Close();
            }
        }
    }
}