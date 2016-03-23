using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Griffin.Networking.Buffers;
using Griffin.Networking.Logging;

namespace Griffin.Networking.Servers
{
    /// <summary>
    /// Represents a client connection in the server.
    /// </summary>
    /// <remarks>These contexts are reused since they contain information which is a bit heavy to recreate every time.</remarks>
    public class ServerClientContext : IServerClientContext, IDisposable
    {
        private readonly SocketAsyncEventArgs readArgs;
        private readonly IBufferSlice readBuffer;
        private readonly SliceStream readStream;
        private readonly SocketWriter writer;
        private INetworkService client;
        private Socket socket;
        private ILogger logger = LogManager.GetLogger<ServerClientContext>();
        private IPEndPoint remoteEndPoint;


        /// <summary>
        /// Initializes a new instance of the <see cref="ServerClientContext" /> class.
        /// </summary>
        /// <param name="readBuffer">The read buffer.</param>
        public ServerClientContext(IBufferSlice readBuffer)
        {
            if (readBuffer == null) throw new ArgumentNullException("readBuffer");
            this.readBuffer = readBuffer;
            this.readStream = new SliceStream(this.ReadBuffer);
            this.readArgs = new SocketAsyncEventArgs();
            this.readArgs.Completed += this.OnReadCompleted;
            this.readArgs.SetBuffer(this.readBuffer.Buffer, this.readBuffer.Offset, this.readBuffer.Count);
            this.writer = new SocketWriter();
            this.writer.Disconnected += this.OnWriterDisconnect;
        }

        /// <summary>
        /// Our read buffer.
        /// </summary>
        protected IBufferSlice ReadBuffer
        {
            get { return this.readBuffer; }
        }

        #region IServerClientContext Members

        /// <summary>
        /// Gets remote end point
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return this.remoteEndPoint; }
        }

        /// <summary>
        /// Send information to the remote end point
        /// </summary>
        /// <param name="slice">Buffer slice</param>
        /// <param name="length">Number of bytes in the slice.</param>
        public void Send(IBufferSlice slice, int length)
        {
            if (slice == null) throw new ArgumentNullException("slice");
            this.writer.Send(new SliceSocketWriterJob(slice, length));
        }

        /// <summary>
        /// Send a stream
        /// </summary>
        /// <param name="stream">Stream to send</param>
        /// <remarks>The stream will be owned by the framework, i.e. disposed when sent.</remarks>
        public void Send(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            this.writer.Send(new StreamSocketWriterJob(stream));
        }

        /// <summary>
        /// Context has been freed. Reset the state.
        /// </summary>
        public virtual void Reset()
        {

        }

        /// <summary>
        /// An unhandled exception was caught during read processing (which always is our entry point since we are a server).
        /// </summary>
        /// <remarks>Use the <see cref="ClientExceptionEventArgs.CanContinue"/> to flag if processing should be aborted or not.</remarks>
        public event EventHandler<ClientExceptionEventArgs> UnhandledExceptionCaught = delegate { };

        #endregion

        /// <summary>
        /// Closes the specified trigger disconnect event.
        /// </summary>
        public virtual void Close()
        {
            if (this.socket == null)
                return;

            try
            {
                //_socket.Shutdown(SocketShutdown.Both);
                //_socket.Disconnect(true);
                this.socket.Dispose();
            }
            catch (Exception err)
            {
                // Do not care
                //Console.WriteLine(err.ToString());
            }

            // let the pending receive do any additional cleanup
        }

        private void Cleanup()
        {
            if (this.socket.Connected)
                this.Close();

            this.socket = null;

            if (this.client == null)
                return;

            this.client.Dispose();
            this.client = null;
            this.writer.Reset();
        }

        private void OnWriterDisconnect(object sender, DisconnectEventArgs e)
        {
            //TODO: Typically we have already detected disconnect thanks to the pending
            // Receive. Hence ignore this
            //OnDisconnect(e.SocketError);
            //Console.WriteLine("Write error: " + e.SocketError);
        }


        /// <summary>
        /// Invoked when we've been disconnected
        /// </summary>
        /// <param name="error"><see cref="SocketError.Success"/> means that we disconnected, all other codes indicates network failure or that the remote end point disconnected.
        /// 
        /// </param>
        /// <remarks>Remember to call the <c>base</c> when you override this method (to trigger the Disconnected event)</remarks>
        protected virtual void OnDisconnect(SocketError error)
        {
            this.Disconnected(this, new DisconnectEventArgs(error));
        }


        /// <summary>
        /// We've received information from the client
        /// </summary>
        /// <param name="data">The type of data depends on the server implementation.</param>
        protected virtual void TriggerClientReceive(object data)
        {
            if (data == null) throw new ArgumentNullException("data");
            this.client.HandleReceive(data);
        }

        /// <summary>
        /// Remote side have disconnected (or network failure)
        /// </summary>
        /// <remarks><para>The source will be the context.</para><para>Will also be triggered when <see cref="Close()"/> is invoked, but with the error code <see cref="SocketError.Success"/>.</para></remarks>
        public event EventHandler<DisconnectEventArgs> Disconnected = delegate { };

        private void OnReadCompleted(object sender, SocketAsyncEventArgs e)
        {
            this.logger.Trace(string.Format("Received {0} from {1}", e.BytesTransferred, this.remoteEndPoint));
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                this.readStream.Position = 0;
                this.readStream.SetLength(e.BytesTransferred);

                try
                {
                    this.HandleRead(this.readBuffer, e.BytesTransferred);
                }
                catch (Exception err)
                {
                    this.logger.Warning("Unhandled exception", err);

                    var buffer = new BufferSlice(this.readBuffer.Buffer, this.readBuffer.Offset, e.BytesTransferred);
                    var context = new ServiceExceptionContext(err, buffer);
                    this.client.OnUnhandledException(context);

                    if (context.CanExceptionBePropagated)
                    {
                        var args = new ClientExceptionEventArgs(this, err, buffer);
                        this.UnhandledExceptionCaught(this, args);
                        if (!args.CanContinue)
                        {
                            this.logger.Debug("Signalled to stop processing");
                            return;
                        }
                    }

                    if (!context.MayContinue)
                    {
                        this.logger.Debug("ClientService signaled to stop processing");
                        this.Cleanup();
                        return;
                    }
                }

                try
                {
                    bool isPending = this.socket.ReceiveAsync(this.readArgs);
                    if (!isPending)
                        this.OnReadCompleted(this.socket, this.readArgs);
                }
                catch (ObjectDisposedException)
                {
                    this.Cleanup();
                    return;
                }
            }
            else
            {
                // read = 0 bytes = SocketError.Success
                // but we want to use it to indicate that localhost have closed the socket.
                // hence the rewrite
                var error = e.SocketError == SocketError.Success
                                ? SocketError.ConnectionReset
                                : e.SocketError;

                this.Cleanup();
                if (e.SocketError != SocketError.OperationAborted)
                    this.OnDisconnect(error);
            }
        }

        /// <summary>
        /// Handle incoming bytes
        /// </summary>
        /// <param name="readBuffer">Buffer containing received bytes</param>
        /// <param name="bytesReceived">Number of bytes that was recieved (will always be set, any errors have triggered <see cref="OnDisconnect"/> instead).</param>
        /// <remarks>
        /// <para>The default implementation will trigger the client with a <see cref="IBufferReader"/> as message. That means that
        /// you should not call the base method from your code.</para>
        /// </remarks>
        protected virtual void HandleRead(IBufferSlice readBuffer, int bytesReceived)
        {
            this.client.HandleReceive(this.readStream);
        }

        /// <summary>
        /// Assign a new socket &amp; client to this context.
        /// </summary>
        /// <param name="socket">Socket that connected</param>
        /// <param name="client">Your own class dealing with this particular client.</param>
        public void Assign(Socket socket, INetworkService client)
        {
            if (socket == null) throw new ArgumentNullException("socket");
            if (client == null) throw new ArgumentNullException("client");
            this.socket = socket;
            this.client = client;
            this.client.Assign(this);
            this.writer.Assign(socket);

            var ep = (IPEndPoint)this.socket.RemoteEndPoint;
            this.remoteEndPoint = new IPEndPoint(ep.Address, ep.Port);

            var willRaiseEvent = this.socket.ReceiveAsync(this.readArgs);
            if (!willRaiseEvent)
                this.OnReadCompleted(this.socket, this.readArgs);
        }

        /// <summary>
        /// Set buffer which can be used for the currently active write operation.
        /// </summary>
        /// <param name="bufferSlice">Slice</param>
        public void SetWriteBuffer(IBufferSlice bufferSlice)
        {
            if (bufferSlice == null) throw new ArgumentNullException("bufferSlice");
            this.writer.SetBuffer(bufferSlice);
        }

        public void Dispose()
        {
            this.readArgs.Dispose();
            this.readStream.Dispose();
            if (this.client != null)
                this.client.Dispose();
            if (this.socket != null)
                this.socket.Dispose();
        }
    }
}