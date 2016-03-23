using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text;
using Griffin.Networking.Buffers;
using Griffin.Networking.Protocol.Http.Implementation;
using Griffin.Networking.Protocol.Http.Protocol;
using Griffin.Networking.Messaging;

namespace Griffin.Networking.Protocol.Http
{
    /// <summary>
    /// Builds HTTP messags from incoming bytes.
    /// </summary>
    public class HttpMessageBuilder : IMessageBuilder, IDisposable
    {
        private IBufferSliceStack stack;
        private readonly IBufferSlice bodySlice;
        private readonly HttpHeaderParser headerParser = new HttpHeaderParser();
        private readonly ConcurrentQueue<IMessage> messages = new ConcurrentQueue<IMessage>();
        private int bodyBytestLeft;
        private Stream bodyStream;
        private IMessage message;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpMessageBuilder" /> class.
        /// </summary>
        /// <param name="stack">Slices are used when processing incoming data.</param>
        /// <example>
        /// <code>
        /// var builder = new HttpMessageBuilder(new BufferSliceStack(100, 65535)); 
        /// </code>
        /// </example>
        public HttpMessageBuilder(IBufferSliceStack stack)
        {
            this.stack = stack;
            this.headerParser.HeaderParsed += this.OnHeader;
            this.headerParser.Completed += this.OnHeaderComplete;
            this.headerParser.RequestLineParsed += this.OnRequestLine;
            this.bodySlice = this.stack.Pop();
        }

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            this.stack.Push(this.bodySlice);
        }

        #endregion

        #region IMessageBuilder Members

        /// <summary>
        /// Append more bytes to your message building
        /// </summary>
        /// <param name="reader">Contains bytes which was received from the other end</param>
        /// <returns><c>true</c> if a complete message has been built; otherwise <c>false</c>.</returns>
        /// <remarks>You must handle/read everything which is available in the buffer</remarks>
        public bool Append(IBufferReader reader)
        {
            this.headerParser.Parse(reader);
            if (this.bodyBytestLeft > 0)
            {
                var bytesToRead = Math.Min(reader.RemainingLength, this.bodyBytestLeft);
                reader.CopyTo(this.bodyStream, bytesToRead);
                this.bodyBytestLeft -= bytesToRead;

                if (this.bodyBytestLeft == 0)
                {
                    this.bodyStream.Position = 0;
                    this.messages.Enqueue(this.message);
                    this.message = null;
                }

                if (reader.RemainingLength > 0)
                {
                    this.headerParser.Parse(reader);
                }
            }

            return this.messages.Count > 0;
        }

        /// <summary>
        /// Try to dequeue a message
        /// </summary>
        /// <param name="message">Message that the builder has built.</param>
        /// <returns><c>true</c> if a message was available; otherwise <c>false</c>.</returns>
        bool IMessageBuilder.TryDequeue(out object message)
        {
            IMessage msg;
            var result = this.messages.TryDequeue(out msg);
            message = msg;
            return result;
        }

        /// <summary>
        /// Reset builder state
        /// </summary>
        public void Reset()
        {
            this.bodyBytestLeft = 0;
            this.headerParser.Reset();

            IMessage message;
            while (this.messages.TryDequeue(out message))
            {
                
            }
        }

        /// <summary>
        /// Try to dequeue a message
        /// </summary>
        /// <param name="message">Message that the builder has built.</param>
        /// <returns><c>true</c> if a message was available; otherwise <c>false</c>.</returns>
        public bool TryDequeue(out IMessage message)
        {
            IMessage msg;
            var result = this.messages.TryDequeue(out msg);
            message = msg;
            return result;
        }


        #endregion

        private void OnRequestLine(object sender, RequestLineEventArgs e)
        {
            this.message = new HttpRequest(e.Verb, e.Url, e.HttpVersion);
        }

        private void OnHeaderComplete(object sender, EventArgs e)
        {
            this.bodyBytestLeft = this.message.ContentLength;
            if (this.message.ContentLength == 0)
            {
                this.messages.Enqueue(this.message);
                this.message = null;
                return;
            }

            if (this.message.ContentLength > this.bodySlice.Count)
                this.bodyStream = new FileStream(Path.GetTempFileName(), FileMode.Create);
            else
                this.bodyStream = new SliceStream(this.bodySlice);

            this.message.Body = this.bodyStream;
        }

        private void OnHeader(object sender, HeaderEventArgs e)
        {
            this.message.AddHeader(e.Name, e.Value);
        }

    }
}