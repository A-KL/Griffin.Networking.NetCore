using System.IO;
using System.Text;

namespace Griffin.Networking.Protocol.Http.Services.BodyDecoders.Mono
{
    /// <summary>
    /// Stream-based multipart handling.
    ///
    /// In this incarnation deals with an HttpInputStream as we are now using
    /// IntPtr-based streams instead of byte [].   In the future, we will also
    /// send uploads above a certain threshold into the disk (to implement
    /// limit-less HttpInputFiles). 
    /// </summary>
    /// <remarks>
    /// Taken from HttpRequest in mono (http://www.mono-project.com)
    /// </remarks>
    internal class HttpMultipart
    {
        private const byte Cr = (byte) '\r';
        private const byte Lf = (byte) '\n';
        private readonly string boundary;
        private readonly byte[] boundaryBytes;
        private readonly byte[] buffer;
        private readonly Stream data;
        private readonly Encoding encoding;
        private readonly StringBuilder sb;
        private bool atEof;

        // See RFC 2046 
        // In the case of multipart entities, in which one or more different
        // sets of data are combined in a single body, a "multipart" media type
        // field must appear in the entity's header.  The body must then contain
        // one or more body parts, each preceded by a boundary delimiter line,
        // and the last one followed by a closing boundary delimiter line.
        // After its boundary delimiter line, each body part then consists of a
        // header area, a blank line, and a body area.  Thus a body part is
        // similar to an RFC 822 message in syntax, but different in meaning.

        public HttpMultipart(Stream data, string b, Encoding encoding)
        {
            this.data = data;
            this.boundary = b;
            this.boundaryBytes = encoding.GetBytes(b);
            this.buffer = new byte[this.boundaryBytes.Length + 2]; // CRLF or '--'
            this.encoding = encoding;
            this.sb = new StringBuilder();
        }

        private bool CompareBytes(byte[] orig, byte[] other)
        {
            for (var i = orig.Length - 1; i >= 0; i--)
                if (orig[i] != other[i])
                    return false;

            return true;
        }

        private static string GetContentDispositionAttribute(string l, string name)
        {
            var idx = l.IndexOf(name + "=\"");
            if (idx < 0)
                return null;
            var begin = idx + name.Length + "=\"".Length;
            var end = l.IndexOf('"', begin);
            if (end < 0)
                return null;
            if (begin == end)
                return "";
            return l.Substring(begin, end - begin);
        }

        private string GetContentDispositionAttributeWithEncoding(string l, string name)
        {
            var idx = l.IndexOf(name + "=\"");
            if (idx < 0)
                return null;
            var begin = idx + name.Length + "=\"".Length;
            var end = l.IndexOf('"', begin);
            if (end < 0)
                return null;
            if (begin == end)
                return "";

            var temp = l.Substring(begin, end - begin);
            var source = new byte[temp.Length];
            for (var i = temp.Length - 1; i >= 0; i--)
                source[i] = (byte) temp[i];

            return this.encoding.GetString(source);
        }

        private long MoveToNextBoundary()
        {
            long retval = 0;
            var gotCr = false;

            var state = 0;
            var c = this.data.ReadByte();
            while (true)
            {
                if (c == -1)
                    return -1;

                if (state == 0 && c == Lf)
                {
                    retval = this.data.Position - 1;
                    if (gotCr)
                        retval--;
                    state = 1;
                    c = this.data.ReadByte();
                }
                else if (state == 0)
                {
                    gotCr = (c == Cr);
                    c = this.data.ReadByte();
                }
                else if (state == 1 && c == '-')
                {
                    c = this.data.ReadByte();
                    if (c == -1)
                        return -1;

                    if (c != '-')
                    {
                        state = 0;
                        gotCr = false;
                        continue; // no ReadByte() here
                    }

                    var nread = this.data.Read(this.buffer, 0, this.buffer.Length);
                    var bl = this.buffer.Length;
                    if (nread != bl)
                        return -1;

                    if (!this.CompareBytes(this.boundaryBytes, this.buffer))
                    {
                        state = 0;
                        this.data.Position = retval + 2;
                        if (gotCr)
                        {
                            this.data.Position++;
                            gotCr = false;
                        }
                        c = this.data.ReadByte();
                        continue;
                    }

                    if (this.buffer[bl - 2] == '-' && this.buffer[bl - 1] == '-')
                    {
                        this.atEof = true;
                    }
                    else if (this.buffer[bl - 2] != Cr || this.buffer[bl - 1] != Lf)
                    {
                        state = 0;
                        this.data.Position = retval + 2;
                        if (gotCr)
                        {
                            this.data.Position++;
                            gotCr = false;
                        }
                        c = this.data.ReadByte();
                        continue;
                    }
                    this.data.Position = retval + 2;
                    if (gotCr)
                        this.data.Position++;
                    break;
                }
                else
                {
                    // state == 1
                    state = 0; // no ReadByte() here
                }
            }

            return retval;
        }

        private bool ReadBoundary()
        {
            try
            {
                var line = this.ReadLine();
                while (line == "")
                    line = this.ReadLine();
                if (line[0] != '-' || line[1] != '-')
                    return false;

                if (!StrUtils.EndsWith(line, this.boundary, false))
                    return true;
            }
            catch
            {
            }

            return false;
        }

        private string ReadHeaders()
        {
            var s = this.ReadLine();
            if (s == "")
                return null;

            return s;
        }

        private string ReadLine()
        {
            // CRLF or LF are ok as line endings.
            var gotCr = false;
            var b = 0;
            this.sb.Length = 0;
            while (true)
            {
                b = this.data.ReadByte();
                if (b == -1)
                {
                    return null;
                }

                if (b == Lf)
                {
                    break;
                }
                gotCr = (b == Cr);
                this.sb.Append((char) b);
            }

            if (gotCr)
                this.sb.Length--;

            return this.sb.ToString();
        }

        public Element ReadNextElement()
        {
            if (this.atEof || this.ReadBoundary())
                return null;

            var elem = new Element();
            string header;
            while ((header = this.ReadHeaders()) != null)
            {
                if (StrUtils.StartsWith(header, "Content-Disposition:", true))
                {
                    elem.Name = GetContentDispositionAttribute(header, "name");
                    elem.Filename = StripPath(this.GetContentDispositionAttributeWithEncoding(header, "filename"));
                }
                else if (StrUtils.StartsWith(header, "Content-Type:", true))
                {
                    elem.ContentType = header.Substring("Content-Type:".Length).Trim();
                }
            }

            var start = this.data.Position;
            elem.Start = start;
            var pos = this.MoveToNextBoundary();
            if (pos == -1)
                return null;

            elem.Length = pos - start;
            return elem;
        }

        private static string StripPath(string path)
        {
            if (path == null || path.Length == 0)
                return path;

            if (path.IndexOf(":\\") != 1 && !path.StartsWith("\\\\"))
                return path;
            return path.Substring(path.LastIndexOf('\\') + 1);
        }

        #region Nested type: Element

        public class Element
        {
            public string ContentType;
            public string Filename;
            public long Length;
            public string Name;
            public long Start;

            public override string ToString()
            {
                return "ContentType " + this.ContentType + ", Name " + this.Name + ", Filename " + this.Filename + ", Start " + this.Start.ToString() + ", Length " + this.Length.ToString();
            }
        }

        #endregion
    }
}