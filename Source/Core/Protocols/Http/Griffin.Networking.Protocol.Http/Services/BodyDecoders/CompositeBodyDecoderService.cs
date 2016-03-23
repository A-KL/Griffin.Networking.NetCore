using System;
using System.Collections.Generic;
using Griffin.Networking.Protocol.Http.Protocol;

namespace Griffin.Networking.Protocol.Http.Services.BodyDecoders
{
    /// <summary>
    /// Can provide one or more decoders.
    /// </summary>
    /// <remarks>The default implementation constructor uses <see cref="UrlFormattedDecoder"/> and <see cref="MultipartDecoder"/></remarks>
    public class CompositeBodyDecoder : IBodyDecoder
    {
        private readonly Dictionary<string, IBodyDecoder> decoders = new Dictionary<string, IBodyDecoder>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeBodyDecoder"/> class.
        /// </summary>
        public CompositeBodyDecoder()
        {
            decoders.Add(UrlFormattedDecoder.MimeType, new UrlFormattedDecoder());
            decoders.Add(MultipartDecoder.MimeType, new MultipartDecoder());
        }

        #region IBodyDecoder Members

        /// <summary>
        /// Parses the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <exception cref="FormatException">Body format is invalid for the specified content type.</exception>
        public bool Decode(IRequest message)
        {
            IBodyDecoder decoder;
            var contentType = GetContentTypeWithoutCharset(message.ContentType);

            if (!decoders.TryGetValue(contentType, out decoder))
                return false;

            decoder.Decode(message);
            return true;
        }

        #endregion

        /// <summary>
        /// Add another handlers.
        /// </summary>
        /// <param name="mimeType">Mime type</param>
        /// <param name="decoder">The decoder implementation. Must be thread safe.</param>
        public void Add(string mimeType, IBodyDecoder decoder)
        {
            if (mimeType == null) throw new ArgumentNullException("mimeType");
            if (decoder == null) throw new ArgumentNullException("decoder");
            decoders[mimeType] = decoder;
        }

        private string GetContentTypeWithoutCharset(string contentType)
        {
            if (!String.IsNullOrEmpty(contentType))
            {
                var pos = contentType.IndexOf(";");

                if (pos > 0)
                {
                    return contentType.Substring(0, pos).Trim();
                }
            }

            return contentType;
        }
    }
}