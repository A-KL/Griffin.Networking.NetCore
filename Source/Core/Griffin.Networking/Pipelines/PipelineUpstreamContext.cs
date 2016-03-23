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
            set { this.nextHandler = value; }
        }

        #region IPipelineHandlerContext Members

        public void SendUpstream(IPipelineMessage message)
        {
            if (this.nextHandler != null)
            {
                this.logger.Trace("Up: " + this.myHandler.ToStringOrClassName() + " sends message " +
                              message.ToStringOrClassName());
                this.nextHandler.Invoke(message);
            }
            else
            {
                this.logger.Warning("Up: " + this.myHandler.ToStringOrClassName() + " tried to send message " +
                                message.ToStringOrClassName() + ", but there are no handler upstream.");
            }
        }


        public void SendDownstream(IPipelineMessage message)
        {
            this.logger.Trace("Down: " + this.myHandler.ToStringOrClassName() + " sends " + message.ToStringOrClassName());
            this.pipeline.SendDownstream(message);
        }

        #endregion

        public void Invoke(IPipelineMessage message)
        {
            this.myHandler.HandleUpstream(this, message);
        }

        public override string ToString()
        {
            return this.myHandler.ToString();
        }
    }
}