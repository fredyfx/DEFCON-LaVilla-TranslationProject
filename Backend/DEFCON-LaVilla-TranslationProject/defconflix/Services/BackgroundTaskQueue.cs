using defconflix.Interfaces;
using System.Collections.Concurrent;

namespace defconflix.Services
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, ValueTask>> _workItems = new();
        private readonly SemaphoreSlim _signal = new(0);

        public ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            _workItems.Enqueue(workItem);
            _signal.Release();

            return ValueTask.CompletedTask;
        }

        public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _workItems.TryDequeue(out var workItem);

            return workItem!;
        }

        public bool IsEmpty => _workItems.IsEmpty;
        public int Count => _workItems.Count;
    }

}
