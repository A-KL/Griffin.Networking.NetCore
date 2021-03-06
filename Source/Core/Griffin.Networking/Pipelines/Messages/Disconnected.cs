using System;

namespace Griffin.Networking.Pipelines.Messages
{
    /// <summary>
    /// A channel have been disconnected by remote peer
    /// </summary>
    public class Disconnected : IPipelineMessage
    {
        private readonly Exception exception;

        /// <summary>
        /// Initializes a new instance of the <see cref="Disconnected"/> class.
        /// </summary>
        /// <param name="exception">NULL = graceful disconnect.</param>
        public Disconnected(Exception exception)
        {
            this.exception = exception;
        }

        /// <summary>
        /// Gets thrown exception (unless we got disconnected gracefully)
        /// </summary>
        public Exception Exception
        {
            get { return this.exception; }
        }
    }
}