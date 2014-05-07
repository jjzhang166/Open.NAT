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

using System.Xml;
using System.Text;

namespace Open.Nat
{
    internal abstract class RequestMessageBase
    {
        private readonly string _serviceType;
        public abstract string Action { get; }
        public abstract string ToXml();
 
        protected RequestMessageBase(string serviceType)
        {
            _serviceType = serviceType;
        }

        protected static void WriteFullElement(XmlWriter writer, string element, string value)
        {
            writer.WriteStartElement(element);
            writer.WriteString(value);
            writer.WriteEndElement();
        }

        protected static XmlWriter CreateWriter(StringBuilder sb)
        {
            var settings = new XmlWriterSettings {ConformanceLevel = ConformanceLevel.Fragment};
            return XmlWriter.Create(sb, settings);
        }

        public byte[] Envelop()
        {
            string bodyString = "<s:Envelope "
                                + "xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" "
                                + "s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">"
                                + "<s:Body>"
                                + "<u:" + Action + " "
                                + "xmlns:u=\"" + _serviceType + "\">"
                                + ToXml()
                                + "</u:" + Action + ">"
                                + "</s:Body>"
                                + "</s:Envelope>\r\n\r\n";

            return Encoding.UTF8.GetBytes(bodyString);
        }

    }
}
