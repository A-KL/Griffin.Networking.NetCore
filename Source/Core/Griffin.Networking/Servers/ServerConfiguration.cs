using System;
using Griffin.Networking.Buffers;
using Griffin.Networking.Messaging;

namespace Griffin.Networking.Servers
{
    /// <summary>
    /// Configures the server
    /// </summary>
    public class ServerConfiguration
    {
        private int bufferSize;
        private BufferSliceStack bufferSliceStack;
        private int maximumNumberOfClients;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerConfiguration" /> class.
        /// </summary>
        public ServerConfiguration()
        {
            this.MaximumNumberOfClients = 100;
            this.BufferSize = 65535;
        }

        /// <summary>
        /// Gets or sets the maximum number of clients that can be connected simultaneously
        /// </summary>
        /// <value>Default = 100</value>
        public int MaximumNumberOfClients
        {
            get { return this.maximumNumberOfClients; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("value", value, "You should at least allow one connection.");

                this.maximumNumberOfClients = value;
            }
        }

        /// <summary>
        /// Gets or sets the size of the buffers should when sending/receiving data.
        /// </summary>
        /// <remarks>Too small buffers can make the selected <see cref="IMessageFormatterFactory"/> fail when serializing/deserializing messages.</remarks>
        /// <value>Default = 65535</value>
        public int BufferSize

        {
            get { return this.bufferSize; }
            set
            {
                if (value < 1024)
                    throw new ArgumentException(
                        "Seriously, any buffer size under 1024 seems like a waste. Have you understood what the buffer is used for?");

                this.bufferSize = value;
            }
        }

        /// <summary>
        /// Gets or sets stack used 
        /// </summary>
        public BufferSliceStack BufferSliceStack
        {
            get
            {
                return this.bufferSliceStack ??
                       (this.bufferSliceStack = new BufferSliceStack(this.MaximumNumberOfClients, this.BufferSize));
            }
            set { this.bufferSliceStack = value; }
        }

        /// <summary>
        /// Validate that the configuration is correct and that it contains all required information
        /// </summary>
        public virtual void Validate()
        {
        }
    }
}