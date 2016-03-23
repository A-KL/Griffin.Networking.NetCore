using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Griffin.Networking.Buffers;
using Griffin.Networking.Logging;

namespace Griffin.Networking
{
    /// <summary>
    /// Used to write information to a socket in a queued fashion.
    /// </summary>
    public class SocketWriter
    {
        private readonly ILogger logger = LogManager.GetLogger<SocketWriter>();
        private readonly SocketAsyncEventArgs writeArgs = new SocketAsyncEventArgs();
        private readonly ConcurrentQueue<ISocketWriterJob> writeQueue = new ConcurrentQueue<ISocketWriterJob>();
        private ISocketWriterJob currentJob;
        private Socket socket;


        /// <summary>
        /// Initializes a new instance of the <see cref="SocketWriter" /> class.
        /// </summary>
        public SocketWriter()
        {
            writeArgs.Completed += OnWriteCompleted;
        }

        private void OnWriteCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                HandleWriteCompleted(e.SocketError, e.BytesTransferred);
            }
            catch (Exception err)
            {
                logger.Error("Failed to handle write completed.", err);
            }
        }

        /// <summary>
        /// Assign socket which will be used for writing.
        /// </summary>
        /// <param name="socket"></param>
        public void Assign(Socket socket)
        {
            if (socket == null) throw new ArgumentNullException("socket");
            if (!socket.Connected)
                throw new InvalidOperationException("Socket must be connected.");

            this.socket = socket;
        }

        /// <summary>
        /// Sends the specified slice.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">slice</exception>
        /// <exception cref="System.InvalidOperationException">Socket as not been Assign():ed.</exception>
        /// <seealso cref="StreamSocketWriterJob"/>
        /// <seealso cref="SliceSocketWriterJob"/>
        public void Send(ISocketWriterJob job)
        {
            if (job == null) throw new ArgumentNullException("job");
            if (socket == null || !socket.Connected)
                throw new InvalidOperationException("Socket is not connected.");

            lock (this)
            {
                if (currentJob != null)
                {
                    logger.Debug(writeArgs.GetHashCode() + ": Enqueueing ");
                    writeQueue.Enqueue(job);
                    return;
                }

                logger.Debug(writeArgs.GetHashCode() + ": sending directly ");
                currentJob = job;
            }

            currentJob.Write(writeArgs);

            var isPending = socket.SendAsync(writeArgs);
            if (!isPending)
                HandleWriteCompleted(writeArgs.SocketError, writeArgs.BytesTransferred);
        }

        private void HandleWriteCompleted(SocketError error, int bytesTransferred)
        {
            if (currentJob == null || socket == null || !socket.Connected)
                return; // got disconnected

            if (error == SocketError.Success && bytesTransferred > 0)
            {
                lock (this)
                {
                    if (currentJob.WriteCompleted(bytesTransferred))
                    {
                        currentJob.Dispose();
                        if (!writeQueue.TryDequeue(out currentJob))
                        {
                            logger.Debug(writeArgs.GetHashCode() + ": no new job ");
                            currentJob = null;
                            return;
                        }
                    }
                }

                currentJob.Write(writeArgs);
                var isPending = socket.SendAsync(writeArgs);
                if (!isPending)
                    HandleWriteCompleted(writeArgs.SocketError, writeArgs.BytesTransferred);
            }
            else
            {
                if (error == SocketError.Success)
                    error = SocketError.ConnectionReset;
                HandleDisconnect(error);
            }
        }

        private void HandleDisconnect(SocketError error)
        {
            Reset();
            Disconnected(this, new DisconnectEventArgs(error));
        }

        /// <summary>
        /// We've been disconnected (detected during a write)
        /// </summary>
        public event EventHandler<DisconnectEventArgs> Disconnected = delegate { };

        /// <summary>
        /// Reset writer.
        /// </summary>
        public void Reset()
        {
            if (currentJob != null)
                currentJob.Dispose();
            currentJob = null;


            ISocketWriterJob job;
            while (writeQueue.TryDequeue(out job))
            {
                job.Dispose();
            }

            socket = null;
        }

        /// <summary>
        /// Assign a buffer which can be used during writes.
        /// </summary>
        /// <param name="bufferSlice">Buffer</param>
        /// <remarks>The buffer is stored as <c>UserToken</c> for the AsyncEventArgs. Do not change the token, but feel free to use it for the current write.</remarks>
        public void SetBuffer(IBufferSlice bufferSlice)
        {
            if (bufferSlice == null) throw new ArgumentNullException("bufferSlice");
            writeArgs.UserToken = bufferSlice;
        }
    }
}