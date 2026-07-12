    
using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EFMonitor
{
    public class SlowQueryInterceptor : DbCommandInterceptor
    {
        private readonly ILogPort<SlowQueryInterceptor> _logger;
        private readonly TimeSpan _threshold = TimeSpan.FromMilliseconds(1000);

        private readonly ConcurrentDictionary<Guid, Stopwatch> _sw = new();

        public SlowQueryInterceptor(ILogPort<SlowQueryInterceptor> logger)
        {
            _logger = logger;
        }

        //SELECT Executing
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _sw.TryAdd(eventData.CommandId, Stopwatch.StartNew());
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        //SELECT Executed
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (_sw.TryRemove(eventData.CommandId, out var sw))
            {
                sw.Stop();
                var longTime = sw.Elapsed >= _threshold;

                DbMetrics.RegisterQuery(failed: false, longTimeQuery: longTime);
                await LogIfSlow(sw.Elapsed, command);
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        //INSERT/UPDATE/DELETE Executing
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _sw.TryAdd(eventData.CommandId, Stopwatch.StartNew());
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        //INSERT/UPDATE/DELETE Executed
        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (_sw.TryRemove(eventData.CommandId, out var sw))
            {
                sw.Stop();
                var longTime = sw.Elapsed >= _threshold;

                DbMetrics.RegisterQuery(failed: false, longTimeQuery: longTime);
                await LogIfSlow(sw.Elapsed, command);
            }

            return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }

        //ALL FAILED
        public override async Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            DbMetrics.RegisterQuery(failed: true, longTimeQuery: false);

            await base.CommandFailedAsync(command, eventData, cancellationToken);
        }

        private async Task LogIfSlow(TimeSpan elapsed, DbCommand command)
        {
            if (elapsed > _threshold)
            {
                var sql = command.CommandText
                    .Replace("\r", " ")
                    .Replace("\n", " ");
                await _logger.LogWarning(
                    "Медленный SQL ({ElapsedMs:F2} ms): {Sql}",
                    elapsed.TotalMilliseconds,
                    sql);
            }
        }
    }
}
