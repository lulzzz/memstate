﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Memstate
{
    public class Batcher<T> : IAsyncDisposable
    {
        private readonly int _maxBatchSize;

        private readonly BlockingCollection<T> _items;

        private readonly Task _batchTask;

        private readonly Action<IEnumerable<T>> _batchHandler;

        private readonly ILogger _logger;

        public Batcher(MemstateSettings config, Action<IEnumerable<T>> batchHandler)
        {
            _logger = config.CreateLogger<Batcher<T>>();
            _batchHandler = batchHandler;
            _maxBatchSize = config.MaxBatchSize;
            _items = new BlockingCollection<T>(config.MaxBatchQueueLength);
            _batchTask = new Task(ProcessItems, TaskCreationOptions.LongRunning);
            _batchTask.Start();
        }

        public void Add(T item)
        {
            _items.Add(item);
        }

        public async Task DisposeAsync()
        {
            _logger.LogDebug("Begin Dispose");

            _items.CompleteAdding();

            await _batchTask.ConfigureAwait(false);

            _logger.LogDebug("End Dispose");
        }

        private void ProcessItems()
        {
            var buffer = new List<T>(_maxBatchSize);

            while (!_items.IsCompleted)
            {
                if (!_items.TryTake(out var firstItem, 1000))
                {
                    continue;
                }

                buffer.Add(firstItem);

                while (buffer.Count < _maxBatchSize && _items.TryTake(out var item))
                {
                    buffer.Add(item);
                }

                _batchHandler.Invoke(buffer);

                buffer.Clear();
            }
        }
    }
}