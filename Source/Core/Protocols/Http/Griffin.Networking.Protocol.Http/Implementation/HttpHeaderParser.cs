using System;
using System.Reflection;
using System.Text;
using Griffin.Networking.Buffers;
using Griffin.Networking.Logging;

namespace Griffin.Networking.Protocol.Http.Implementation
{
    /// <summary>
    /// Parser for the HTTP header
    /// </summary>
    /// <remarks>Parses everything in the header including the seperator line between the header and body. i.e. The next available byte
    /// in the buffer is the first body byte.</remarks>
    public class HttpHeaderParser
    {
        private readonly HeaderEventArgs args = new HeaderEventArgs();
        private readonly StringBuilder headerName = new StringBuilder();
        private readonly StringBuilder headerValue = new StringBuilder();
        private char lookAhead;
        private Action<char> parserMethod;
        private ILogger logger = LogManager.GetLogger<HttpHeaderParser>();
        private bool isCompleted;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpHeaderParser" /> class.
        /// </summary>
        public HttpHeaderParser()
        {
            this.parserMethod = this.FirstLine;
        }

        /// <summary>
        /// Will try to parse everything in the buffer
        /// </summary>
        /// <param name="reader">Reader to read from.</param>
        /// <remarks><para>Do note that the parser is for the header only. The <see cref="Completed"/> event will
        /// indicate that there might be body bytes left in the buffer. You have to handle them by yourself.</para></remarks>
        public void Parse(IBufferReader reader)
        {
            var theByte = 0;
            while ((theByte = this.Read(reader)) != -1)
            {
                var ch = (char) theByte;
                this.logger.Trace(this.parserMethod.GetMethodInfo().Name + ": " + ch);
                this.parserMethod(ch);
                if (this.isCompleted)
                    break;

            }

            this.isCompleted = false;
        }

        private int Read(IBufferReader reader)
        {
            if (this.lookAhead != char.MinValue)
            {
                var tmp = this.lookAhead;
                this.lookAhead = char.MinValue;
                return tmp;
            }

            return reader.Read();
        }

        private void FirstLine(char ch)
        {
            if (ch == '\r')
                return;
            if (ch == '\n')
            {
                var line = this.headerName.ToString().Split(' ');
                if (line.Length != 3)
                    throw new BadRequestException("First line is not a valid REQUEST/RESPONSE line: " + this.headerName);

                if (line[2].ToLower().StartsWith("http"))
                    this.RequestLineParsed(this, new RequestLineEventArgs(line[0], line[1], line[2]));
                else
                {
                    throw new NotSupportedException("Not supporting response parsing yet.");
                }

                this.headerName.Clear();
                this.parserMethod = this.Name_StripWhiteSpacesBefore;
                return;
            }

            this.headerName.Append(ch);
        }

        private void Name_StripWhiteSpacesBefore(char ch)
        {
            if (this.IsHorizontalWhitespace(ch))
                return;

            this.parserMethod = this.Name_ParseUntilComma;
            this.lookAhead = ch;
        }

        private void Name_ParseUntilComma(char ch)
        {
            if (ch == ':')
            {
                this.parserMethod = this.Value_StripWhitespacesBefore;
                return;
            }

            this.headerName.Append(ch);
        }

        private void Value_StripWhitespacesBefore(char ch)
        {
            if (this.IsHorizontalWhitespace(ch))
                return;

            this.parserMethod = this.Value_ParseUntilQouteOrNewLine;
            this.lookAhead = ch;
        }

        private void Value_ParseUntilQouteOrNewLine(char ch)
        {
            if (ch == '"')
            {
                this.parserMethod = this.Value_ParseQuoted;
                return;
            }

            if (ch == '\r')
                return;
            if (ch == '\n')
            {
                this.parserMethod = this.Value_CompletedOrMultiLine;
                return;
            }

            this.headerValue.Append(ch);
        }

        private void Value_ParseQuoted(char ch)
        {
            if (ch == '"')
            {
                // exited the quouted string
                this.parserMethod = this.Value_ParseUntilQouteOrNewLine;
                return;
            }

            this.headerValue.Append(ch);
        }

        private void Value_CompletedOrMultiLine(char ch)
        {
            if (this.IsHorizontalWhitespace(ch))
            {
                this.parserMethod = this.Value_StripWhitespacesBefore;
                return;
            }
            if (ch == '\r')
                return; //empty line

            this.args.Set(this.headerName.ToString(), this.headerValue.ToString());
            this.HeaderParsed(this, this.args);
            this.ResetLineParsing();
            this.parserMethod = this.Name_StripWhiteSpacesBefore;

            if (ch == '\n')
            {
                //Header completed
                this.TriggerHeaderCompleted();
                return;
            }


            this.lookAhead = ch;
        }

        private void TriggerHeaderCompleted()
        {
            this.isCompleted = true;
            this.Completed(this, EventArgs.Empty);
            this.Reset();
        }

        /// <summary>
        /// The header part of the request/response has been parsed successfully. The remaining bytes is for the body
        /// </summary>
        public event EventHandler Completed = delegate { };

        private bool IsHorizontalWhitespace(char ch)
        {
            return ch == ' ' || ch == '\t';
        }

        /// <summary>
        /// We've parsed a header and it's value.
        /// </summary>
        public event EventHandler<HeaderEventArgs> HeaderParsed = delegate { };

        /// <summary>
        /// We've parsed a request line, meaning that all headers is for a HTTP Request.
        /// </summary>
        public event EventHandler<RequestLineEventArgs> RequestLineParsed = delegate { };

        /// <summary>
        /// Reset parser state
        /// </summary>
        public void Reset()
        {
            this.ResetLineParsing();
            this.parserMethod = this.FirstLine;
        }

        protected void ResetLineParsing()
        {
            this.headerName.Clear();
            this.headerValue.Clear();
        }


    }
}