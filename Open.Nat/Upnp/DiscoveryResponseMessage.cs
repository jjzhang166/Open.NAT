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
            _headers = headers.ToDictionary(x => x.Key, x=>x.Value);
        }

        public string this[string key]
        {
            get { return _headers[key]; }
        }
    }
}
