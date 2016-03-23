using System;

namespace Griffin.Networking.Buffers
{
    /// <summary>
    /// We are a slice which should get returned to a pool when done
    /// </summary>
    /// <remarks>It's important that you check if a buffer implement <code>IDisposable</code> since you then have
    /// to invoke <c>Dispose()</c> when done to return the buffer to the pool.
    /// </remarks>
    /// <seealso cref="BufferSliceStack"/>
    public class PooledBufferSlice : IBufferSlice, IDisposable
    {
        private readonly IBufferSliceStack bufferSliceStack;
        private readonly int initialOffset;
        private readonly int initialSize;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledBufferSlice" /> class.
        /// </summary>
        /// <param name="bufferSliceStack">The buffer slice stack.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="count">The count.</param>
        public PooledBufferSlice(IBufferSliceStack bufferSliceStack, byte[] buffer, int offset, int count)
        {
            if (bufferSliceStack == null) throw new ArgumentNullException("bufferSliceStack");
            if (buffer == null) throw new ArgumentNullException("buffer");

            Buffer = buffer;
            Offset = offset;
            Count = count;
            this.bufferSliceStack = bufferSliceStack;
            initialSize = count;
            initialOffset = offset;
        }

        #region IBufferSlice Members

        /// <summary>
        /// Gets buffer that we are working with
        /// </summary>
        public byte[] Buffer { get; private set; }

        /// <summary>
        /// Gets current offset
        /// </summary>
        public int Offset { get; private set; }

        /// <summary>
        /// Gets number of bytes in the buffer
        /// </summary>
        public int Count { get; set; }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (isDisposed)
                throw new InvalidOperationException(
                    "Don't dispose me twice, since it will screw up the stack that I came from.");

            bufferSliceStack.Push(this);
        }

        #endregion

        /// <summary>
        /// Checks if the supplied stack is the one that we came from
        /// </summary>
        /// <param name="stack">Stack to check</param>
        /// <returns><c>>true</c> if our; otherwise <c>false</c>.</returns>
        public bool IsMyStack(IBufferSliceStack stack)
        {
            if (stack == null) throw new ArgumentNullException("stack");
            return ReferenceEquals(bufferSliceStack, stack);
        }

        /// <summary>
        /// Reset buffer (i.e. go back to initial size and offset)
        /// </summary>
        public void Reset()
        {
            Count = initialSize;
            Offset = initialOffset;
            isDisposed = false;
        }
    }
}