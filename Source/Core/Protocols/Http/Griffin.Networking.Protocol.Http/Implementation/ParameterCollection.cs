using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Griffin.Networking.Protocol.Http.Protocol;

namespace Griffin.Networking.Protocol.Http.Implementation
{
    public class ParameterCollection : IParameterCollection
    {
        private readonly Dictionary<string, IParameter> items =
            new Dictionary<string, IParameter>(StringComparer.OrdinalIgnoreCase);

        #region IParameterCollection Members

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<IParameter> GetEnumerator()
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
        /// Gets number of parameters.
        /// </summary>
        public int Count
        {
            get { return this.items.Count; }
        }

        /// <summary>
        /// Gets last value of an parameter.
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <returns>String if found; otherwise <c>null</c>.</returns>
        public string this[string name]
        {
            get
            {
                IParameter parameter;
                return this.items.TryGetValue(name, out parameter) ? parameter.Last() : null;
            }
        }

        /// <summary>
        /// Get a parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IParameter Get(string name)
        {
            IParameter value;
            return this.items.TryGetValue(name, out value) ? value : null;
        }

        /// <summary>
        /// Add a query string parameter.
        /// </summary>
        /// <param name="name">Parameter name</param>
        /// <param name="value">Value</param>
        public void Add(string name, string value)
        {
            IParameter parameter;
            if (!this.items.TryGetValue(name, out parameter))
            {
                parameter = new Parameter(name, value);
                this.items.Add(name, parameter);
            }
            else
                parameter.Add(value);
        }

        /// <summary>
        /// Checks if the specified parameter exists
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>;</returns>
        public bool Exists(string name)
        {
            return this.items.ContainsKey(name);
        }

        #endregion

        /// <summary>
        /// Remove all item
        /// </summary>
        public void Clear()
        {
            this.items.Clear();
        }
    }
}