using System;
using Griffin.Networking.Buffers;
using Griffin.Networking.Servers;

namespace Griffin.Networking.Messaging
{
    /// <summary>
    /// Used by <see cref="MessagingServer"/>.
    /// </summary>
    public class MessagingClientContext : ServerClientContext
    {
        private readonly IMessageFormatterFactory formatterFactory;
        private readonly IMessageBuilder messageBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagingClientContext" /> class.
        /// </summary>
        /// <param name="readBuffer">The read buffer.</param>
        /// <param name="formatterFactory">Used to format messages </param>
        public MessagingClientContext(IBufferSlice readBuffer, IMessageFormatterFactory formatterFactory)
            : base(readBuffer)
        {
            if (formatterFactory == null) throw new ArgumentNullException("formatterFactory");
            this.formatterFactory = formatterFactory;
            messageBuilder = this.formatterFactory.CreateBuilder();
        }

        /// <summary>
        /// Handles the read.
        /// </summary>
        /// <param name="slice">The slice.</param>
        /// <param name="bytesRead">The bytes read.</param>
        protected override void HandleRead(IBufferSlice slice, int bytesRead)
        {
            if (messageBuilder.Append(new SliceStream(slice, bytesRead)))
            {
                object message;
                while (messageBuilder.TryDequeue(out message))
                {
                    TriggerClientReceive(message);
                }
            }
        }

        /// <summary>
        /// Context has been freed. Reset the state.
        /// </summary>
        /// <remarks>will reset the message builder.</remarks>
        public override void Reset()
        {
            messageBuilder.Reset();
            base.Reset();
        }

        /// <summary>
        /// Will serialize messages
        /// </summary>
        /// <param name="message"></param>
        public virtual void Write(object message)
        {
            var formatter = formatterFactory.CreateSerializer();
            var buffer = new BufferSlice(65535);
            var writer = new SliceStream(buffer);
            formatter.Serialize(message, writer);
            writer.Position = 0;
            Send(buffer, (int) writer.Length);
        }
    }
}