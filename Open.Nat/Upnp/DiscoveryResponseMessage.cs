//
// Authors:
//   Lucas Ontivero lucasontivero@gmail.com 
//
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

namespace Mono.Nat.Upnp
{
    class DiscoveryResponseMessage
    {
        private IDictionary<string, string> _headers;

        public DiscoveryResponseMessage(string message)
        {
            var lines = message.Split(new[]{"\r\n"}, StringSplitOptions.RemoveEmptyEntries);
            var headers = from h in lines.Skip(1)
                    let c = h.Split(':')
                    let key = c[0]
                    let value = c.Length > 1 
                        ? string.Join(":", c.Skip(1)) 
                        : string.Empty 
                    select new {Key = key, Value = value.Trim()};
            _headers = headers.ToDictionary(x => x.Key.ToUpper(), x=>x.Value);
        }

        public string this[string key]
        {
            get { return _headers[key.ToUpper()]; }
        }
    }

    class xx
    {
        void x()
        {
            var m =
                "HTTP/1.1 200 OK\r\n" +
                "CACHE-CONTROL: max-age=100\r\n" +
                "DATE: Fri, 06 Jun 2014 05:24:52 GMT\r\n" +
                "EXT:\r\n" +
                "LOCATION: http://192.168.0.1:1900/igd.xml\r\n" +
                "SERVER: ipos/7.0 UPnP/1.0 TL-WR740N/4.0\r\n" +
                "ST: urn:schemas-upnp-org:service:WANIPConnection:1\r\n" +
                "USN: uuid:9f0865b3-f5da-4ad5-85b7-7404637fdf37::urn:schemas-upnp-org:service:WANIPConnection:1\r\n";

            var mm = new DiscoveryResponseMessage(m);
            var st = mm["Location"];
        }
        
    }
}
