using System;
using Griffin.Networking.Protocol.Http.Protocol;

namespace Griffin.Networking.Protocol.Http.Implementation
{
    /// <summary>
    /// Parses a request cookie header value.
    /// </summary>
    /// <remarks>This class is not thread safe.</remarks>
    public class HttpCookieParser
    {
        private readonly string headerValue;
        private HttpCookieCollection<IHttpCookie> cookies;
        private int index;
        private string cookieName = "";
        private Action parserMethod;
        private string cookieValue = "";


        /// <summary>
        /// Initializes a new instance of the <see cref="HttpCookieParser" /> class.
        /// </summary>
        /// <param name="headerValue">The header value.</param>
        public HttpCookieParser(string headerValue)
        {
            if (headerValue == null) throw new ArgumentNullException("headerValue");
            this.headerValue = headerValue;
        }

        private char Current
        {
            get
            {
                if (this.index >= this.headerValue.Length)
                    return char.MinValue;

                return this.headerValue[this.index];
            }
        }

        protected bool IsEof
        {
            get { return this.index >= this.headerValue.Length; }
        }

        protected void Name_Before()
        {
            while (char.IsWhiteSpace(this.Current))
            {
                this.MoveNext();
            }

            this.parserMethod = this.Name;
        }

        protected virtual void Name()
        {
            while (!char.IsWhiteSpace(this.Current) && this.Current != '=')
            {
                this.cookieName += this.Current;
                this.MoveNext();
            }

            this.parserMethod = this.Name_After;
        }

        protected virtual void Name_After()
        {
            while (char.IsWhiteSpace(this.Current) || this.Current == ':')
            {
                this.MoveNext();
            }

            this.parserMethod = this.Value_Before;
        }

        protected virtual void Value_Before()
        {
            if (this.Current == '"')
                this.parserMethod = this.Value_Qouted;
            else
                this.parserMethod = this.Value;

            this.MoveNext();
        }

        private void Value()
        {
            while (this.Current != ';' && !this.IsEof)
            {
                this.cookieValue += this.Current;
                this.MoveNext();
            }

            this.parserMethod = this.Value_After;
        }

        private void Value_Qouted()
        {
            this.MoveNext(); // skip '"'

            var last = char.MinValue;
            while (this.Current != '"' && !this.IsEof)
            {
                if (this.Current == '"' && last == '\\')
                {
                    this.cookieValue += '#';
                    this.MoveNext();
                }
                else
                {
                    this.cookieValue += this.Current;
                }

                last = this.Current;
                this.MoveNext();
            }

            this.parserMethod = this.Value_After;
        }

        private void Value_After()
        {
            this.OnCookie(this.cookieName, this.cookieValue);
            this.cookieName = "";
            this.cookieValue = "";
            while (char.IsWhiteSpace(this.Current) || this.Current == ';')
            {
                this.MoveNext();
            }

            this.parserMethod = this.Name_Before;
        }

        private void OnCookie(string name, string value)
        {
            if (name == null) throw new ArgumentNullException("name");

            this.cookies.Add(new HttpCookie(name, value));
        }

        private void MoveNext()
        {
            if (!this.IsEof)
                ++this.index;
        }

        /// <summary>
        /// Parse cookie string
        /// </summary>
        /// <returns>A generated cookie collection.</returns>
        public IHttpCookieCollection<IHttpCookie> Parse()
        {
            this.cookies = new HttpCookieCollection<IHttpCookie>();
            this.parserMethod = this.Name_Before;

            while (!this.IsEof)
            {
                this.parserMethod();
            }

            this.OnCookie(this.cookieName, this.cookieValue);
            return this.cookies;
        }
    }
}