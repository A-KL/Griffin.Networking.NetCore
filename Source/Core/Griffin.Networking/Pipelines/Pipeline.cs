﻿using System;
using System.Collections.Generic;
using System.Linq;
using Griffin.Networking.Logging;

namespace Griffin.Networking.Pipelines
{
    /// <summary>
    /// A pipeline is created for one channel only.
    /// </summary>
    /// <remarks>
    ///   <para>
    /// Pipelines are used to transform the information recieved by the socket handler before
    /// it reaches the client. Same thing goes when the client want to send something through the channel.
    ///   </para>
    ///   <para>
    /// You MUST call <see cref="SetChannel(IDownstreamHandler)"/> before using the pipeline, since nothing till handle the messages otherwise (when all downstream handlers are finished).
    ///   </para>
    /// </remarks>
    public class Pipeline : IPipeline, IDownstreamHandler
    {
        private readonly LinkedList<PipelineDownstreamContext> downstreamContexts =
            new LinkedList<PipelineDownstreamContext>();

        private readonly ILogger logger = LogManager.GetLogger<Pipeline>();

        private readonly LinkedList<PipelineUpstreamContext> upstreamContexts =
            new LinkedList<PipelineUpstreamContext>();

        private PipelineDownstreamContext channelContext;
        private IDownstreamHandler downStreamEndPoint;

        #region IDownstreamHandler Members

        /// <summary>
        /// Process message
        /// </summary>
        /// <param name="context">Context information</param>
        /// <param name="message">Message to process</param>
        public void HandleDownstream(IPipelineHandlerContext context, IPipelineMessage message)
        {
            this.downStreamEndPoint.HandleDownstream(context, message);
        }

        #endregion

        #region IPipeline Members

        /// <summary>
        /// Set down stream end point
        /// </summary>
        /// <param name="handler"> </param>
        public void SetChannel(IDownstreamHandler handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            this.downStreamEndPoint = handler;
            this.channelContext = new PipelineDownstreamContext(this, this.downStreamEndPoint);
            this.downstreamContexts.Last.Value.NextHandler = this.channelContext;
        }

        /// <summary>
        /// Send a message from the client and downwards.
        /// </summary>
        /// <param name="message">Message to send to the channel</param>
        public void SendDownstream(IPipelineMessage message)
        {
            this.downstreamContexts.First.Value.Invoke(message);
        }

        /// <summary>
        /// Send something from the channel to all handlers.
        /// </summary>
        /// <param name="message">Message to send to the client</param>
        public void SendUpstream(IPipelineMessage message)
        {
            this.upstreamContexts.First.Value.Invoke(message);
        }

        #endregion

        /// <summary>
        /// Add a new downstream handler 
        /// </summary>
        /// <param name="handler">Handler to add</param>
        /// <remarks>Downstream handlers takes care of everything sent from the client to the channel.</remarks>
        public void AddDownstreamHandler(IDownstreamHandler handler)
        {
            // always link with channel handler (will be replaced when the pipe is filled with more handlers
            // so that only the last handler has the channel as NextHandler).
            var ctx = new PipelineDownstreamContext(this, handler) {NextHandler = this.channelContext};

            var last = this.downstreamContexts.Last;
            if (last != null)
            {
                this.logger.Debug("Added downstream handler " + handler + " and linked it as next handler for " + last.Value);
                last.Value.NextHandler = ctx;
            }
            else
            {
                this.logger.Debug("Added downstream handler " + handler);
            }

            this.downstreamContexts.AddLast(ctx);
        }

        /// <summary>
        /// Add a new upstream handler 
        /// </summary>
        /// <param name="handler">Handler to add</param>
        /// <remarks>Upstream handlers takes care of everything sent from the channel to the client.</remarks>
        public void AddUpstreamHandler(IUpstreamHandler handler)
        {
            var ctx = new PipelineUpstreamContext(this, handler);
            var last = this.upstreamContexts.Last;
            if (last != null)
            {
                this.logger.Debug("Added upstream handler " + handler + " and linked it as next handler for " + last.Value);
                last.Value.NextHandler = ctx;
            }
            else
            {
                this.logger.Debug("Added upstream handler " + handler);
            }


            this.upstreamContexts.AddLast(ctx);
        }


        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            var value = this.downstreamContexts.Aggregate("Downstream [",
                                                      (current, context) => current + (context + ", "));
            value = value.Remove(value.Length - 2, 2);

            value += "], Upstream [";
            value = this.upstreamContexts.Aggregate(value, (current, context) => current + (context + ", "));
            value = value.Remove(value.Length - 2, 2) + "]";

            return value;
        }
    }
}