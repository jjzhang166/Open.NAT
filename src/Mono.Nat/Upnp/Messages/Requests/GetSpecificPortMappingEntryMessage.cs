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

using System.Globalization;
using System.Text;

namespace Mono.Nat
{
	internal class GetSpecificPortMappingEntryRequestMessage : RequestMessageBase
	{
		private readonly Protocol _protocol;
        private readonly int _externalPort;

		public GetSpecificPortMappingEntryRequestMessage(Protocol protocol, int externalPort, string serviceType)
            : base(serviceType)
		{
			_protocol = protocol;
			_externalPort = externalPort;
		}

	    public override string Action
	    {
	        get { return "GetSpecificPortMappingEntry"; }
	    }

	    public override string ToXml()
		{
			var sb = new StringBuilder(64);
			using(var writer = CreateWriter(sb))
			{
			    WriteFullElement(writer, "NewRemoteHost", string.Empty);
			    WriteFullElement(writer, "NewExternalPort", _externalPort.ToString(CultureInfo.InvariantCulture));
			    WriteFullElement(writer, "NewProtocol", _protocol == Protocol.Tcp ? "TCP" : "UDP");
			    writer.Flush();

			    return sb.ToString();
			}
		}
	}
}
