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
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

namespace Open.Nat
{
    internal class SoapClient
    {
        private readonly Uri _url;
        private readonly string _serviceType;

        public SoapClient(Uri url, string serviceType)
        {
            _url = url;
            _serviceType = serviceType;
        }

        public async Task<string> InvokeAsync(string operationName, IDictionary<string, object> args)
        {
            var messageBody = BuildMessageBody(operationName, args);
            var request = BuildHttpWebRequest(operationName, messageBody);

            if (messageBody.Length > 0)
            {
                using (var stream = await request.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(messageBody, 0, messageBody.Length);
                }
            }

            WebResponse response = null;
            try
            {
                try
                {
                    response = await request.GetResponseAsync();
                }
                catch (WebException ex)
                {
                    // Even if the request "failed" i want to continue on to read out the response from the router
                    response = ex.Response as HttpWebResponse;
                    if (response == null)
                        throw;
                }

                var stream = response.GetResponseStream();
                var contentLength = response.ContentLength;

                var reader = new StreamReader(stream, Encoding.UTF8);
                // Read out the content of the message, hopefully picking 
                // everything up in the case where we have no contentlength
                return contentLength != 1
                    ? reader.ReadAsMany((int)contentLength)
                    : reader.ReadToEnd();

            }
            finally
            {
                if (response != null)
                    response.Close();
            }
        }

        private HttpWebRequest BuildHttpWebRequest(string operationName, byte[] messageBody)
        {
            var request = WebRequest.CreateHttp(_url);
            request.KeepAlive = false;
            request.Method = "POST";
            request.ContentType = "text/xml; charset=\"utf-8\"";
            request.Headers.Add("SOAPACTION", "\"" + _serviceType + "#" + operationName + "\"");
            request.ContentLength = messageBody.Length;
            return request;
        }

        private byte[] BuildMessageBody(string operationName, IEnumerable<KeyValuePair<string, object>> args)
        {
            var sb = new StringBuilder();
            sb.Append("<s:Envelope ");
            sb.Append("   xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" ");
            sb.Append("   s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">");
            sb.Append("<s:Body>");
            sb.Append("   <u:" + operationName + " xmlns:u=\"" + _serviceType + "\">");
            sb.Append("   </u:" + operationName + ">");
            sb.Append("</s:Body>");
            sb.Append("</s:Envelope>\r\n\r\n");

            foreach (var a in args)
            {
                sb.Append("<" + a.Key + ">" + args + "</" + Convert.ToString(a.Value, CultureInfo.InvariantCulture) + ">");
            }

            var messageBody = Encoding.UTF8.GetBytes(sb.ToString());
            return messageBody;
        }
    }
}
