using System;
using System.Collections.Generic;
using System.Linq;

namespace Day24.AdvancedSql;

public record WindowRowResult<T>(
    T Item,
    long RowNumber,
    long Rank,
    long DenseRank,
    decimal RunningTotal,
    decimal MovingAverage,
    decimal? LagValue,
    decimal? LeadValue
);

/// <summary>
/// In-Memory Analytical SQL Window Functions Engine & Query Generator.
/// Implements standard SQL ANSI:2003 window function semantics:
/// - ROW_NUMBER(): Strict sequential increment per partition
/// - RANK(): Tied ranks with ordinal gap skipping
/// - DENSE_RANK(): Tied ranks without ordinal gaps
/// - LAG() / LEAD(): Inter-row comparative lookups
/// - Moving averages & cumulative running frames
/// </summary>
public static class WindowFunctionsEngine
{
    public static List<WindowRowResult<T>> EvaluateWindowFunctions<T, TKey, TOrder>(
        IEnumerable<T> source,
        Func<T, TKey> partitionSelector,
        Func<T, TOrder> orderSelector,
        Func<T, decimal> metricSelector,
        int movingAverageWindow = 3,
        bool ascending = true)
        where TKey : notnull
        where TOrder : IComparable<TOrder>
    {
        var results = new List<WindowRowResult<T>>();

        var grouped = source.GroupBy(partitionSelector);

        foreach (var group in grouped)
        {
            var orderedList = ascending
                ? group.OrderBy(orderSelector).ToList()
                : group.OrderByDescending(orderSelector).ToList();

            decimal runningSum = 0m;
            var metricHistory = new List<decimal>();

            long denseRankCounter = 0;
            TOrder? lastOrderVal = default;
            bool isFirst = true;

            for (int i = 0; i < orderedList.Count; i++)
            {
                var item = orderedList[i];
                var orderVal = orderSelector(item);
                var metric = metricSelector(item);

                // 1. ROW_NUMBER(): 1-based sequential index
                long rowNumber = i + 1;

                // 2 & 3. RANK() and DENSE_RANK()
                long rank;
                if (isFirst || !EqualityComparer<TOrder>.Default.Equals(orderVal, lastOrderVal))
                {
                    rank = i + 1;
                    denseRankCounter++;
                    lastOrderVal = orderVal;
                    isFirst = false;
                }
                else
                {
                    // Tied rank: duplicate previous rank
                    rank = results.Count > 0 ? results[^1].Rank : 1;
                }
                long denseRank = denseRankCounter;

                // 4. Running Total
                runningSum += metric;

                // 5. Moving Average (ROWS BETWEEN (windowSize-1) PRECEDING AND CURRENT ROW)
                metricHistory.Add(metric);
                var windowSlice = metricHistory.TakeLast(movingAverageWindow).ToList();
                decimal movingAvg = Math.Round(windowSlice.Average(), 4);

                // 6. LAG(1)
                decimal? lag = i > 0 ? metricSelector(orderedList[i - 1]) : null;

                // 7. LEAD(1)
                decimal? lead = i < orderedList.Count - 1 ? metricSelector(orderedList[i + 1]) : null;

                results.Add(new WindowRowResult<T>(
                    Item: item,
                    RowNumber: rowNumber,
                    Rank: rank,
                    DenseRank: denseRank,
                    RunningTotal: runningSum,
                    MovingAverage: movingAvg,
                    LagValue: lag,
                    LeadValue: lead
                ));
            }
        }

        return results;
    }

    /// <summary>
    /// Generates reference production-grade PostgreSQL analytical query featuring CTEs, Windowing, and Partitions.
    /// </summary>
    public static string GeneratePostgreSqlAnalyticalQuery()
    {
        return @"
WITH TimeSeriesDailyAggregates AS (
    SELECT 
        device_id,
        DATE_TRUNC('day', recorded_at) AS reading_day,
        AVG(temperature) AS avg_daily_temp,
        MAX(voltage) AS max_voltage
    FROM sensor_readings
    WHERE recorded_at >= NOW() - INTERVAL '30 days'
    GROUP BY device_id, DATE_TRUNC('day', recorded_at)
),
WindowRankings AS (
    SELECT
        device_id,
        reading_day,
        avg_daily_temp,
        ROW_NUMBER() OVER (PARTITION BY device_id ORDER BY reading_day ASC) AS row_num,
        RANK() OVER (PARTITION BY device_id ORDER BY avg_daily_temp DESC) AS temp_rank,
        DENSE_RANK() OVER (PARTITION BY device_id ORDER BY avg_daily_temp DESC) AS dense_temp_rank,
        LAG(avg_daily_temp, 1) OVER (PARTITION BY device_id ORDER BY reading_day ASC) AS prev_day_temp,
        LEAD(avg_daily_temp, 1) OVER (PARTITION BY device_id ORDER BY reading_day ASC) AS next_day_temp,
        AVG(avg_daily_temp) OVER (
            PARTITION BY device_id 
            ORDER BY reading_day ASC 
            ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
        ) AS seven_day_moving_avg
    FROM TimeSeriesDailyAggregates
)
SELECT 
    device_id,
    reading_day,
    avg_daily_temp,
    (avg_daily_temp - prev_day_temp) AS daily_temp_delta,
    seven_day_moving_avg,
    temp_rank,
    dense_temp_rank
FROM WindowRankings
ORDER BY device_id, reading_day;".Trim();
    }
}
