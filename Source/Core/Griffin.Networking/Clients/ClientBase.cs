namespace Griffin.Networking.Clients
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using Buffers;
    using Messaging;

    /// <summary>
    /// Base class for clients.
    /// </summary>
    public abstract class ClientBase
    {
        private readonly SocketAsyncEventArgs readArgs = new SocketAsyncEventArgs();
        private readonly BufferSlice readBuffer = new BufferSlice(65535);
        private readonly SocketWriter socketWriter = new SocketWriter();
        private Socket client;
        //private Windows.Networking.Sockets.StreamSocketListener client;
        private IPEndPoint remoteEndPoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientBase"/> class. 
        /// Initializes a new instance of the <see cref="MessagingClient"/> class.
        /// </summary>
        protected ClientBase()
        {
            this.readArgs.Completed += this.OnClientRead;
            this.readArgs.SetBuffer(this.readBuffer.Buffer, this.readBuffer.Offset, this.readBuffer.Count);
        }

        /// <summary>
        /// The remote end point have disconnected (or network failure)
        /// </summary>
        /// <param name="socketAsyncEventArgs"></param>
        protected void HandleDisconnect(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            if (this.Disconnected != null)
            {
                this.Disconnected(this, new DisconnectEventArgs(socketAsyncEventArgs.SocketError));
            }
        }

        /// <summary>
        /// We've received something from the other end
        /// </summary>
        /// <param name="buffer">Buffer containing the received bytes</param>
        /// <param name="bytesRead">Amount of bytes that we received</param>
        /// <remarks>You have to handle all bytes, anything left will be discarded.</remarks>
        protected abstract void OnReceived(IBufferSlice buffer, int bytesRead);


        /// <summary>
        /// Send something to the remote end point
        /// </summary>
        /// <param name="slice">Slice to send. It's up to you to make sure that it's returned to the pool (if pooled)</param>
        /// <param name="count">Number of bytes in the buffer</param>
        protected void Send(IBufferSlice slice, int count)
        {
            if (slice == null)
            {
                throw new ArgumentNullException("slice");
            }
            this.socketWriter.Send(new SliceSocketWriterJob(slice, count));
        }

        /// <summary>
        /// Send a stream
        /// </summary>
        /// <param name="stream">Stream to send</param>
        /// <remarks>The stream will be owned by the framework, i.e. disposed when sent.</remarks>
        protected void Send(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            this.socketWriter.Send(new StreamSocketWriterJob(stream));
        }

        /// <summary>
        /// Connect to an end point
        /// </summary>
        /// <param name="remoteEndPoint">end point</param>
        public void Connect(IPEndPoint remoteEndPoint)
        {
            if (this.remoteEndPoint == null)
            {
                throw new ArgumentNullException("remoteEndPoint");
            }

            if (this.client != null)
            {
                throw new InvalidOperationException("Client already connected.");
            }

            this.remoteEndPoint = remoteEndPoint;
            this.Connect();
        }

        /// <summary>
        /// Close connection and clean up
        /// </summary>
        public void Close()
        {
            if (this.client == null)
            {
                throw new InvalidOperationException("We have already been closed (or never connected).");
            }

            this.client.Shutdown(SocketShutdown.Send);
            //client.Close();
            this.client.Dispose();
            this.client = null;
        }

        /// <summary>
        /// Other side disconnected (or network failure)
        /// </summary>
        public virtual event EventHandler Disconnected = delegate { };

        private void Connect()
        {
            this.client = new Socket(this.remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.client.Bind(this.remoteEndPoint);

            this.socketWriter.Assign(this.client);

            var willRaiseEvent = this.client.ReceiveAsync(this.readArgs);
            if (!willRaiseEvent)
            {
                this.OnClientRead(this, this.readArgs);
            }
        }

        private void OnClientRead(object sender, SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                this.OnReceived(this.readBuffer, e.BytesTransferred);
                this.client.ReceiveAsync(e);
            }
            else
            {
                this.HandleDisconnect(e);
            }
        }
    }
}