using Griffin.Networking.Logging;

namespace Griffin.Networking.Pipelines
{
    /// <summary>
    /// Context for downstream handlers.
    /// </summary>
    /// <remarks>Each context is unique for a handler in a channel.</remarks>
    internal class PipelineUpstreamContext : IPipelineHandlerContext
    {
        private readonly ILogger logger = LogManager.GetLogger<PipelineUpstreamContext>();
        private readonly IUpstreamHandler myHandler;
        private readonly IPipeline pipeline;
        private PipelineUpstreamContext nextHandler;

        public PipelineUpstreamContext(IPipeline pipeline, IUpstreamHandler myHandler)
        {
            this.pipeline = pipeline;
            this.myHandler = myHandler;
        }

        public PipelineUpstreamContext NextHandler
        {
            set { nextHandler = value; }
        }

        #region IPipelineHandlerContext Members

        public void SendUpstream(IPipelineMessage message)
        {
            if (nextHandler != null)
            {
                logger.Trace("Up: " + myHandler.ToStringOrClassName() + " sends message " +
                              message.ToStringOrClassName());
                nextHandler.Invoke(message);
            }
            else
            {
                logger.Warning("Up: " + myHandler.ToStringOrClassName() + " tried to send message " +
                                message.ToStringOrClassName() + ", but there are no handler upstream.");
            }
        }


        public void SendDownstream(IPipelineMessage message)
        {
            logger.Trace("Down: " + myHandler.ToStringOrClassName() + " sends " + message.ToStringOrClassName());
            pipeline.SendDownstream(message);
        }

        #endregion

        public void Invoke(IPipelineMessage message)
        {
            myHandler.HandleUpstream(this, message);
        }

        public override string ToString()
        {
            return myHandler.ToString();
        }
    }
}