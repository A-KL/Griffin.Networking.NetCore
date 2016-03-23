using System;
using System.Collections.Generic;

namespace Griffin.Networking.Pipelines
{
    /// <summary>
    /// Uses a <see cref="IServiceLocator"/> to build all handlers.
    /// </summary>
    /// <remarks>
    /// You must register all handlers in your service locator if the service locator can't build unregistered components.
    /// </remarks>
    public class ServiceLocatorPipelineFactory : IPipelineFactory
    {
        private readonly LinkedList<HandlerInformation<IDownstreamHandler>> downstreamHandlers =
            new LinkedList<HandlerInformation<IDownstreamHandler>>();

        private readonly IServiceLocator serviceLocator;

        private readonly LinkedList<HandlerInformation<IUpstreamHandler>> uptreamHandlers =
            new LinkedList<HandlerInformation<IUpstreamHandler>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceLocatorPipelineFactory"/> class.
        /// </summary>
        /// <param name="serviceLocator">The service locator.</param>
        public ServiceLocatorPipelineFactory(IServiceLocator serviceLocator)
        {
            this.serviceLocator = serviceLocator;
        }

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
                if (handler.HandlerType != null)
                    pipeline.AddUpstreamHandler((IUpstreamHandler) serviceLocator.Resolve(handler.HandlerType));
                else
                    pipeline.AddUpstreamHandler(handler.Handler);
            }
            foreach (var handler in downstreamHandlers)
            {
                if (handler.HandlerType != null)
                    pipeline.AddDownstreamHandler((IDownstreamHandler) serviceLocator.Resolve(handler.HandlerType));
                else
                    pipeline.AddDownstreamHandler(handler.Handler);
            }
            return pipeline;
        }

        #endregion

        /// <summary>
        /// Add another handler.
        /// </summary>
        /// <typeparam name="T">Handler type. Must implement <see cref="IDownstreamHandler"/> or <see cref="IUpstreamHandler"/></typeparam>
        public void AddDownstreamHandler<T>() where T : IPipelineHandler
        {
            downstreamHandlers.AddLast(new HandlerInformation<IDownstreamHandler>(typeof (T)));
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
        /// <typeparam name="T">Handler type. Must implement <see cref="IDownstreamHandler"/> or <see cref="IUpstreamHandler"/></typeparam>
        public void AddUpstreamHandler<T>() where T : IPipelineHandler
        {
            uptreamHandlers.AddLast(new HandlerInformation<IUpstreamHandler>(typeof (T)));
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
            public HandlerInformation(Type handlerType)
            {
                HandlerType = handlerType;
            }

            public HandlerInformation(T handler)
            {
                Handler = handler;
            }

            public T Handler { get; private set; }

            public Type HandlerType { get; private set; }
        }

        #endregion
    }
}