using Prometheus;

namespace EFMonitor
{
    public static class DbErrorRateMetrics
    {
        public static void RegisterQuery(bool failed, bool longTimeQuery)
        {
            DbMetrics.TotalQueries.Inc();

            if (failed)
            {
                DbMetrics.FailedQueries.Inc();
            }

            if (longTimeQuery)
            {
                DbMetrics.LongTimeQueries.Inc();
            }
        }
    }

    public static class DbMetrics
    {
        public static readonly Counter TotalQueries =
            Metrics.CreateCounter("db_queries_total", "Общее количество SQL-запросов");

        public static readonly Counter FailedQueries =
            Metrics.CreateCounter("db_queries_failed_total", "Количество упавших SQL-запросов");

        public static readonly Counter LongTimeQueries =
            Metrics.CreateCounter("db_queries_longtime_total", "Количество медленных SQL-запросов(>=1сек)");
    }
}