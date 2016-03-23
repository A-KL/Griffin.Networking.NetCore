using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Griffin.Networking.Protocol.Http.Services.Authentication.Digest
{
    /// <summary>
    /// Monitors all nonces.
    /// </summary>
    public class NonceService : IDisposable
    {
        private readonly TimeSpan expiresTimeout = new TimeSpan(0, 0, 15);
        private readonly ConcurrentDictionary<string, Nonce> items = new ConcurrentDictionary<string, Nonce>();
        private Timer timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="NonceService"/> class.
        /// </summary>
        public NonceService()
        {
            this.timer = new Timer(this.Sweep, null, this.expiresTimeout, this.expiresTimeout);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NonceService"/> class.
        /// </summary>
        /// <param name="expiresTimeout">How long a nonce is valid, default is 15 seconds.</param>
        public NonceService(TimeSpan expiresTimeout)
        {
            this.expiresTimeout = expiresTimeout;
            this.timer = new Timer(this.Sweep, null, this.expiresTimeout, this.expiresTimeout);
        }

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (this.timer == null)
                return;

            this.timer.Dispose();
            this.timer = null;
        }

        #endregion

        /// <summary>
        /// Checks if a nonce is valid
        /// </summary>
        /// <param name="value">nonce value</param>
        /// <param name="counter">nc counter</param>
        /// <returns>true if nonce is valid; otherwise false.</returns>
        public virtual bool IsValid(string value, int counter)
        {
            Nonce nonce;
            if (!this.items.TryGetValue(value, out nonce))
                return false;

            if (!nonce.Validate(counter))
                return false;

            return !nonce.Expired;
        }

        /// <summary>
        /// Create a new nonce
        /// </summary>
        /// <returns>Created nonce.</returns>
        /// <remarks>Valid Time span is configured in the <see cref="NonceService(System.TimeSpan)"/> constructor. Default time is 15 seconds.</remarks>
        public string CreateNonce()
        {
            var nonce = Guid.NewGuid().ToString("N");
            this.items.AddOrUpdate(nonce, new Nonce(DateTime.Now.Add(this.expiresTimeout)), (x, y) => null);
            return nonce;
        }

        /// <summary>
        /// Remove expired nonces.
        /// </summary>
        /// <param name="state"></param>
        private void Sweep(object state)
        {
            this.items.Where(kvp => (DateTime.Now - kvp.Value.LastUpdate) > this.expiresTimeout)
                .Select(kvp => kvp.Key)
                .ToList()
                .ForEach(key =>
                    {
                        Nonce item;
                        this.items.TryRemove(key, out item);
                    });
        }
    }
}