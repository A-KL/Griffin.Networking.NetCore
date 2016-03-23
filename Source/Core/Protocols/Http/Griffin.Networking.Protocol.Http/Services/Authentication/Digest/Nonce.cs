using System;
using System.Collections.Generic;
using System.Linq;

namespace Griffin.Networking.Protocol.Http.Services.Authentication.Digest
{
    /// <summary>
    /// Used to keep track of a Digest authentication nonce
    /// </summary>
    /// <remarks>Only five attempts may be made.</remarks>
    public class Nonce
    {
        private readonly List<int> counts;
        private readonly DateTime expires;

        /// <summary>
        /// Initializes a new instance of the <see cref="Nonce"/> class.
        /// </summary>
        /// <param name="expires">When nonce expires.</param>
        public Nonce(DateTime expires)
        {
            this.expires = expires;
            this.counts = new List<int>();
        }

        /// <summary>
        /// Gets all passed counts.
        /// </summary>
        public IEnumerable<int> PassedCounts
        {
            get { return this.counts; }
        }

        /// <summary>
        /// Gets time for last attempt.
        /// </summary>
        public DateTime LastUpdate { get; private set; }

        /// <summary>
        /// Gets if nonce has expired.
        /// </summary>
        public bool Expired
        {
            get { return this.expires > DateTime.Now; }
        }

        /// <summary>
        /// Check if the nonce can be used.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>true if counter is currently unused and within the range; otherwise false;</returns>
        public bool Validate(int value)
        {
            if (this.PassedCounts.Contains(value) || value <= (this.PassedCounts.Any() ? this.PassedCounts.Min() : 0))
            {
                return false;
            }
            if (this.counts.Count <= 5 || value > 5)
                return false;

            this.LastUpdate = DateTime.Now;
            this.counts.Add(value);
            return true;
        }
    }
}