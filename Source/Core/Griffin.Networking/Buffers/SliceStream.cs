﻿using System;
using System.IO;

namespace Griffin.Networking.Buffers
{
    /// <summary>
    /// A stream operating on a buffer slice.
    /// </summary>
    public class SliceStream : Stream, IBufferStream, IPeekable
    {
        private readonly IBufferSlice slice;
        private long length;
        private long offset;

        /// <summary>
        /// Initializes a new instance of the <see cref="SliceStream" /> class.
        /// </summary>
        /// <param name="slice">The slice to use.</param>
        /// <exception cref="System.ArgumentNullException">slice</exception>
        public SliceStream(IBufferSlice slice)
        {
            if (slice == null) throw new ArgumentNullException("slice");
            this.slice = slice;
            this.offset = 0;
            this.length = 0;
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="SliceStream" /> class.
        /// </summary>
        /// <param name="slice">The slice.</param>
        /// <param name="length">Number of bytes which have already been written to the buffer.</param>
        /// <exception cref="System.ArgumentNullException">slice</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">length;Must be less or equal to slice length which is  + slice.Count</exception>
        public SliceStream(IBufferSlice slice, int length)
        {
            if (slice == null) throw new ArgumentNullException("slice");
            if (length > slice.Count)
                throw new ArgumentOutOfRangeException("length", length,
                                                      "Must be less or equal to slice length which is " + slice.Count);

            this.slice = slice;
            this.offset = 0;
            this.length = length;
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports reading.
        /// </summary>
        /// <returns>
        /// true if the stream supports reading; otherwise, false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanRead
        {
            get { return true; }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
        /// </summary>
        /// <returns>
        /// true if the stream supports seeking; otherwise, false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanSeek
        {
            get { return true; }
        }

        /// <summary>
        /// When overridden in a derived class, gets a value indicating whether the current stream supports writing.
        /// </summary>
        /// <returns>
        /// true if the stream supports writing; otherwise, false.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override bool CanWrite
        {
            get { return true; }
        }

        /// <summary>
        /// When overridden in a derived class, gets the length in bytes of the stream.
        /// </summary>
        /// <returns>
        /// A long value representing the length of the stream in bytes.
        /// </returns>
        /// <exception cref="T:System.NotSupportedException">A class derived from Stream does not support seeking. </exception><exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>1</filterpriority>
        public override long Length
        {
            get { return this.length; }
        }

        /// <summary>
        /// When overridden in a derived class, gets or sets the position within the current stream.
        /// </summary>
        /// <returns>
        /// The current position within the stream.
        /// </returns>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception><exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception><exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>1</filterpriority>
        public override long Position
        {
            get { return this.offset; }
            set
            {
                if (value > this.Length)
                    throw new ArgumentOutOfRangeException("value", value,
                                                          "Position must be less than the written length which is " + this.Length);
                if (value < 0)
                    throw new ArgumentOutOfRangeException("value", value, "Position must be equal to 0 or larger.");

                this.offset = value;
            }
        }

        #region IBufferStream Members

        /// <summary>
        /// When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> + <paramref name="count" /> - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>
        /// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">offset;count</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset >= buffer.Length || offset < 0)
                throw new ArgumentOutOfRangeException("offset", offset, "Must be 0 >= x < " + this.slice.Count);
            if (count + offset >= this.slice.Count)
                throw new ArgumentOutOfRangeException("count", count,
                                                      "offset + count must be less than slice size: " + this.slice.Count);

            var toRead = Math.Min(count, this.RemainingLength);
            Buffer.BlockCopy(this.slice.Buffer, (int) (this.slice.Offset + this.Position), buffer, offset, toRead);
            this.Position += toRead;
            return toRead;
        }

        /// <summary>
        /// Read one byte
        /// </summary>
        /// <returns>Byte if read; -1 if end of stream.</returns>
        public int Read()
        {
            if (this.RemainingLength == 0)
                return -1;

            var ch = this.slice.Buffer[this.slice.Offset + this.Position];
            ++this.Position;
            return ch;
        }

        /// <summary>
        /// Write our contents to another stream
        /// </summary>
        /// <param name="other">Stream to write to</param>
        /// <param name="count">Number of bytes to write</param>
        /// <returns>Bytes written</returns>
        public new int CopyTo(Stream other, int count)
        {
            if (other == null) throw new ArgumentNullException("other");
            if (count + this.Position > this.slice.Count)
                throw new ArgumentOutOfRangeException("count", count,
                                                      "Count+Position is larger than the allocated buffer size");

            other.Write(this.slice.Buffer, (int) (this.slice.Offset + this.Position), count);
            this.Position = this.Position + count;
            return count;
        }

        /// <summary>
        /// Gets number of bytes left from the current postion to <see cref="IBufferWrapper.Count"/>.
        /// </summary>
        public int RemainingLength
        {
            get { return (int) (this.Length - this.Position); }
        }

        /// <summary>
        /// When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count" /> bytes from <paramref name="buffer" /> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer" /> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        /// <exception cref="System.ArgumentNullException">buffer</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">offset;count;</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException("offset", offset, "Must be 0 >= x < " + buffer.Length);
            if (count + this.Position >= this.slice.Count)
                throw new ArgumentOutOfRangeException("count", count,
                                                      "Position + count must be less than " + this.slice.Count);
            if (offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException("offset", offset,
                                                      "Offset + Count must be less than " + buffer.Length);

            Buffer.BlockCopy(buffer, offset, this.slice.Buffer, (int) (this.slice.Offset + this.Position), count);

            var newCount = this.Position + count;
            if (newCount > this.length)
                this.length = newCount;

            this.Position += count;
        }

        /// <summary>
        /// Copy everything from the specified stream into this writer.
        /// </summary>
        /// <param name="stream">Stream to copy information from.</param>
        public void Copy(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            stream.CopyTo(this);
        }

        int IBufferWrapper.Count
        {
            get { return (int) this.Length; }
        }

        int IBufferWrapper.Capacity
        {
            get { return this.slice.Count; }
        }

        int IBufferWrapper.Position
        {
            get { return (int) this.Position; }
            set { this.Position = value; }
        }

        #endregion

        #region IPeekable Members

        /// <summary>
        /// Peek at the next byte in the sequence.
        /// </summary>
        /// <returns>Char if not EOF; otherwise <see cref="char.MinValue"/></returns>
        public char Peek()
        {
            if (this.RemainingLength <= 0)
                return char.MinValue;

            return (char) this.slice.Buffer[this.slice.Offset + this.Position + 1];
        }

        #endregion

        /// <summary>
        /// When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <exception cref="T:System.IO.IOException">An I/O error occurs. </exception><filterpriority>2</filterpriority>
        public override void Flush()
        {
        }

        /// <summary>
        /// When overridden in a derived class, sets the position within the current stream.
        /// </summary>
        /// <returns>
        /// The new position within the current stream.
        /// </returns>
        /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter. </param><param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"/> indicating the reference point used to obtain the new position. </param><exception cref="T:System.IO.IOException">An I/O error occurs. </exception><exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception><exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>1</filterpriority>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    this.Position = offset;
                    break;
                case SeekOrigin.Current:
                    this.Position += offset;
                    break;
                default:
                    this.Position = this.Length - offset;
                    break;
            }

            return this.Position;
        }

        /// <summary>
        /// When overridden in a derived class, sets the length of the current stream.
        /// </summary>
        /// <param name="value">The desired length of the current stream in bytes. </param><exception cref="T:System.IO.IOException">An I/O error occurs. </exception><exception cref="T:System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception><exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>2</filterpriority>
        public override void SetLength(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException("value", value, "Length must be 0 or larger.");

            this.length = value;
        }
    }
}