using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kafkaesque.Internals
{
    struct WriteTask
    {
        readonly TaskCompletionSource<object> _taskCompletionSource;

        public WriteTask(byte[][] data, CancellationToken cancellationToken)
        {
            Data = data;
            _taskCompletionSource = new TaskCompletionSource<object>();
            cancellationToken.Register(TryCancelTask);
        }

        public byte[][] Data { get; }

        public Task Task => _taskCompletionSource.Task;

        public void Complete() => _taskCompletionSource.TrySetResult(null);

        public void Fail(Exception exception) => _taskCompletionSource.TrySetException(exception);

        public bool IsCancelled => Task.IsCanceled;

        void TryCancelTask() => _taskCompletionSource.TrySetCanceled();
    }
}