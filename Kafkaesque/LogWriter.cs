using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kafkaesque
{
    public abstract class LogWriter : IDisposable
    {
        public abstract Task WriteAsync(byte[] data, CancellationToken cancellationToken = default);
        public abstract Task WriteManyAsync(IEnumerable<byte[]> dataSequence, CancellationToken cancellationToken = default);
        public abstract void Dispose();
    }
}