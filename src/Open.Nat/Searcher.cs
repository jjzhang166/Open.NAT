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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Open.Nat
{
    internal abstract class Searcher : ISearcher
    {
        public event EventHandler<DeviceEventArgs> DeviceFound;
        protected List<UdpClient> Sockets;

        public void Receive()
        {
            var received = WellKnownConstants.NatPmpEndPoint;
            foreach (var client in Sockets.Where(c => c.Available > 0))
            {
                var localAddress = ((IPEndPoint)client.Client.LocalEndPoint).Address;
                var data = client.Receive(ref received);
                Handle(localAddress, data, received);
            }
        }

        public void Search()
        {
            if (!IsSearchTime) return;

            NatUtility.Log("Searching for: {0}", GetType().Name);

            foreach (var socket in Sockets)
            {
                try
                {
                    Search(socket);
                }
                catch (Exception)
                {
                    continue; // Ignore any search errors
                }
            }
        }

        protected void OnDeviceFound(DeviceEventArgs args)
        {
            var handler = DeviceFound;
            if (handler != null)
                handler(this, args);
        }

        protected abstract void Search(UdpClient client);

        public abstract void Handle(IPAddress localAddress, byte[] response, IPEndPoint endpoint);

        protected DateTime NextSearch { get; set; }

        public bool IsSearchTime
        {
            get { return NextSearch < DateTime.Now; }
        }
    }
}