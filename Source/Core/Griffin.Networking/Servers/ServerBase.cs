using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Griffin.Networking.Buffers;

namespace Griffin.Networking.Servers
{
    /// <summary>
    /// Base class for servers.
    /// </summary>
    /// <remarks>Contains most of the logic, but do not dictate how you should handle clients.</remarks>
    public abstract class ServerBase : IDisposable
    {
        private readonly BufferSliceStack bufferSliceStack;
        private readonly ConcurrentStack<ServerClientContext> contexts = new ConcurrentStack<ServerClientContext>();
        private readonly int maxAmountOfConnection;
        private readonly Semaphore maxNumberAcceptedClients;
        private Socket listener;
        private int numConnectedSockets;
        private SocketAsyncEventArgs listenerArgs;
        ManualResetEvent shutdown = new ManualResetEvent(false);


        /// <summary>
        /// Initializes a new instance of the <see cref="Server" /> class.
        /// </summary>
        protected ServerBase(ServerConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            configuration.Validate();

            this.numConnectedSockets = 0;
            this.maxAmountOfConnection = configuration.MaximumNumberOfClients;
            this.maxNumberAcceptedClients = new Semaphore(configuration.MaximumNumberOfClients,
                                                      configuration.MaximumNumberOfClients);

            this.listenerArgs = new SocketAsyncEventArgs();
            this.listenerArgs.Completed += this.OnAccept;

            // *2 since we need one for each send/receive pair.
            this.bufferSliceStack = new BufferSliceStack(configuration.MaximumNumberOfClients*2, configuration.BufferSize);
        }


        private void Init()
        {
            for (var i = 0; i < this.maxAmountOfConnection; i++)
            {
                var context = this.CreateClientContext(this.bufferSliceStack.Pop());
                context.Disconnected += this.OnClientDisconnectedInternal;
                context.UnhandledExceptionCaught += this.OnClientException;
                context.SetWriteBuffer(this.bufferSliceStack.Pop());
                this.contexts.Push(context);
            }
        }

        private void OnClientException(object sender, ClientExceptionEventArgs e)
        {
            this.UnhandledClientExceptionCaught(this, e);
        }

        /// <summary>
        /// Create a new client context
        /// </summary>
        /// <param name="readBuffer">The read buffer to use</param>
        /// <returns>Created context</returns>
        /// <remarks>The contexts are created at startup and then reused during the server lifetime. A context
        /// is assigned to a socket each time we've accepted one using the listener.</remarks>
        protected virtual ServerClientContext CreateClientContext(IBufferSlice readBuffer)
        {
            return new ServerClientContext(readBuffer);
        }

        /// <summary>
        /// A client has disconnected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnClientDisconnectedInternal(object sender, EventArgs e)
        {
            var context = (ServerClientContext) sender;
            this.OnClientDisconnected(context);
            Interlocked.Decrement(ref this.numConnectedSockets);
            this.maxNumberAcceptedClients.Release();
            context.Reset();
            this.contexts.Push(context);
        }

        /// <summary>
        /// A client has disconnected from the server (either network failure or by the remote end point)
        /// </summary>
        /// <param name="context">Disconnected client</param>
        /// <remarks>Calls to <see cref="ServerClientContext.Close()"/> will also trigger this method, but with <see cref="SocketError.Success"/>. 
        /// <para>The method is typically used to clean up your own implementation. The context, socket ETC have already been cleaned up.</para></remarks>
        protected virtual void OnClientDisconnected(ServerClientContext context)
        {
        }

        /// <summary>
        /// Start server and begin accepting end points.
        /// </summary>
        /// <param name="localEndPoint">End point that the server should listen on.</param>
        public void Start(IPEndPoint localEndPoint)
        {
            if (this.listener != null)
                throw new InvalidOperationException("Server already started.");

            if (this.contexts.IsEmpty)
                this.Init();

            this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.listener.Bind(localEndPoint);
            this.listener.Listen(100);
            this.LocalPort = ((IPEndPoint) this.listener.LocalEndPoint).Port;

            this.StartAccept();
        }

        /// <summary>
        /// Gets port that the server is listening on
        /// </summary>
        /// <remarks>Useful if you specify <c>0</c> as port in <see cref="Start"/> (which means that the OS should pick a free port)</remarks>
        public int LocalPort { get; private set; }

        private void StartAccept()
        {
            this.maxNumberAcceptedClients.WaitOne();
            this.listenerArgs.AcceptSocket = null;
            var willRaiseEvent = this.listener.AcceptAsync(this.listenerArgs);
            if (!willRaiseEvent)
            {
                this.OnAccept(this, this.listenerArgs);
            }
        }


        private void OnAccept(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                this.shutdown.Set();
                return;
            }
            Interlocked.Increment(ref this.numConnectedSockets);

            ServerClientContext context;
            if (!this.contexts.TryPop(out context))
                throw new InvalidOperationException("Failed to get a new client context, all is currently in use.");

            if (!this.ValidateClient(e.AcceptSocket))
            {
                try
                {
                    e.AcceptSocket.Shutdown(SocketShutdown.Send);
                }
                catch
                {
                }
                e.AcceptSocket.Dispose();
                return;
            }

            var client = this.CreateClient(e.AcceptSocket.RemoteEndPoint);
            context.Assign(e.AcceptSocket, client);
            this.OnClientConnected(context);

            this.StartAccept();
        }

        /// <summary>
        /// Create a new object which will handle all communication to/from a specific client.
        /// </summary>
        /// <param name="remoteEndPoint">Remote end point</param>
        /// <returns>Created client</returns>
        protected abstract INetworkService CreateClient(EndPoint remoteEndPoint);

        /// <summary>
        /// A new client have connected
        /// </summary>
        /// <param name="context">Client context</param>
        /// <remarks>Invoked when a client has been validated and successfully been added.</remarks>
        /// <seealso cref="ValidateClient"/>
        protected virtual void OnClientConnected(ServerClientContext context)
        {
        }

        /// <summary>
        /// A new client have connected
        /// </summary>
        /// <param name="acceptedSocket">Socket for the client</param>
        /// <returns><c>true</c> if the client can be accepted; <c>false</c> to disconnect the client.</returns>
        /// <remarks>Use this method to filter out any unwanted clients. Feel free to use it for any handshake etc.</remarks>
        /// <seealso cref="OnClientConnected"/>
        protected virtual bool ValidateClient(Socket acceptedSocket)
        {
            return true;
        }

        /// <summary>
        /// Stop accepting new connections
        /// </summary>
        /// <remarks>Any existing connections will continue to run until they disconnect.</remarks>
        public void Stop()
        {
            if (this.listener == null)
                return;

            this.listener.Dispose();
            this.shutdown.WaitOne(5000);
            this.listener = null;
        }

        /// <summary>
        /// An unhandled exception has been caught for one of the clients.
        /// </summary>
        /// <remarks>Use the <see cref="ClientExceptionEventArgs.CanContinue"/> to flag if processing should be aborted or not.</remarks>
        public event EventHandler<ClientExceptionEventArgs> UnhandledClientExceptionCaught = delegate { };
        
        public void Dispose()
        {
            this.Stop();
            foreach (var c in this.contexts)
            {
                c.Dispose();
            }
        }
    }
}