using System;
using Griffin.Networking.Buffers;

namespace Griffin.Networking.Pipelines.Messages
{
    /// <summary>
    /// Send a slice 
    /// </summary>
    public class SendSlice : IPipelineMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SendSlice"/> class.
        /// </summary>
        /// <param name="slice">The slice.</param>
        /// <param name="length">Number of bytes written to the slice.</param>
        public SendSlice(IBufferSlice slice, int length)
        {
            this.Length = length;
            if (slice == null)
                throw new ArgumentNullException("slice");

            this.Slice = slice;
        }

        /// <summary>
        /// Gets buffer slice to send.
        /// </summary>
        public IBufferSlice Slice { get; private set; }

        /// <summary>
        /// Gets number of bytes written to the slice
        /// </summary>
        public int Length { get; private set; }
    }
}