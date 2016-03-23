using System;
using System.IO;
using System.Net;
using Griffin.Networking.Buffers;
using Griffin.Networking.Protocol.Http.Implementation;
using Griffin.Networking.Protocol.Http.Pipeline.Messages;
using Griffin.Networking.Protocol.Http.Protocol;
using Griffin.Networking.Protocol.Http.Services;
using Griffin.Networking.Pipelines;
using Griffin.Networking.Pipelines.Messages;

namespace Griffin.Networking.Protocol.Http.Pipeline.Handlers
{
    /// <summary>
    /// Can decode bodies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Will not pass on the <see cref="ReceivedHttpRequest"/> message until the body have been parsed successfully.
    /// </para>
    /// <para>
    ///  The decoder uses a BufferPool buffer to host the body contents.The decoder will switch to <see cref="FileStream"/> for bodies larger than the <c>bufferSize</c> contructor parameter.
    /// This will of course hurt performance but keep the memory usage per request down.
    /// </para>
    /// </remarks>
    public class BodyDecoder : IUpstreamHandler
    {
        private IBufferSliceStack _bufferPool;
        private readonly int _bufferSize;
        private readonly IBodyDecoder _decoderService;
        private readonly int _sizeLimit;
        private IMessage _currentMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="BodyDecoder"/> class.
        /// </summary>
        /// <param name="decoderService">The decoder service.</param>
        /// <param name="bufferSize">Buffer size of each buffer in the pool. Read the remarks at <see cref="BodyDecoder"/></param>
        /// <param name="sizeLimit">Maximum size of the body in bytes. Larger content will generate a <see cref="HttpStatusCode.RequestEntityTooLarge"/> response which will
        /// be sent back to the client.</param>
        public BodyDecoder(IBodyDecoder decoderService, int bufferSize, int sizeLimit)
        {
            if (decoderService == null) throw new ArgumentNullException("decoderService");
            this._decoderService = decoderService;
            this._bufferSize = bufferSize;
            this._sizeLimit = sizeLimit;
            this._bufferPool = new BufferSliceStack(1000, bufferSize);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BodyDecoder"/> class.
        /// </summary>
        /// <param name="decoderService">The decoder service.</param>
        /// <param name="bufferSliceStack">Used to provide buffers used when decoding the body</param>
        /// <param name="sizeLimit">Maximum size of the body in bytes. Larger content will generate a <see cref="HttpStatusCode.RequestEntityTooLarge"/> response which will
        /// be sent back to the client.</param>
        public BodyDecoder(IBodyDecoder decoderService, IBufferSliceStack bufferSliceStack, int sizeLimit)
        {
            if (decoderService == null) throw new ArgumentNullException("decoderService");
            if (bufferSliceStack == null) throw new ArgumentNullException("bufferSliceStack");
            this._decoderService = decoderService;
            this._sizeLimit = sizeLimit;
            this._bufferPool = bufferSliceStack;

            var buffer = bufferSliceStack.Pop();
            this._bufferSize = buffer.Count;
            bufferSliceStack.Push(buffer);
        }

        #region IUpstreamHandler Members

        /// <summary>
        /// Handle an message
        /// </summary>
        /// <param name="context">Context unique for this handler instance</param>
        /// <param name="message">Message to process</param>
        /// <remarks>
        /// All messages that can't be handled MUST be send up the chain using <see cref="IPipelineHandlerContext.SendUpstream"/>.
        /// </remarks>
        public void HandleUpstream(IPipelineHandlerContext context, IPipelineMessage message)
        {
            var httpmsg = message as ReceivedHttpRequest;
            if (httpmsg != null)
            {
                if (httpmsg.HttpRequest.ContentLength > this._sizeLimit)
                {
                    var response = httpmsg.HttpRequest.CreateResponse(HttpStatusCode.RequestEntityTooLarge,
                                                                      string.Format("Max body size is {0} bytes.", this._sizeLimit));
                    context.SendDownstream(new SendHttpResponse(httpmsg.HttpRequest, response));
                    return;
                }

                if (httpmsg.HttpRequest.ContentLength == 0)
                {
                    context.SendUpstream(message);
                    return;
                }

                this._currentMessage = httpmsg.HttpRequest;
                return; // don't send the request upwards.
            }

            var msg = message as Received;
            if (msg != null)
            {
                if (this._currentMessage == null)
                    throw new InvalidOperationException("Current message is not set. We have no way of knowing when to stop decoding the body.");

                var result = this.ParseBody(msg.BufferReader);
                if (!result)
                    return;

                this._currentMessage.Body.Position = 0;
                this._decoderService.Decode((IRequest) this._currentMessage);
                context.SendUpstream(new ReceivedHttpRequest((HttpRequest) this._currentMessage));
                this._currentMessage = null;
                return;
            }

            // pass on all other messages
            context.SendUpstream(message);
        }

        #endregion

        /// <summary>
        /// Parser method to copy all body bytes.
        /// </summary>
        /// <param name="reader"> </param>
        /// <returns></returns>
        /// <remarks>Needed since a TCP packet can contain multiple messages
        /// after each other, or partial messages.</remarks>
        private bool ParseBody(IBufferReader reader)
        {
            if (reader.RemainingLength == 0)
                return false;

            if (this._currentMessage.Body == null)
            {
                if (this._currentMessage.ContentLength > this._bufferSize)
                    this._currentMessage.Body =
                        new FileStream(
                            Path.Combine(Path.GetTempPath(), "http." + Guid.NewGuid().ToString("N") + ".tmp"),
                            FileMode.CreateNew);
                else
                {
                    var slice = this._bufferPool.Pop();
                    this._currentMessage.Body = new SliceStream(slice);
                }
            }

            var bytesLeft =
                (int) Math.Min(this._currentMessage.ContentLength - this._currentMessage.Body.Length, reader.RemainingLength);
            reader.CopyTo(this._currentMessage.Body, bytesLeft);
            return this._currentMessage.Body.Length == this._currentMessage.ContentLength;
        }
    }
}