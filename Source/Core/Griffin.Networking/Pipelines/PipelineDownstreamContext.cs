using Griffin.Networking.Logging;

namespace Griffin.Networking.Pipelines
{
    /// <summary>
    /// Context for a downstream (from channel to client) handler
    /// </summary>
    internal class PipelineDownstreamContext : IPipelineHandlerContext
    {
        private readonly ILogger logger = LogManager.GetLogger<PipelineDownstreamContext>();
        private readonly IDownstreamHandler myHandler;
        private readonly IPipeline pipeline;
        private PipelineDownstreamContext nextHandler;

        public PipelineDownstreamContext(IPipeline pipeline, IDownstreamHandler myHandler)
        {
            this.pipeline = pipeline;
            this.myHandler = myHandler;
        }

        public PipelineDownstreamContext NextHandler
        {
            set { nextHandler = value; }
        }

        #region IPipelineHandlerContext Members

        public void SendDownstream(IPipelineMessage message)
        {
            if (nextHandler != null)
            {
                logger.Trace("Down: " + myHandler.ToStringOrClassName() + " is passing on message");
                nextHandler.Invoke(message);
            }
            else
            {
                logger.Warning("Down: " + myHandler.ToStringOrClassName() +
                                " tried to send message, but there are no more handlers.");
            }
        }


        public void SendUpstream(IPipelineMessage message)
        {
            logger.Trace("Up: " + myHandler.ToStringOrClassName() + " is sending " + message.ToStringOrClassName());
            pipeline.SendUpstream(message);
        }

        #endregion

        public void Invoke(IPipelineMessage message)
        {
            logger.Trace("Down: Invoking " + myHandler.ToStringOrClassName() + " with msg " +
                          message.ToStringOrClassName());
            myHandler.HandleDownstream(this, message);
        }

        public override string ToString()
        {
            return myHandler.ToString();
        }
    }
}