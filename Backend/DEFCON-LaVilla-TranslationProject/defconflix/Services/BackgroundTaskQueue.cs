using defconflix.Interfaces;
using defconflix.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace defconflix.Services
{
    /// <summary>
    /// Thread-safe background task queue with configurable max size.
    /// Uses ConcurrentQueue with SemaphoreSlim for efficient producer-consumer pattern.
    /// </summary>
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, ValueTask>> _workItems = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly int _maxQueueSize;
        private readonly ILogger<BackgroundTaskQueue> _logger;

        public BackgroundTaskQueue(IOptions<BackgroundJobOptions> options, ILogger<BackgroundTaskQueue> logger)
        {
            _maxQueueSize = options.Value.MaxQueueSize;
            _logger = logger;
        }

        public ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            // Check max queue size (0 = unlimited)
            if (_maxQueueSize > 0 && _workItems.Count >= _maxQueueSize)
            {
                _logger.LogWarning("Background task queue is full ({MaxSize} items). Rejecting new work item.",
                    _maxQueueSize);
                throw new InvalidOperationException($"Background task queue is full. Maximum size: {_maxQueueSize}");
            }

            _workItems.Enqueue(workItem);
            _signal.Release();

            _logger.LogDebug("Work item queued. Queue depth: {Count}", _workItems.Count);

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
