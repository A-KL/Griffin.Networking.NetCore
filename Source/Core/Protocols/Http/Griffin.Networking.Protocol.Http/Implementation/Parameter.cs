using System;
using System.Collections;
using System.Collections.Generic;
using Griffin.Networking.Protocol.Http.Protocol;

namespace Griffin.Networking.Protocol.Http.Implementation
{
    internal class Parameter : IParameter
    {
        private readonly List<string> values = new List<string>();

        public Parameter(string name, string value)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (value == null) throw new ArgumentNullException("value");
            this.Name = name;
            this.values.Add(value);
        }

        /// <summary>
        /// Gets a list of all values.
        /// </summary>
        public IEnumerable<string> Values
        {
            get { return this.values; }
        }

        #region IParameter Members

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<string> GetEnumerator()
        {
            return this.values.GetEnumerator();
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
        /// Gets *last* value.
        /// </summary>
        /// <remarks>
        /// Parameters can have multiple values. This property will always get the last value in the list.
        /// </remarks>
        /// <value>String if any value exist; otherwise <c>null</c>.</value>
        public string Value
        {
            get { return this.values.Count == 0 ? null : this.values[this.values.Count - 1]; }
        }

        /// <summary>
        /// Gets or sets name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Get one of the values.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public string this[int index]
        {
            get { return this.values[index]; }
        }

        /// <summary>
        /// Get number of values
        /// </summary>
        public int Count
        {
            get { return this.values.Count; }
        }

        /// <summary>
        /// Add a new parameter value
        /// </summary>
        /// <param name="value">Value to add</param>
        public void Add(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            this.values.Add(value);
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return this.Name + ": " + string.Join(", ", this.Values);
        }
    }
}