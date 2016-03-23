using System;
using System.Net;
using Griffin.Networking.Protocol.Http.Implementation;
using Griffin.Networking.Protocol.Http.Pipeline.Messages;
using Griffin.Networking.Protocol.Http.Protocol;
using Griffin.Networking.Pipelines;
using Griffin.Networking.Pipelines.Messages;

namespace Griffin.Networking.Protocol.Http.Pipeline.Handlers
{
    /// <summary>
    /// Parses the HTTP header and passes on a constructed message
    /// </summary>
    public class HeaderDecoder : IUpstreamHandler
    {
        private readonly HttpHeaderParser _headerParser;
        private int _bodyBytesLeft = -1;
        private bool _headerCompleted;
        private IMessage _message;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderDecoder"/> class.
        /// </summary>
        public HeaderDecoder()
        {
            this._headerParser = new HttpHeaderParser();
            this._headerParser.HeaderParsed += this.OnHeader;
            this._headerParser.Completed += this.OnHeaderCompleted;
            this._headerParser.RequestLineParsed += this.OnRequestLine;
        }

        #region IUpstreamHandler Members

        /// <summary>
        /// Handle an message
        /// </summary>
        /// <param name="context">Context unique for this handler instance</param>
        /// <param name="message">Message to process</param>
        public void HandleUpstream(IPipelineHandlerContext context, IPipelineMessage message)
        {
            if (message is Closed)
            {
                this._bodyBytesLeft = 0;
                this._headerParser.Reset();
            }
            else if (message is Received)
            {
                var msg = (Received) message;

                // complete the body
                if (this._bodyBytesLeft > 0)
                {
                    var bytesToSend = Math.Min(this._bodyBytesLeft, msg.BufferReader.RemainingLength);
                    this._bodyBytesLeft -= bytesToSend;
                    context.SendUpstream(message);
                    return;
                }

                this._headerParser.Parse(msg.BufferReader);
                if (this._headerCompleted)
                {
                    var request = (IRequest) this._message;

                    var ourRequest = this._message as HttpRequest;
                    if (ourRequest != null)
                        ourRequest.RemoteEndPoint = msg.RemoteEndPoint as IPEndPoint;
                    request.AddHeader("RemoteAddress", msg.RemoteEndPoint.ToString());

                    var receivedHttpRequest = new ReceivedHttpRequest(request);

                    this._headerParser.Reset();
                    this._headerCompleted = false;

                    context.SendUpstream(receivedHttpRequest);
                    if (msg.BufferReader.RemainingLength > 0)
                        context.SendUpstream(msg);
                }

                return;
            }

            context.SendUpstream(message);
        }

        #endregion

        private void OnRequestLine(object sender, RequestLineEventArgs e)
        {
            this._message = new HttpRequest(e.Verb, e.Url, e.HttpVersion);
        }

        private void OnHeaderCompleted(object sender, EventArgs e)
        {
            this._bodyBytesLeft = this._message.ContentLength;
            this._headerCompleted = true;
        }

        private void OnHeader(object sender, HeaderEventArgs e)
        {
            this._message.AddHeader(e.Name, e.Value);
        }
    }
}