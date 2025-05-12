//using Npgsql;
//using System.Collections.Concurrent;

//namespace DBStressSimulator.DB;

//public class CpuStressManager
//{
//    private readonly string _connectionString;
//    private readonly ConcurrentDictionary<int, CancellationTokenSource> _tasks = new();
//    private int _nextId = 0;

//    public CpuStressManager(string connStr) => _connectionString = connStr;

//    public void StartWorkers(int count)
//    {
//        for (int i = 0; i < count; i++)
//        {
//            var cts = new CancellationTokenSource();
//            int id = Interlocked.Increment(ref _nextId);
//            _tasks.TryAdd(id, cts);

//            Task.Run(async () =>
//            {
//                using var conn = new NpgsqlConnection(_connectionString);
//                await conn.OpenAsync();

//                while (!cts.Token.IsCancellationRequested)
//                {
//                    using var cmd = new NpgsqlCommand("SELECT generate_series(1, 10000000)", conn);
//                    await cmd.ExecuteReaderAsync(cts.Token);
//                }
//            }, cts.Token);
//        }
//    }

//    public void StopAll()
//    {
//        foreach (var kv in _tasks)
//        {
//            kv.Value.Cancel();
//        }
//        _tasks.Clear();
//        _nextId = 0;
//    }

//    public int ActiveCount => _tasks.Count;
//}




using Npgsql;
using System.Collections.Concurrent;

namespace DBStressSimulator.DB;

public class CpuStressManager
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<int, (Task task, CancellationTokenSource cts)> _workers = new();
    private int _nextId = 0;

    public CpuStressManager(string connStr)
    {
        _connectionString = connStr;       
    }

    public void StartWorkers(int count, StressTestMode mode = StressTestMode.CpuIntensive)
    {
        for (int i = 0; i < count; i++)
        {
            var cts = new CancellationTokenSource();
            int id = Interlocked.Increment(ref _nextId);

            var task = Task.Run(async () =>
            {
                try
                {
                    using var conn = new NpgsqlConnection(_connectionString);
                    await conn.OpenAsync(cts.Token);
                    
                    while (!cts.Token.IsCancellationRequested)
                    {
                        await ExecuteStressQuery(conn, mode, id, cts.Token);
                        //await Task.Delay(100, cts.Token); // Brief pause between queries
                    }
                }
                catch (OperationCanceledException)
                {
                    
                }
                catch (Exception ex)
                {
                   
                }
            }, cts.Token);

            _workers.TryAdd(id, (task, cts));
        }
    }

    private async Task ExecuteStressQuery(NpgsqlConnection conn, StressTestMode mode, int workerId, CancellationToken ct)
    {
        try
        {
            var query = mode switch
            {
                StressTestMode.CpuIntensive => GenerateCpuIntensiveQuery(),
                StressTestMode.MemoryIntensive => GenerateMemoryIntensiveQuery(),
                StressTestMode.Locking => GenerateLockingQuery(),
                _ => GenerateCpuIntensiveQuery()
            };

            using var cmd = new NpgsqlCommand(query, conn)
            {
                CommandTimeout = 0 // No timeout for stress tests
            };

            // Add parameters to prevent query plan caching
            cmd.Parameters.AddWithValue("worker_id", workerId);
            cmd.Parameters.AddWithValue("random_factor", Random.Shared.NextDouble());

            // Execute and read all results to maximize impact
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                // Process each row to ensure full execution
                var _ = reader.GetValue(0);
            }
        }
        catch (Exception ex)
        {
            await Task.Delay(1000, ct); // Wait before retrying
        }
    }

    private string GenerateCpuIntensiveQuery()
    {
        return $@"
WITH RECURSIVE cpu_stress AS (
    -- Initial values
    SELECT 
        1 AS iteration,
        ARRAY[random(), random(), random(), random(), random()] AS vectors,
        ARRAY[random(), random(), random(), random(), random()] AS coefficients
    UNION ALL
    SELECT 
        iteration + 1,
        -- Transform vectors using expensive operations that resist optimization
        ARRAY[
            -- Multiple transcendental functions chained together
            POWER(EXP(COS(vectors[1] * PI() * coefficients[3])), SIN(vectors[2] * PI())),
            
            -- Logarithmic operations with safety checks
            LN(ABS(SIN(vectors[2] * iteration) * COS(coefficients[1] * PI()) + 0.0001)),
            
            -- Factorial-like growth with trig functions
            POWER(2.0, MOD(iteration, 10)) * ATAN(vectors[3] * coefficients[2] * iteration / 1000.0),
            
            -- Pseudorandom sequence using polynomial calculation
            MOD(
                (1664525 * vectors[4]::numeric + 1013904223)::numeric, 
                1.0
            )::float8,
            
            -- Complex compound calculation
            (
                POWER(vectors[5], 3) * SIN(iteration * PI() / 180) + 
                SQRT(ABS(COS(vectors[1] * PI()) * vectors[3]))
            ) / (ABS(TAN(coefficients[4] + 0.1)) + 0.01)
        ],
        
        -- Transform coefficients with different calculations
        ARRAY[
            -- Rotate through transcendental functions
            CASE iteration % 4
                WHEN 0 THEN SIN(coefficients[1] * PI() * vectors[2])
                WHEN 1 THEN COS(coefficients[1] * PI() * vectors[3])
                WHEN 2 THEN TAN(coefficients[1] * PI() * vectors[4] + 0.1)
                ELSE LN(ABS(coefficients[1] * vectors[5]) + 0.1)
            END,
            
            -- Recursive dependency on previous values
            SQRT(ABS(coefficients[2] * vectors[1] + coefficients[3] * vectors[2])),
            
            -- Complex wave pattern
            SIN(iteration / 100.0) * COS(iteration / 75.0) * SIN(coefficients[3] * PI()),
            
            -- Polynomial approximation
            POWER(coefficients[4], 2) - POWER(coefficients[4], 3) / 3 + POWER(coefficients[4], 4) / 5,
            
            -- Chaotic mapping (logistic map variant)
            3.9 * coefficients[5] * (1 - coefficients[5])
        ]
    FROM 
        cpu_stress
    WHERE 
        iteration < {25000}
),
-- Perform additional calculations on the results to prevent query optimization
intermediate_calculations AS (
    SELECT
        iteration,
        -- Calculate matrix-like operations between vectors and coefficients
        vectors[1] * coefficients[1] + vectors[2] * coefficients[2] AS dot_product1,
        vectors[3] * coefficients[3] + vectors[4] * coefficients[4] AS dot_product2,
        -- Measure statistical properties
        (vectors[1] + vectors[2] + vectors[3] + vectors[4] + vectors[5]) / 5 AS avg_vector,
        (coefficients[1] + coefficients[2] + coefficients[3] + coefficients[4] + coefficients[5]) / 5 AS avg_coef,
        -- Calculate Euclidean-like distance
        SQRT(
            POWER(vectors[1] - coefficients[1], 2) +
            POWER(vectors[2] - coefficients[2], 2) +
            POWER(vectors[3] - coefficients[3], 2) +
            POWER(vectors[4] - coefficients[4], 2) +
            POWER(vectors[5] - coefficients[5], 2)
        ) AS distance,
        -- Trig wave formations
        SIN(vectors[1] * 2 * PI()) * COS(coefficients[2] * 2 * PI()) AS wave_product
    FROM 
        cpu_stress
    -- Only calculate for a subset to avoid excessive memory usage
    WHERE 
        iteration % 100 = 0
        OR iteration = {25000} - 1
),
-- Perform window functions which are very CPU intensive
window_calculations AS (
    SELECT
        iteration,
        dot_product1,
        dot_product2,
        avg_vector,
        avg_coef,
        distance,
        wave_product,
        -- Intensive window functions
        AVG(dot_product1) OVER (
            ORDER BY iteration
            ROWS BETWEEN 5 PRECEDING AND 5 FOLLOWING
        ) AS moving_avg_dot1,
        STDDEV(distance) OVER (
            ORDER BY iteration
            ROWS BETWEEN 10 PRECEDING AND CURRENT ROW
        ) AS stddev_distance,
        RANK() OVER (
            ORDER BY dot_product1 DESC
        ) AS rank_by_dot1,
        PERCENT_RANK() OVER (
            ORDER BY distance ASC
        ) AS percent_rank_distance,
        -- Calculate correlation approximation manually
        AVG(dot_product1 * distance) OVER (
            ORDER BY iteration
            ROWS BETWEEN 20 PRECEDING AND 20 FOLLOWING
        ) - 
        AVG(dot_product1) OVER (
            ORDER BY iteration 
            ROWS BETWEEN 20 PRECEDING AND 20 FOLLOWING
        ) * 
        AVG(distance) OVER (
            ORDER BY iteration
            ROWS BETWEEN 20 PRECEDING AND 20 FOLLOWING
        ) AS covariance_estimate
    FROM
        intermediate_calculations
)

-- Final calculation that ensures all prior computations are necessary
-- and cannot be optimized away by the query planner
SELECT
    MIN(iteration) AS min_iter,
    MAX(iteration) AS max_iter,
    -- Force complex aggregations
    ROUND(
        SQRT(AVG(POWER(dot_product1, 2) + POWER(dot_product2, 2))), 
        6
    ) AS rms_dots,
    -- Correlation approximation
    ROUND(
        SUM(dot_product1 * distance) / 
        (SQRT(SUM(POWER(dot_product1, 2))) * SQRT(SUM(POWER(distance, 2)))),
        6
    ) AS correlation,
    -- Aggregations that depend on window functions
    ROUND(AVG(wave_product * moving_avg_dot1 * stddev_distance), 6) AS complex_metric1,
    ROUND(SUM(distance * percent_rank_distance) / COUNT(*), 6) AS complex_metric2,
    -- Force statistical calculations
    STDDEV(covariance_estimate) AS std_covariance,
    -- Calculate hashes of concatenated values to force computation of all values
    -- This guarantees no intermediate results can be optimized away
    MD5(string_agg(
        rank_by_dot1::text || '_' || 
        ROUND(percent_rank_distance::numeric, 4)::text || '_' ||
        ROUND(covariance_estimate::numeric, 4)::text,
        ','
        ORDER BY iteration
    )) AS result_hash
FROM
    window_calculations;
";
    }


    private string GenerateMemoryIntensiveQuery()
    {
        return @"
        SELECT 
            md5(seq::text) || md5((seq*@random_factor)::text) AS hash_value,
            (SELECT sum(seq) FROM generate_series(1, 100000) seq) AS big_sum
        FROM 
            generate_series(1, 1000000) seq
        WHERE 
            seq % (@worker_id * 100) = 0";
    }

    private string GenerateLockingQuery()
    {
        return @"
        BEGIN;
        LOCK TABLE orders IN EXCLUSIVE MODE;
        SELECT pg_sleep(5 * @random_factor);
        COMMIT;
        
        -- Generate some CPU load after locking
        SELECT count(*) FROM (
            SELECT sqrt(seq * @random_factor) FROM generate_series(1, 10000000) seq
        ) t";
    }

    public async Task StopAll()
    {
        // Cancel all workers
        foreach (var (id, (_, cts)) in _workers)
        {
            cts.Cancel();            
        }

        // Wait for all tasks to complete
        await Task.WhenAll(_workers.Values.Select(x => x.task));

        _workers.Clear();
        _nextId = 0;
    }

    public int ActiveCount => _workers.Count;
}

public enum StressTestMode
{
    CpuIntensive,
    MemoryIntensive,
    Locking
}