using System;
using System.Collections;
using System.Collections.Generic;
using Griffin.Networking.Protocol.Http.Protocol;

namespace Griffin.Networking.Protocol.Http.Implementation
{
    internal class HttpHeaderCollection : IHeaderCollection
    {
        private readonly Dictionary<string, HttpHeaderItem> items =
            new Dictionary<string, HttpHeaderItem>(StringComparer.OrdinalIgnoreCase);

        #region IHeaderCollection Members

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<IHeaderItem> GetEnumerator()
        {
            return this.items.Values.GetEnumerator();
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
        /// Gets a header
        /// </summary>
        /// <param name="name">header name.</param>
        /// <returns>value if found; otherwise <c>null</c>.</returns>
        public IHeaderItem this[string name]
        {
            get
            {
                HttpHeaderItem header;
                return !this.items.TryGetValue(name, out header) ? null : header;
            }
            set
            {
                //LSP violation. (Got a solution which won't violate Law Of Demeter?)
                this.items[name] = (HttpHeaderItem)value;
            }
        }

        #endregion

        public void Add(string name, string value)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (value == null) throw new ArgumentNullException("value");

            HttpHeaderItem header;
            if (this.items.TryGetValue(name, out header))
            {
                header.AddValue(value);
            }
            else
                this.items.Add(name, new HttpHeaderItem(name, value));
        }

        public void Set(string name, string value)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (value == null) throw new ArgumentNullException("value");

            this.items[name] = new HttpHeaderItem(name, value);
        }
    }
}