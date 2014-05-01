using System.IO;

namespace Mono.Nat
{
    static class StreamExtensions
    {
        public static byte[] ReadToEnd(this Stream stream)
        {
            using (var ts = new MemoryStream())
            {
                var buffer = new byte[1048];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ts.Write(buffer, 0, bytesRead);
                }
                return ts.GetBuffer();
            }
        }
    }
}
