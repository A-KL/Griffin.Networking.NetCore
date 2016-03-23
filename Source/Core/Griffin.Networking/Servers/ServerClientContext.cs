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
            readStream = new SliceStream(ReadBuffer);
            readArgs = new SocketAsyncEventArgs();
            readArgs.Completed += OnReadCompleted;
            readArgs.SetBuffer(this.readBuffer.Buffer, this.readBuffer.Offset, this.readBuffer.Count);
            writer = new SocketWriter();
            writer.Disconnected += OnWriterDisconnect;
        }

        /// <summary>
        /// Our read buffer.
        /// </summary>
        protected IBufferSlice ReadBuffer
        {
            get { return readBuffer; }
        }

        #region IServerClientContext Members

        /// <summary>
        /// Gets remote end point
        /// </summary>
        public IPEndPoint RemoteEndPoint
        {
            get { return remoteEndPoint; }
        }

        /// <summary>
        /// Send information to the remote end point
        /// </summary>
        /// <param name="slice">Buffer slice</param>
        /// <param name="length">Number of bytes in the slice.</param>
        public void Send(IBufferSlice slice, int length)
        {
            if (slice == null) throw new ArgumentNullException("slice");
            writer.Send(new SliceSocketWriterJob(slice, length));
        }

        /// <summary>
        /// Send a stream
        /// </summary>
        /// <param name="stream">Stream to send</param>
        /// <remarks>The stream will be owned by the framework, i.e. disposed when sent.</remarks>
        public void Send(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            writer.Send(new StreamSocketWriterJob(stream));
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
            if (socket == null)
                return;

            try
            {
                //_socket.Shutdown(SocketShutdown.Both);
                //_socket.Disconnect(true);
                socket.Dispose();
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
            if (socket.Connected)
                Close();

            socket = null;

            if (client == null)
                return;

            client.Dispose();
            client = null;
            writer.Reset();
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
            Disconnected(this, new DisconnectEventArgs(error));
        }


        /// <summary>
        /// We've received information from the client
        /// </summary>
        /// <param name="data">The type of data depends on the server implementation.</param>
        protected virtual void TriggerClientReceive(object data)
        {
            if (data == null) throw new ArgumentNullException("data");
            client.HandleReceive(data);
        }

        /// <summary>
        /// Remote side have disconnected (or network failure)
        /// </summary>
        /// <remarks><para>The source will be the context.</para><para>Will also be triggered when <see cref="Close()"/> is invoked, but with the error code <see cref="SocketError.Success"/>.</para></remarks>
        public event EventHandler<DisconnectEventArgs> Disconnected = delegate { };

        private void OnReadCompleted(object sender, SocketAsyncEventArgs e)
        {
            logger.Trace(string.Format("Received {0} from {1}", e.BytesTransferred, remoteEndPoint));
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                readStream.Position = 0;
                readStream.SetLength(e.BytesTransferred);

                try
                {
                    HandleRead(readBuffer, e.BytesTransferred);
                }
                catch (Exception err)
                {
                    logger.Warning("Unhandled exception", err);

                    var buffer = new BufferSlice(readBuffer.Buffer, readBuffer.Offset, e.BytesTransferred);
                    var context = new ServiceExceptionContext(err, buffer);
                    client.OnUnhandledException(context);

                    if (context.CanExceptionBePropagated)
                    {
                        var args = new ClientExceptionEventArgs(this, err, buffer);
                        UnhandledExceptionCaught(this, args);
                        if (!args.CanContinue)
                        {
                            logger.Debug("Signalled to stop processing");
                            return;
                        }
                    }

                    if (!context.MayContinue)
                    {
                        logger.Debug("ClientService signaled to stop processing");
                        Cleanup();
                        return;
                    }
                }

                try
                {
                    bool isPending = socket.ReceiveAsync(readArgs);
                    if (!isPending)
                        OnReadCompleted(socket, readArgs);
                }
                catch (ObjectDisposedException)
                {
                    Cleanup();
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

                Cleanup();
                if (e.SocketError != SocketError.OperationAborted)
                    OnDisconnect(error);
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
            client.HandleReceive(readStream);
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
            writer.Assign(socket);

            var ep = (IPEndPoint)this.socket.RemoteEndPoint;
            remoteEndPoint = new IPEndPoint(ep.Address, ep.Port);

            var willRaiseEvent = this.socket.ReceiveAsync(readArgs);
            if (!willRaiseEvent)
                OnReadCompleted(this.socket, readArgs);
        }

        /// <summary>
        /// Set buffer which can be used for the currently active write operation.
        /// </summary>
        /// <param name="bufferSlice">Slice</param>
        public void SetWriteBuffer(IBufferSlice bufferSlice)
        {
            if (bufferSlice == null) throw new ArgumentNullException("bufferSlice");
            writer.SetBuffer(bufferSlice);
        }

        public void Dispose()
        {
            readArgs.Dispose();
            readStream.Dispose();
            if (client != null)
                client.Dispose();
            if (socket != null)
                socket.Dispose();
        }
    }
}