using System;
using System.Collections.Generic;

namespace Griffin.Networking.Pipelines
{
    /// <summary>
    /// Uses delegates to created scoped handlers
    /// </summary>
    public class DelegatePipelineFactory : IPipelineFactory
    {
        private readonly LinkedList<HandlerInformation<IDownstreamHandler>> downstreamHandlers =
            new LinkedList<HandlerInformation<IDownstreamHandler>>();

        private readonly LinkedList<HandlerInformation<IUpstreamHandler>> uptreamHandlers =
            new LinkedList<HandlerInformation<IUpstreamHandler>>();

        #region IPipelineFactory Members

        /// <summary>
        /// Create a pipeline for a channel
        /// </summary>
        /// <returns>Created pipeline</returns>
        public IPipeline Build()
        {
            var pipeline = new Pipeline();

            foreach (var handler in uptreamHandlers)
            {
                if (handler.Factory != null)
                    pipeline.AddUpstreamHandler(handler.Factory());
                else
                    pipeline.AddUpstreamHandler(handler.Handler);
            }
            foreach (var handler in downstreamHandlers)
            {
                if (handler.Factory != null)
                    pipeline.AddDownstreamHandler(handler.Factory());
                else
                    pipeline.AddDownstreamHandler(handler.Handler);
            }
            return pipeline;
        }

        #endregion

        /// <summary>
        /// Add another handler.
        /// </summary>
        /// <param name="factoryMethod">The factory method.</param>
        public void AddDownstreamHandler(Func<IDownstreamHandler> factoryMethod)
        {
            downstreamHandlers.AddLast(new HandlerInformation<IDownstreamHandler>(factoryMethod));
        }

        /// <summary>
        /// Add an handler instance (singleton)
        /// </summary>
        /// <param name="handler">Must implement <see cref="IDownstreamHandler"/> and/or <see cref="IUpstreamHandler"/></param>
        /// <remarks>Same instance will be used for all channels. Use the <see cref="IPipelineHandlerContext"/> to store any context information.</remarks>
        public void AddDownstreamHandler(IDownstreamHandler handler)
        {
            downstreamHandlers.AddLast(new HandlerInformation<IDownstreamHandler>(handler));
        }

        /// <summary>
        /// Add another handler.
        /// </summary>
        /// <param name="factoryMethod">The factory method.</param>
        public void AddUpstreamHandler(Func<IUpstreamHandler> factoryMethod)
        {
            uptreamHandlers.AddLast(new HandlerInformation<IUpstreamHandler>(factoryMethod));
        }

        /// <summary>
        /// Add an handler instance (singleton)
        /// </summary>
        /// <param name="handler">Must implement <see cref="IDownstreamHandler"/> and/or <see cref="IUpstreamHandler"/></param>
        /// <remarks>Same instance will be used for all channels. Use the <see cref="IPipelineHandlerContext"/> to store any context information.</remarks>
        public void AddUpstreamHandler(IUpstreamHandler handler)
        {
            uptreamHandlers.AddLast(new HandlerInformation<IUpstreamHandler>(handler));
        }

        #region Nested type: HandlerInformation

        private class HandlerInformation<T>
        {
            public HandlerInformation(Func<T> factory)
            {
                Factory = factory;
            }

            public HandlerInformation(T handler)
            {
                Handler = handler;
            }

            public T Handler { get; private set; }

            public Func<T> Factory { get; private set; }
        }

        #endregion
    }
}