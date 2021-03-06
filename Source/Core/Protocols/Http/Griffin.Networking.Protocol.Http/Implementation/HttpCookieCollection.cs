﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Griffin.Networking.Protocol.Http.Protocol;

namespace Griffin.Networking.Protocol.Http.Implementation
{
    /// <summary>
    /// A collection of HTTP cookies
    /// </summary>
    /// <typeparam name="T">Type of cookie</typeparam>
    public class HttpCookieCollection<T> : IHttpCookieCollection<T> where T : class, IHttpCookie
    {
        private readonly List<T> items = new List<T>();

        #region IHttpCookieCollection<T> Members

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<T> GetEnumerator()
        {
            return this.items.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// Adds the specified cookie.
        /// </summary>
        /// <param name="cookie">The cookie.</param>
        public void Add(T cookie)
        {
            if (cookie == null)
                throw new ArgumentNullException("cookie");

            this.items.Add(cookie);
        }

        /// <summary>
        /// Gets the count of cookies in the collection.
        /// </summary>
        public int Count
        {
            get { return this.items.Count; }
        }

        /// <summary>
        /// Gets the cookie of a given identifier (<c>null</c> if not existing).
        /// </summary>
        public T this[string id]
        {
            get
            {
                if (id == null) throw new ArgumentNullException("id");
                return this.items.FirstOrDefault(x => x.Name.Equals(id, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Remove all cookies.
        /// </summary>
        public void Clear()
        {
            this.items.Clear();
        }

        /// <summary>
        /// Remove a cookie from the collection.
        /// </summary>
        /// <param name="cookieName">Name of cookie.</param>
        public void Remove(string cookieName)
        {
            if (cookieName == null) throw new ArgumentNullException("cookieName");
            this.items.RemoveAll(x => x.Name.Equals(cookieName, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}