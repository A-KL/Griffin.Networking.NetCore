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
            parserMethod = FirstLine;
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
            while ((theByte = Read(reader)) != -1)
            {
                var ch = (char) theByte;
                logger.Trace(parserMethod.GetMethodInfo().Name + ": " + ch);
                parserMethod(ch);
                if (isCompleted)
                    break;

            }

            isCompleted = false;
        }

        private int Read(IBufferReader reader)
        {
            if (lookAhead != char.MinValue)
            {
                var tmp = lookAhead;
                lookAhead = char.MinValue;
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
                var line = headerName.ToString().Split(' ');
                if (line.Length != 3)
                    throw new BadRequestException("First line is not a valid REQUEST/RESPONSE line: " + headerName);

                if (line[2].ToLower().StartsWith("http"))
                    RequestLineParsed(this, new RequestLineEventArgs(line[0], line[1], line[2]));
                else
                {
                    throw new NotSupportedException("Not supporting response parsing yet.");
                }

                headerName.Clear();
                parserMethod = Name_StripWhiteSpacesBefore;
                return;
            }

            headerName.Append(ch);
        }

        private void Name_StripWhiteSpacesBefore(char ch)
        {
            if (IsHorizontalWhitespace(ch))
                return;

            parserMethod = Name_ParseUntilComma;
            lookAhead = ch;
        }

        private void Name_ParseUntilComma(char ch)
        {
            if (ch == ':')
            {
                parserMethod = Value_StripWhitespacesBefore;
                return;
            }

            headerName.Append(ch);
        }

        private void Value_StripWhitespacesBefore(char ch)
        {
            if (IsHorizontalWhitespace(ch))
                return;

            parserMethod = Value_ParseUntilQouteOrNewLine;
            lookAhead = ch;
        }

        private void Value_ParseUntilQouteOrNewLine(char ch)
        {
            if (ch == '"')
            {
                parserMethod = Value_ParseQuoted;
                return;
            }

            if (ch == '\r')
                return;
            if (ch == '\n')
            {
                parserMethod = Value_CompletedOrMultiLine;
                return;
            }

            headerValue.Append(ch);
        }

        private void Value_ParseQuoted(char ch)
        {
            if (ch == '"')
            {
                // exited the quouted string
                parserMethod = Value_ParseUntilQouteOrNewLine;
                return;
            }

            headerValue.Append(ch);
        }

        private void Value_CompletedOrMultiLine(char ch)
        {
            if (IsHorizontalWhitespace(ch))
            {
                parserMethod = Value_StripWhitespacesBefore;
                return;
            }
            if (ch == '\r')
                return; //empty line

            args.Set(headerName.ToString(), headerValue.ToString());
            HeaderParsed(this, args);
            ResetLineParsing();
            parserMethod = Name_StripWhiteSpacesBefore;

            if (ch == '\n')
            {
                //Header completed
                TriggerHeaderCompleted();
                return;
            }


            lookAhead = ch;
        }

        private void TriggerHeaderCompleted()
        {
            isCompleted = true;
            Completed(this, EventArgs.Empty);
            Reset();
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
            ResetLineParsing();
            parserMethod = FirstLine;
        }

        protected void ResetLineParsing()
        {
            headerName.Clear();
            headerValue.Clear();
        }


    }
}