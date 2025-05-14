using Npgsql;
using Microsoft.Extensions.Logging;

namespace DBStressSimulator.DB
{
    
    public class DatabaseReaderService : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseReaderService> _logger;
        private CancellationTokenSource _cts;
        private Task _readingTask;

        public DatabaseReaderService(string connectionString, ILogger<DatabaseReaderService> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
            _cts = new CancellationTokenSource();
        }

        public void StartReading()
        {
            if (_readingTask == null || _readingTask.IsCompleted)
            {
                _cts = new CancellationTokenSource();
                _readingTask = Task.Run(() => ReadContinuously(_cts.Token));
                _logger.LogInformation("Database reads started");
            }
        }

        private async Task ReadContinuously(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using (var conn = new NpgsqlConnection(_connectionString))
                    using (var cmd = new NpgsqlCommand("SELECT * FROM Orders", conn))
                    {
                        await conn.OpenAsync(ct);
                        using (var reader = await cmd.ExecuteReaderAsync(ct))
                        {
                            while (await reader.ReadAsync(ct))
                            {
                                var order = new
                                {
                                    Id = reader.GetInt32(0),
                                    FirstName = reader.GetString(1),
                                    LastName = reader.GetString(2)
                                };
                                _logger.LogInformation($"Read order: {order.Id}");
                            }
                        }
                    }
                    await Task.Delay(1000, ct); // Read every second
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Read operation cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database read failed");
                    await Task.Delay(5000, ct); // Wait before retrying
                }
            }
        }

        public async Task StopReading()
        {
            _cts.Cancel();
            if (_readingTask != null)
            {
                await _readingTask;
                _readingTask = null;
            }
            _logger.LogInformation("Database reads stopped");
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
