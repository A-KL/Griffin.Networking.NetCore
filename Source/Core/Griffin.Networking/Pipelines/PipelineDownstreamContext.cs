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
            set { this.nextHandler = value; }
        }

        #region IPipelineHandlerContext Members

        public void SendDownstream(IPipelineMessage message)
        {
            if (this.nextHandler != null)
            {
                this.logger.Trace("Down: " + this.myHandler.ToStringOrClassName() + " is passing on message");
                this.nextHandler.Invoke(message);
            }
            else
            {
                this.logger.Warning("Down: " + this.myHandler.ToStringOrClassName() +
                                " tried to send message, but there are no more handlers.");
            }
        }


        public void SendUpstream(IPipelineMessage message)
        {
            this.logger.Trace("Up: " + this.myHandler.ToStringOrClassName() + " is sending " + message.ToStringOrClassName());
            this.pipeline.SendUpstream(message);
        }

        #endregion

        public void Invoke(IPipelineMessage message)
        {
            this.logger.Trace("Down: Invoking " + this.myHandler.ToStringOrClassName() + " with msg " +
                          message.ToStringOrClassName());
            this.myHandler.HandleDownstream(this, message);
        }

        public override string ToString()
        {
            return this.myHandler.ToString();
        }
    }
}