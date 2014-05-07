//
// Authors:
//   Alan McGovern  alan.mcgovern@gmail.com
//   Lucas Ontivero lucas.ontivero@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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

using System.Net;
using System.Globalization;
using System.Text;

namespace Open.Nat
{
    internal class CreatePortMappingRequestMessage : RequestMessageBase
    {
        private readonly IPAddress _localIpAddress;
        private readonly Mapping _mapping;

        public CreatePortMappingRequestMessage(Mapping mapping, IPAddress localIpAddress, string serviceType) 
            : base(serviceType)
        {
            _mapping = mapping;
            _localIpAddress = localIpAddress;
        }

        public override string Action
        {
            get { return "AddPortMapping"; }
        }

        public override string ToXml()
        {
            var builder = new StringBuilder(256);
            using(var writer = CreateWriter(builder))
            {
                WriteFullElement(writer, "NewRemoteHost", string.Empty);
                WriteFullElement(writer, "NewExternalPort", _mapping.PublicPort.ToString(CultureInfo.InvariantCulture));
                WriteFullElement(writer, "NewProtocol", _mapping.Protocol == Protocol.Tcp ? "TCP" : "UDP");
                WriteFullElement(writer, "NewInternalPort", _mapping.PrivatePort.ToString(CultureInfo.InvariantCulture));
                WriteFullElement(writer, "NewInternalClient", _localIpAddress.ToString());
                WriteFullElement(writer, "NewEnabled", "1");
                WriteFullElement(writer, "NewPortMappingDescription", _mapping.Description);
                WriteFullElement(writer, "NewLeaseDuration", _mapping.Lifetime.ToString(CultureInfo.InvariantCulture));

                writer.Flush();
                return builder.ToString();
            }
        }
    }
}
