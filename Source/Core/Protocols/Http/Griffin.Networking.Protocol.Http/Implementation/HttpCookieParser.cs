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
                if (index >= headerValue.Length)
                    return char.MinValue;

                return headerValue[index];
            }
        }

        protected bool IsEof
        {
            get { return index >= headerValue.Length; }
        }

        protected void Name_Before()
        {
            while (char.IsWhiteSpace(Current))
            {
                MoveNext();
            }

            parserMethod = Name;
        }

        protected virtual void Name()
        {
            while (!char.IsWhiteSpace(Current) && Current != '=')
            {
                cookieName += Current;
                MoveNext();
            }

            parserMethod = Name_After;
        }

        protected virtual void Name_After()
        {
            while (char.IsWhiteSpace(Current) || Current == ':')
            {
                MoveNext();
            }

            parserMethod = Value_Before;
        }

        protected virtual void Value_Before()
        {
            if (Current == '"')
                parserMethod = Value_Qouted;
            else
                parserMethod = Value;

            MoveNext();
        }

        private void Value()
        {
            while (Current != ';' && !IsEof)
            {
                cookieValue += Current;
                MoveNext();
            }

            parserMethod = Value_After;
        }

        private void Value_Qouted()
        {
            MoveNext(); // skip '"'

            var last = char.MinValue;
            while (Current != '"' && !IsEof)
            {
                if (Current == '"' && last == '\\')
                {
                    cookieValue += '#';
                    MoveNext();
                }
                else
                {
                    cookieValue += Current;
                }

                last = Current;
                MoveNext();
            }

            parserMethod = Value_After;
        }

        private void Value_After()
        {
            OnCookie(cookieName, cookieValue);
            cookieName = "";
            cookieValue = "";
            while (char.IsWhiteSpace(Current) || Current == ';')
            {
                MoveNext();
            }

            parserMethod = Name_Before;
        }

        private void OnCookie(string name, string value)
        {
            if (name == null) throw new ArgumentNullException("name");

            cookies.Add(new HttpCookie(name, value));
        }

        private void MoveNext()
        {
            if (!IsEof)
                ++index;
        }

        /// <summary>
        /// Parse cookie string
        /// </summary>
        /// <returns>A generated cookie collection.</returns>
        public IHttpCookieCollection<IHttpCookie> Parse()
        {
            cookies = new HttpCookieCollection<IHttpCookie>();
            parserMethod = Name_Before;

            while (!IsEof)
            {
                parserMethod();
            }

            OnCookie(cookieName, cookieValue);
            return cookies;
        }
    }
}