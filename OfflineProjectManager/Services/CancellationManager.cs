using System;
using System.Collections.Concurrent;
using System.Threading;

namespace OfflineProjectManager.Services
{
    public interface ICancellationManager
    {
        /// <summary>
        /// Application-wide cancellation token that fires on shutdown
        /// </summary>
        CancellationToken ApplicationLifetime { get; }

        /// <summary>
        /// Creates a cancellation token for a specific operation
        /// </summary>
        CancellationToken CreateOperationToken(string operationId);

        /// <summary>
        /// Cancels a specific operation by ID
        /// </summary>
        void CancelOperation(string operationId);

        /// <summary>
        /// Cancels all active operations
        /// </summary>
        void CancelAllOperations();
    }

    public class CancellationManager : ICancellationManager, IDisposable
    {
        private readonly CancellationTokenSource _appLifetimeCts = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _operations = new();

        public CancellationToken ApplicationLifetime => _appLifetimeCts.Token;

        public CancellationToken CreateOperationToken(string operationId)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_appLifetimeCts.Token);
            _operations[operationId] = cts;
            return cts.Token;
        }

        public void CancelOperation(string operationId)
        {
            if (_operations.TryRemove(operationId, out var cts))
            {
                try
                {
                    cts.Cancel();
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }

        public void CancelAllOperations()
        {
            foreach (var kvp in _operations)
            {
                try
                {
                    kvp.Value.Cancel();
                }
                catch { /* Ignore cancellation errors */ }
                finally
                {
                    kvp.Value.Dispose();
                }
            }
            _operations.Clear();
        }

        public void Dispose()
        {
            CancelAllOperations();
            _appLifetimeCts.Cancel();
            _appLifetimeCts.Dispose();
        }
    }
}
