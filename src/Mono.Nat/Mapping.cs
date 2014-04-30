//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//   Ben Motmans <ben.motmans@gmail.com>
//
// Copyright (C) 2006 Alan McGovern
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

namespace Mono.Nat
{
	public class Mapping
	{
        public string Description { get; internal set; }

        public Protocol Protocol { get; internal set; }

        public int PrivatePort { get; internal set; }

        public int PublicPort { get; internal set; }

        public int Lifetime { get; internal set; }

        public DateTime Expiration { get; internal set; }

        public Mapping(Protocol protocol, int privatePort, int publicPort)
			: this (protocol, privatePort, publicPort, 0, "Mono.Nat")
		{
		}

        public Mapping(Protocol protocol, int privatePort, int publicPort, string description)
            : this(protocol, privatePort, publicPort, 0, description)
        {
        }
		
		public Mapping (Protocol protocol, int privatePort, int publicPort, int lifetime, string description)
		{
			Protocol = protocol;
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

		public override bool Equals (object obj)
		{
			var other = obj as Mapping;
			return other != null && (Protocol == other.Protocol &&
			                         PrivatePort == other.PrivatePort && 
                                     PublicPort == other.PublicPort);
		}

		public override int GetHashCode()
		{
			return Protocol.GetHashCode() ^ PrivatePort.GetHashCode() ^ PublicPort.GetHashCode();
		}

        public override string ToString( )
        {
            return string.Format(
@"Protocol  : {0}, 
Public Port : {1}, 
Private Port: {2}, 
Description : {3}, 
Expiration  : {4}, Lifetime: {5}", 
            Protocol, PublicPort, PrivatePort, Description, Expiration, Lifetime );
        }
	}
}
