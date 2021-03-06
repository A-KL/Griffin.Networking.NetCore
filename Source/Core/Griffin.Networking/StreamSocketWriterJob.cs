using System;
using System.IO;
using System.Net.Sockets;
using Griffin.Networking.Buffers;
using Griffin.Networking.Logging;

namespace Griffin.Networking
{
    /// <summary>
    /// Send a stream to the socket.
    /// </summary>
    /// <remarks>Large stream will be send a bit at a time.</remarks>
    public class StreamSocketWriterJob : ISocketWriterJob
    {
        private int bytesLeft;
        private Stream stream;
        private ILogger logger = LogManager.GetLogger<StreamSocketWriterJob>();

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamSocketWriterJob" /> class.
        /// </summary>
        /// <param name="stream">The stream, owned by this class (i.e. being disposed when sent).</param>
        /// <exception cref="System.ArgumentNullException">stream</exception>
        public StreamSocketWriterJob(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            this.stream = stream;
            this.logger.Debug(string.Format("Stream position: {0}, size: {1}", stream.Position, stream.Length));
            this.bytesLeft = (int)stream.Length - (int)stream.Position;
        }

        #region ISocketWriterJob Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (this.stream == null) return;
            this.stream.Dispose();
            this.stream = null;
        }

        /// <summary>
        /// Write stuff to our args.
        /// </summary>
        /// <param name="args">Args used when sending bytes to the socket</param>
        public void Write(SocketAsyncEventArgs args)
        {
            var buffer = (IBufferSlice)args.UserToken;
            var bytesToSend = Math.Min(this.bytesLeft, buffer.Count);
            var bytesRead = this.stream.Read(buffer.Buffer, buffer.Offset, bytesToSend);
            if (bytesRead == 0)
                throw new InvalidOperationException(
                    "Failed to read bytes from the stream. Did you remember to set the correct Postition in the stream?");

            this.logger.Debug(string.Format("Writing {0} bytes of total {1}", bytesRead, this.stream.Length));
            args.SetBuffer(buffer.Buffer, buffer.Offset, bytesRead);
        }

        /// <summary>
        /// The async write has been completed
        /// </summary>
        /// <param name="bytes">Number of bytes that was sent</param>
        /// <returns><c>true</c> if everything was sent; otherwise <c>false</c>.</returns>
        public bool WriteCompleted(int bytes)
        {
            this.bytesLeft -= bytes;
            this.logger.Debug("Write completed, bytes left " + this.bytesLeft);
            return this.bytesLeft == 0;
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return string.Format("Stream {0} at position {1} of {2}.", this.stream, this.stream.Position, this.stream.Length);
        }
    }
}