using System;
using System.Linq;
using System.Text;

namespace Griffin.Networking.Buffers
{
    /// <summary>
    /// Read text from a buffer slice.
    /// </summary>
    public class StringBufferSliceReader : IStringBufferReader
    {
        private readonly Encoding encoding;
        private readonly int length;
        private int position;
        private IBufferSlice reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringBufferSliceReader"/> class.
        /// </summary>
        /// <remarks>You must use <see cref="Assign(IBufferSlice,int)"/> if you use this constructor<para>Initialied using ASCII as encoding.</para></remarks>
        public StringBufferSliceReader()
        {
            this.encoding = Encoding.ASCII;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringBufferSliceReader"/> class.
        /// </summary>
        /// <param name="encoding">Encoding to use when converting byte array to strings.</param>
        /// <remarks>You must use <see cref="Assign(IBufferSlice, int)"/> if you use this constructor</remarks>
        public StringBufferSliceReader(Encoding encoding)
        {
            if (encoding == null) throw new ArgumentNullException("encoding");
            this.encoding = encoding;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringBufferSliceReader"/> class.
        /// </summary>
        public StringBufferSliceReader(IBufferSlice slice, int count)
        {
            if (slice == null) throw new ArgumentNullException("slice");
            this.reader = slice;
            this.length = count;
            this.encoding = Encoding.ASCII;
        }

        /// <summary>
        /// Gets buffer that we are reading from.
        /// </summary>
        public byte[] Buffer
        {
            get { return this.reader.Buffer; }
        }

        /// <summary>
        /// Gets if end of buffer have been reached
        /// </summary>
        /// <value></value>
        public bool EndOfFile
        {
            get { return this.position == this.length; }
        }

        #region IStringBufferReader Members

        /// <summary>
        /// Gets current character
        /// </summary>
        /// <value><see cref="char.MinValue"/> if end of buffer.</value>
        public char Current
        {
            get { return this.HasMore ? (char) this.reader.Buffer[this.position] : char.MinValue; }
        }

        /// <summary>
        /// Gets if more bytes can be processed.
        /// </summary>
        /// <value></value>
        public bool HasMore
        {
            get { return this.position < this.Length; }
        }

        /// <summary>
        /// Gets or sets current position in buffer.
        /// </summary>
        /// <remarks>
        /// THINK before you manually change the position since it can blow up
        /// the whole parsing in your face.
        /// </remarks>
        public int Index
        {
            get { return this.position; }
            set { this.position = value; }
        }

        /// <summary>
        /// Gets total length of buffer.
        /// </summary>
        /// <value></value>
        public int Length
        {
            get { return this.length; }
        }

        /// <summary>
        /// Gets next character
        /// </summary>
        /// <value><see cref="char.MinValue"/> if end of buffer.</value>
        public char Peek
        {
            get { return this.HasMore ? (char) this.reader.Buffer[this.Index + 1] : char.MinValue; }
        }

        /// <summary>
        /// Gets number of bytes left.
        /// </summary>
        public int RemainingLength
        {
            get { return this.length - this.position; }
        }

        /// <summary>
        /// Consume current character.
        /// </summary>
        public void Consume()
        {
            ++this.Index;
        }

        /// <summary>
        /// Consume specified characters
        /// </summary>
        /// <param name="chars">One or more characters.</param>
        public void Consume(params char[] chars)
        {
            while (this.HasMore && chars.Contains(this.Current))
                ++this.Index;
        }

        /// <summary>
        /// Consume all characters until the specified one have been found.
        /// </summary>
        /// <param name="delimiter">Stop when the current character is this one</param>
        /// <returns>New offset.</returns>
        public int ConsumeUntil(char delimiter)
        {
            if (this.EndOfFile)
                return this.Index;

            var startIndex = this.Index;

            while (true)
            {
                if (this.EndOfFile)
                {
                    this.Index = startIndex;
                    return this.Index;
                }

                if (this.Current == delimiter)
                    return this.Index;

                // Delimiter is not new line and we got one.
                if (delimiter != '\r' && delimiter != '\n' && this.Current == '\r' || this.Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + this.GetString(startIndex, this.Index) +
                                                        "[CRLF].");

                ++this.Index;
            }
        }

        /// <summary>
        /// Consumes horizontal white spaces (space and tab).
        /// </summary>
        public void ConsumeWhiteSpaces()
        {
            this.Consume(' ', '\t');
        }

        /// <summary>
        /// Consume horizontal white spaces and the specified character.
        /// </summary>
        /// <param name="extraCharacter">Extra character to consume</param>
        public void ConsumeWhiteSpaces(char extraCharacter)
        {
            this.Consume(' ', '\t', extraCharacter);
        }

        /// <summary>
        /// Checks if one of the remaining bytes are a specified character.
        /// </summary>
        /// <param name="ch">Character to find.</param>
        /// <returns>
        /// 	<c>true</c> if found; otherwise <c>false</c>.
        /// </returns>
        public bool Contains(char ch)
        {
            var index = this.Index;
            while (this.Current != ch && this.HasMore)
                ++this.Index;
            var found = this.Current == ch;
            this.Index = index;
            return found;
        }

        /// <summary>
        /// Read a character.
        /// </summary>
        /// <returns>
        /// Character if not EOF; otherwise <c>null</c>.
        /// </returns>
        public char Read()
        {
            return (char) this.reader.Buffer[this.Index++];
        }

        /// <summary>
        /// Get a text line. 
        /// </summary>
        /// <returns></returns>
        /// <remarks>Will merge multi line headers and rewind of end of line was not found.</remarks> 
        public string ReadLine()
        {
            var startIndex = this.Index;
            while (this.HasMore && this.Current != '\n' && this.Current != '\r')
                this.Consume();


            // EOF? Then we havent enough bytes.
            if (this.EndOfFile)
            {
                this.Index = startIndex;
                return null;
            }

            var thisLine = this.encoding.GetString(this.reader.Buffer, startIndex, this.Index - startIndex);

            // \r\n
            if (this.Current == '\r')
                this.Consume();
            if (this.Current == '\n')
                this.Consume();

            // Multi line message?
            if (this.Current == '\t' || this.Current == ' ')
            {
                this.Consume();
                var extra = this.ReadLine();

                // Multiline isn't complete, wait for more bytes.
                if (extra == null)
                {
                    this.Index = startIndex;
                    return null;
                }

                return thisLine + " " + extra.TrimStart(' ', '\t');
            }

            return thisLine;
        }

        /// <summary>
        /// Read quoted string
        /// </summary>
        /// <returns>string if current character (in buffer) is a quote; otherwise <c>null</c>.</returns>
        public string ReadQuotedString()
        {
            if (this.Current != '\"')
                return null;

            this.Consume();
            var startPos = this.Index;
            while (this.HasMore)
            {
                switch (this.Current)
                {
                    case '\\':
                        if (this.Peek == '"') // escaped quote
                        {
                            this.Consume();
                            this.Consume();
                            continue;
                        }
                        break;
                    case '"':
                        var value = this.encoding.GetString(this.reader.Buffer, startPos, this.Index - startPos);
                        ++this.Index; // skip qoute
                        return value;
                    default:
                        this.Consume();
                        break;
                }
            }

            this.Index = startPos;
            return null;
        }

        /// <summary>
        /// Read until end of string, or to one of the delimiters are found.
        /// </summary>
        /// <param name="delimiters">characters to stop at</param>
        /// <returns>
        /// A string (can be <see cref="string.Empty"/>).
        /// </returns>
        /// <remarks>
        /// Will not consume the delimiter.
        /// </remarks>
        /// <exception cref="InvalidOperationException"><c>InvalidOperationException</c>.</exception>
        public string ReadToEnd(string delimiters)
        {
            if (this.EndOfFile)
                return string.Empty;

            var startIndex = this.Index;

            var isDelimitersNewLine = delimiters.IndexOfAny(new[] {'\r', '\n'}) != -1;
            while (true)
            {
                if (this.EndOfFile)
                    return this.GetString(startIndex, this.Index);

                if (delimiters.IndexOf(this.Current) != -1)
                    return this.GetString(startIndex, this.Index, true);

                // Delimiter is not new line and we got one.
                if (isDelimitersNewLine && this.Current == '\r' || this.Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + this.GetString(startIndex, this.Index) +
                                                        "[CRLF].");

                ++this.Index;
            }
        }

        /// <summary>
        /// Read until end of string, or to one of the delimiters are found.
        /// </summary>
        /// <returns>A string (can be <see cref="string.Empty"/>).</returns>
        /// <remarks>
        /// Will not consume the delimiter.
        /// </remarks>
        public string ReadToEnd()
        {
            var str = this.encoding.GetString(this.reader.Buffer, this.Index, this.RemainingLength);
            this.Index = this.position;
            return str;
        }

        /// <summary>
        /// Read to end of buffer, or until specified delimiter is found.
        /// </summary>
        /// <param name="delimiter">Delimiter to find.</param>
        /// <returns>
        /// A string (can be <see cref="string.Empty"/>).
        /// </returns>
        /// <remarks>
        /// Will not consume the delimiter.
        /// </remarks>
        /// <exception cref="InvalidOperationException"><c>InvalidOperationException</c>.</exception>
        public string ReadToEnd(char delimiter)
        {
            if (this.EndOfFile)
                return string.Empty;

            var startIndex = this.Index;

            while (true)
            {
                if (this.EndOfFile)
                    return this.GetString(startIndex, this.Index);

                if (this.Current == delimiter)
                    return this.GetString(startIndex, this.Index, true);

                // Delimiter is not new line and we got one.
                if (delimiter != '\r' && delimiter != '\n' && this.Current == '\r' || this.Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + this.GetString(startIndex, this.Index) +
                                                        "[CRLF].");

                ++this.Index;
            }
        }

        /// <summary>
        /// Will read until specified delimiter is found.
        /// </summary>
        /// <param name="delimiter">Character to stop at.</param>
        /// <returns>
        /// A string if the delimiter was found; otherwise <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Will trim away spaces and tabs from the end.</remarks>
        /// <exception cref="InvalidOperationException"><c>InvalidOperationException</c>.</exception>
        public string ReadUntil(char delimiter)
        {
            if (this.EndOfFile)
                return null;

            var startIndex = this.Index;

            while (true)
            {
                if (this.EndOfFile)
                {
                    this.Index = startIndex;
                    return null;
                }

                if (this.Current == delimiter)
                    return this.GetString(startIndex, this.Index, true);

                // Delimiter is not new line and we got one.
                if (delimiter != '\r' && delimiter != '\n' && this.Current == '\r' || this.Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + this.GetString(startIndex, this.Index) +
                                                        "[CRLF].");

                ++this.Index;
            }
        }

        /// <summary>
        /// Read until one of the delimiters are found.
        /// </summary>
        /// <param name="delimiters">characters to stop at</param>
        /// <returns>
        /// A string if one of the delimiters was found; otherwise <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Will not consume the delimiter.
        /// </remarks>
        /// <exception cref="InvalidOperationException"><c>InvalidOperationException</c>.</exception>
        public string ReadUntil(string delimiters)
        {
            if (this.EndOfFile)
                return null;

            var startIndex = this.Index;

            var isDelimitersNewLine = delimiters.IndexOfAny(new[] {'\r', '\n'}) != -1;
            while (true)
            {
                if (this.EndOfFile)
                {
                    this.Index = startIndex;
                    return null;
                }

                if (delimiters.IndexOf(this.Current) != -1)
                    return this.GetString(startIndex, this.Index, true);

                // Delimiter is not new line and we got one.
                if (isDelimitersNewLine && this.Current == '\r' || this.Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + this.GetString(startIndex, this.Index) +
                                                        "[CRLF].");

                ++this.Index;
            }
        }

        /// <summary>
        /// Read until a horizontal white space occurs.
        /// </summary>
        /// <returns>A string if a white space was found; otherwise <c>null</c>.</returns>
        public string ReadWord()
        {
            return this.ReadUntil(" \t");
        }

        /// <summary>
        /// Assigns the slice to read from
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="count"> </param>
        public void Assign(IBufferSlice buffer, int count)
        {
            this.reader = buffer;
        }

        #endregion

        /// <summary>
        /// Assign a new buffer
        /// </summary>
        /// <param name="buffer">Buffer to process.</param>
        /// <param name="offset">Where to start process the buffer</param>
        /// <param name="count">Number if bytes to read</param>
        /// <exception cref="ArgumentException">Buffer needs to be a byte array</exception>
        public void Assign(byte[] buffer, int offset, int count)
        {
            this.reader = new BufferSlice(buffer, offset, count);
        }

        private string GetString(int startIndex, int endIndex)
        {
            return this.encoding.GetString(this.reader.Buffer, startIndex, endIndex - startIndex);
        }

        private string GetString(int startIndex, int endIndex, bool trimEnd)
        {
            if (trimEnd)
            {
                --endIndex; // need to move one back to be able to trim.
                while (endIndex > 0 && this.reader.Buffer[endIndex] == ' ' || this.reader.Buffer[endIndex] == '\t')
                    --endIndex;
                ++endIndex;
            }
            return this.encoding.GetString(this.reader.Buffer, startIndex, endIndex - startIndex);
        }
    }
}