using System;
using System.IO;

namespace Griffin.Networking.Buffers
{
    /// <summary>
    /// A memory stream that supports <see cref="IPeekable"/>.
    /// </summary>
    public class PeekableMemoryStream : MemoryStream, IPeekable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PeekableMemoryStream"/> class.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="capacity">The capacity.</param>
        public PeekableMemoryStream(byte[] buffer, int offset, int capacity)
            : base(buffer, offset, capacity, true, false)
        {
        }

        #region IPeekable Members

        /// <summary>
        /// Peek at the next byte in the sequence.
        /// </summary>
        /// <returns>Char if not EOF; otherwise <see cref="char.MinValue"/></returns>
        public char Peek()
        {
            if (Position >= Length)
                return char.MinValue;

            ArraySegment<byte> segment;
            if (this.TryGetBuffer(out segment))
            {
                return (char)segment.Array[Position + 1];
            }

            return Char.MaxValue;
        }

        #endregion
    }
}