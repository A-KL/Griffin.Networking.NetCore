﻿using System.Runtime.CompilerServices;

namespace Griffin.Networking.Buffers
{
    /// <summary>
    /// Contains buffer handling
    /// </summary>
    /// <remarks>Do note that it's recommended that you pool buffers (i.e.) reuse them. You can use the <see cref="BufferSliceStack"/> for that.
    /// 
    /// <para>All buffers that are reusable should implement <c>IDisposable</c>. Buffers are returned to the pool by calling <c>Dispose()</c>. You therefore
    /// have to check each buffer if it's disposable when you are done with it (and dispose it if it is).</para>
    /// </remarks>
    [CompilerGenerated]
    internal class NamespaceDoc
    {
    }
}