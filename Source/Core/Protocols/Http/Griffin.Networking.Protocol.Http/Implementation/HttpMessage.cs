﻿using System;
using System.IO;
using System.Text;
using Griffin.Networking.Protocol.Http.Protocol;

namespace Griffin.Networking.Protocol.Http.Implementation
{
    /// <summary>
    /// Base class for HTTP messages
    /// </summary>
    public class HttpMessage : IMessage
    {
        private readonly HttpHeaderCollection headers = new HttpHeaderCollection();
        private int contentLength;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpMessage"/> class.
        /// </summary>
        public HttpMessage()
        {
            this.ContentEncoding = Encoding.UTF8;
            this.ProtocolVersion = "HTTP/1.1";
        }

        #region IMessage Members

        /// <summary>
        /// Gets current protocol version
        /// </summary>
        /// <value>
        /// Default is HTTP/1.1
        /// </value>
        public string ProtocolVersion { get; set; }

        /// <summary>
        /// Gets or sets body stream (null per default unless it's a request where a body where sent)
        /// </summary>
        public Stream Body { get; set; }

        /// <summary>
        /// Gets number of bytes in the body
        /// </summary>
        public int ContentLength
        {
            get
            {
                if (this.contentLength == 0 && this.Body != null)
                    return (int) this.Body.Length;
                return this.contentLength;
            }
            set { this.contentLength = value; }
        }

        /// <summary>
        /// Gets or sets content encoding
        /// </summary>
        /// <remarks>Appended to the contentType header as "charset" parameter.</remarks>
        /// <value>Default is UTF8</value>
        public Encoding ContentEncoding { get; set; }

        /// <summary>
        /// Gets headers.
        /// </summary>
        public IHeaderCollection Headers
        {
            get { return this.headers; }
        }

        /// <summary>
        /// Add a new header
        /// </summary>
        /// <param name="name">Name of the header</param>
        /// <param name="value">Value</param>
        /// <remarks>Adding a header which already exists will just append the value to that header.</remarks>
        public virtual void AddHeader(string name, string value)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (value == null) throw new ArgumentNullException("value");

            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                this.ParseContentEncoding(value);
            }
            this.headers.Add(name, value);
        }

        /// <summary>
        /// Set or replace an existing header
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetHeader(string name, string value)
        {
            this.headers.Set(name, value);
        }

        private void ParseContentEncoding(string value)
        {
            var pos = value.ToLower().IndexOf("charset=");
            if (pos != -1)
            {
                pos += 8;
                var endPos = value.IndexOf(";", pos + 1);
                var encoding = endPos == -1 ? value.Substring(pos) : value.Substring(pos, endPos - pos);
                encoding = encoding.ToUpper();
                this.ContentEncoding = Encoding.GetEncoding(encoding.ToUpper());
            }
        }

        #endregion
    }
}