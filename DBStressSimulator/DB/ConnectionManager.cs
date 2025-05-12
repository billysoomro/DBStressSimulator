using Npgsql;
using System.Collections.Concurrent;

namespace DBStressSimulator.DB;

public class ConnectionManager
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<int, NpgsqlConnection> _connections = new();
    private int _nextId = 0;

    public ConnectionManager(string connString)
    {
        _connectionString = connString;
    }

    public int OpenConnection()
    {
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        int id = Interlocked.Increment(ref _nextId);
        _connections.TryAdd(id, conn);
        return id;
    }

    public void CloseAll()
    {
        foreach (var kv in _connections)
        {
            kv.Value.Close();
            kv.Value.Dispose();
        }
        _connections.Clear();
        _nextId = 0;
    }

    public int ActiveCount => _connections.Count;
}
