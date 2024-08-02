using Microsoft.EntityFrameworkCore.Diagnostics;
using Polly.Retry;
using System.Data.Common;

namespace DelikatessenDrehbuch.RetryInterceptor
{
    public class RetryInterceptor : DbCommandInterceptor
{
    private readonly AsyncRetryPolicy _retryPolicy;

    public RetryInterceptor(AsyncRetryPolicy retryPolicy)
    {
        _retryPolicy = retryPolicy;
    }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
             DbCommand command,
             CommandEventData eventData,
             InterceptionResult<DbDataReader> result,
             CancellationToken cancellationToken = default)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            });
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var executionResult =  await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
            return executionResult;
        });
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var executionResult = await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
            return executionResult;
        });
    }
}
   
}
