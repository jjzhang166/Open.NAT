//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//   Ben Motmans <ben.motmans@gmail.com>
//   Lucas Ontivero lucasontivero@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Net;

namespace Open.Nat
{
	public class Mapping
	{
        public string Description { get; internal set; }
        public IPAddress PrivateIP { get; internal set; }
        public Protocol Protocol { get; internal set; }
        public int PrivatePort { get; internal set; }
        public int PublicPort { get; internal set; }
        public int Lifetime { get; internal set; }

        public DateTime Expiration { get; internal set; }

        internal Mapping(Protocol protocol, IPAddress privateIP, int privatePort, int publicPort)
            : this(protocol, privateIP, privatePort, publicPort, 0, "Open.Nat")
		{
		}

        internal Mapping(Protocol protocol, IPAddress privateIP, int privatePort, int publicPort, string description)
            : this(protocol, privateIP, privatePort, publicPort, 0, description)
        {
        }

        public Mapping(Protocol protocol, int privatePort, int publicPort)
            : this(protocol, IPAddress.None, privatePort, publicPort, 0, "Open.Nat")
        {
        }

        public Mapping(Protocol protocol, int privatePort, int publicPort, string description)
            : this(protocol, IPAddress.None, privatePort, publicPort, 0, description)
        {
        }

        internal Mapping(Protocol protocol, IPAddress privateIP, int privatePort, int publicPort, int lifetime, string description)
		{
			Protocol = protocol;
		    PrivateIP = privateIP;
			PrivatePort = privatePort;
			PublicPort = publicPort;
			Lifetime = lifetime;
		    Description = description;

			switch (lifetime)
			{
			    case int.MaxValue:
			        Expiration = DateTime.MaxValue;
			        break;
			    case 0:
			        Expiration = DateTime.Now;
			        break;
			    default:
			        Expiration = DateTime.Now.AddSeconds (lifetime);
			        break;
			}
		}

	    public bool IsExpired ()
		{
			return Expiration < DateTime.Now;
		}
	}
}
