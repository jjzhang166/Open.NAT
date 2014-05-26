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
	/// <summary>
	/// Represents a port forwarding entry in the NAT translation table.
	/// </summary>
	public class Mapping
	{
	    private DateTime _expiration;
	    private bool _isPermanent;
	    private int _lifetime;

        /// <summary>
        /// Gets the mapping's description. It is the value stored in the NewPortMappingDescription parameter. 
        /// The NewPortMappingDescription parameter is a human readable string that describes the connection. 
        /// It is used in sorme web interfaces of routers so the user can see which program is using what port.
        /// </summary>
	    public string Description { get; internal set; }
        /// <summary>
        /// Gets the private ip.
        /// </summary>
        public IPAddress PrivateIP { get; internal set; }
        /// <summary>
        /// Gets the protocol.
        /// </summary>
        public Protocol Protocol { get; internal set; }
        /// <summary>
        /// The PrivatePort parameter specifies the port on a client machine to which all traffic 
        /// coming in on <see cref="#PublicPort">PublicPort</see> for the protocol specified by 
        /// <see cref="#Protocol">Protocol</see> should be forwarded to.
        /// </summary>
        /// <see cref="Protocol">Protocol enum</see>
        public int PrivatePort { get; internal set; }
        /// <summary>
        /// Gets the public ip.
        /// </summary>
        public IPAddress PublicIP { get; internal set; }
        /// <summary>
        /// Gets the external (visible) port number.
        /// It is the value stored in the NewExternalPort parameter .
        /// The NewExternalPort parameter is used to specify the TCP or UDP port on the WAN side of the router which should be forwarded. 
        /// </summary>
        public int PublicPort { get; internal set; }
        /// <summary>
        /// Gets the lifetime. The Lifetime parameter tells the router how long the portmapping should be active. 
        /// Since most programs don't know this in advance, it is often set to 0, which means 'unlimited' or 'permanent'.
        /// </summary>
        /// <remarks>
        /// All portmappings are release automatically as part of the shutdown process when <see cref="NatUtility">NatUtility</see>.<see cref="NatUtility#releaseonshutdown">ReleaseOnShutdown</see> is true.
        /// Permanent portmappings will not be released if the process ends anormally.
        /// Since most programs don't know the lifetime in advance, Open.NAT renew all the portmappings (except the permanents) before they expires. So, developers have to close explicitly those portmappings
        /// they don't want to remain open for the session.
        /// </remarks>
        public int Lifetime 
        { 
            get { return _lifetime; }
            internal set
            {
                _lifetime = value;
                switch (_lifetime)
                {
                    case int.MaxValue:
                        _expiration = DateTime.MaxValue;
                        _isPermanent = true;
                        break;
                    case 0:
                        _expiration = DateTime.UtcNow;
                        _isPermanent = true;
                        break;
                    default:
                        _expiration = DateTime.UtcNow.AddSeconds(_lifetime);
                        _isPermanent = false;
                        break;
                }
            } 
        }

        /// <summary>
        /// Gets the expiration. The property value is calculated using <see cref="#Lifetime">Lifetime</see> property.
        /// </summary>
        public DateTime Expiration
        {
            get { return _expiration; }
            internal set
            {
                _expiration = value;
                Lifetime = (int)(_expiration - DateTime.UtcNow).TotalSeconds;
            }
        }

	    internal Mapping(Protocol protocol, IPAddress privateIP, int privatePort, int publicPort)
            : this(protocol, privateIP, privatePort, publicPort, 0, "Open.Nat")
		{
		}

        internal Mapping(Protocol protocol, IPAddress privateIP, int privatePort, int publicPort, string description)
            : this(protocol, privateIP, privatePort, publicPort, 0, description)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mapping"/> class.
        /// </summary>
        /// <param name="protocol">The protocol.</param>
        /// <param name="privateIP">The private ip.</param>
        /// <param name="privatePort">The private port.</param>
        /// <param name="publicPort">The public port.</param>
        /// <param name="lifetime">The lifetime.</param>
        /// <param name="description">The description.</param>
        internal Mapping(Protocol protocol, IPAddress privateIP, int privatePort, int publicPort, int lifetime, string description)
        {
            Guard.IsInRange(privatePort, 0, ushort.MaxValue, "privatePort");
            Guard.IsInRange(publicPort, 0, ushort.MaxValue, "publicPort");
            Guard.IsInRange(lifetime, 0, int.MaxValue, "lifetime");
            Guard.IsTrue(protocol == Protocol.Tcp || protocol == Protocol.Udp, "protocol");

            Protocol = protocol;
            PrivateIP = privateIP;
            PrivatePort = privatePort;
            PublicIP = IPAddress.None;
            PublicPort = publicPort;
            Lifetime = lifetime;
            Description = description;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mapping"/> class.
        /// </summary>
        /// <param name="protocol">The protocol.</param>
        /// <param name="privatePort">The private port.</param>
        /// <param name="publicPort">The public port.</param>
        /// <remarks>
        /// This constructor initializes a Permanent mapping. The description by deafult is "Open.NAT"
        /// </remarks>
        public Mapping(Protocol protocol, int privatePort, int publicPort)
            : this(protocol, IPAddress.None, privatePort, publicPort, 0, "Open.NAT")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mapping"/> class.
        /// </summary>
        /// <param name="protocol">The protocol.</param>
        /// <param name="privatePort">The private port.</param>
        /// <param name="publicPort">The public port.</param>
        /// <param name="description">The description.</param>
        /// <remarks>
        /// This constructor initializes a Permanent mapping.
        /// </remarks>
        public Mapping(Protocol protocol, int privatePort, int publicPort, string description)
            : this(protocol, IPAddress.None, privatePort, publicPort, 0, description)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Mapping"/> class.
        /// </summary>
        /// <param name="protocol">The protocol.</param>
        /// <param name="privatePort">The private port.</param>
        /// <param name="publicPort">The public port.</param>
        /// <param name="lifetime">The lifetime.</param>
        /// <param name="description">The description.</param>
        public Mapping(Protocol protocol, int privatePort, int publicPort, int lifetime, string description)
            : this(protocol, IPAddress.None, privatePort, publicPort, lifetime, description)
        {
        }

        /// <summary>
        /// Determines whether this instance is expired.
        /// </summary>
        /// <remarks>
        /// Permanent mappings never expires.
        /// </remarks>
	    public bool IsExpired ()
		{
			return !_isPermanent && Expiration < DateTime.UtcNow;
		}

        internal bool ShoundRenew()
        {
            return !_isPermanent && (DateTime.UtcNow - Expiration).TotalSeconds < 5;
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("{0} {1} --> {2}:{3} ({4})",
                                    Protocol == Protocol.Udp ? "Tcp" : "Udp",
                                    PublicPort,
                                    PrivateIP,
                                    PrivatePort,
                                    Description); 
        }
	}

    internal class Guard
    {
        public static void IsInRange(int paramValue, int lowerBound, int upperBound, string paramName)
        {
            if (paramValue < lowerBound || paramValue > upperBound)
                throw new ArgumentOutOfRangeException(paramName);
        }

        public static void IsTrue(bool exp, string paramName)
        {
            if(!exp)
                throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
