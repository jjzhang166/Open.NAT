//
// Authors:
//   Lucas Ontivero lucas.ontivero@gmail.com
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
using System.Globalization;
using System.Xml;

namespace Open.Nat
{
    internal abstract class ResponseMessageBase
    {
        private readonly string _response;
        protected string ServiceType;
        private readonly XmlDocument _document;
        private XmlNamespaceManager _nsm;

        protected ResponseMessageBase(string response, string serviceType)
        {
            _response = response;
            ServiceType = serviceType;
            _document = GetXmlDocument();
        }

        protected XmlNode GetNode()
        {
            var typeName = GetType().Name;
            var messageName = typeName.Substring(0, typeName.Length - "Message".Length);
            var node = _document.SelectSingleNode("//responseNs:" + messageName, _nsm);
            if (node == null) throw new InvalidOperationException("The response is invalid: " + messageName);

            return node;
        }

        private XmlDocument GetXmlDocument()
        {
            XmlNode node;
            var doc = new XmlDocument();
            doc.LoadXml(_response);

            _nsm = new XmlNamespaceManager(doc.NameTable);

            // Error messages should be found under this namespace
            _nsm.AddNamespace("errorNs", "urn:schemas-upnp-org:control-1-0");
            _nsm.AddNamespace("responseNs", ServiceType);

            // Check to see if we have a fault code message.
            if ((node = doc.SelectSingleNode("//errorNs:UPnPError", _nsm)) != null)
            {
                var code = Convert.ToInt32(node.GetXmlElementText("errorCode"), CultureInfo.InvariantCulture);
                var errorMessage = node.GetXmlElementText("errorDescription");
                throw new MappingException(code, errorMessage);
            }

            return doc;
        }
    }
}