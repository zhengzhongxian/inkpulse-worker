using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;
using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Infrastructure.Persistence.Implementations
{
    public class DapperRepository(WorkerDbContext dbContext, IConfiguration configuration) : IDapperRepository
    {
        private readonly string _connectionString =
            configuration.GetSection(KeyConstant.ConnectionStrings.DefaultConnection).Value 
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Host=write.db.inkpulse.com;Port=5432;Database=enterprise_db;Username=postgres;Password=AdminSecret123";
        
        private IDbTransaction? TryGetRealTransaction(IDbTransaction? transaction)
        {
            if (transaction != null)
            {
                return transaction;
            }

            return dbContext.Database.CurrentTransaction?.GetDbTransaction();
        }

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var realTx = TryGetRealTransaction(transaction);

            var connection = realTx?.Connection ?? new NpgsqlConnection(_connectionString);
            if (realTx == null) await ((NpgsqlConnection)connection).OpenAsync(cancellationToken);

            try
            {
                var def = new CommandDefinition(sql, param, realTx, cancellationToken: cancellationToken);
                var result = await connection.QueryAsync<T>(def);
                return result.AsList();
            }
            finally
            {
                if (realTx == null && connection is NpgsqlConnection npgsqlConn) await npgsqlConn.DisposeAsync();
            }
        }

        public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var realTx = TryGetRealTransaction(transaction);
            var connection = realTx?.Connection ?? new NpgsqlConnection(_connectionString);

            if (realTx == null) await ((NpgsqlConnection)connection).OpenAsync(cancellationToken);

            try
            {
                var def = new CommandDefinition(sql, param, realTx, cancellationToken: cancellationToken);
                return await connection.QueryFirstOrDefaultAsync<T>(def);
            }
            finally
            {
                if (realTx == null && connection is NpgsqlConnection npgsqlConn) await npgsqlConn.DisposeAsync();
            }
        }

        public async Task<T> QuerySingleAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var realTx = TryGetRealTransaction(transaction);
            var connection = realTx?.Connection ?? new NpgsqlConnection(_connectionString);

            if (realTx == null) await ((NpgsqlConnection)connection).OpenAsync(cancellationToken);

            try
            {
                var def = new CommandDefinition(sql, param, realTx, cancellationToken: cancellationToken);
                return await connection.QuerySingleAsync<T>(def);
            }
            finally
            {
                if (realTx == null && connection is NpgsqlConnection npgsqlConn) await npgsqlConn.DisposeAsync();
            }
        }

        public async Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var realTx = TryGetRealTransaction(transaction);
            var connection = realTx?.Connection ?? new NpgsqlConnection(_connectionString);

            if (realTx == null) await ((NpgsqlConnection)connection).OpenAsync(cancellationToken);

            try
            {
                var def = new CommandDefinition(sql, param, realTx, cancellationToken: cancellationToken);
                return await connection.ExecuteAsync(def);
            }
            finally
            {
                if (realTx == null && connection is NpgsqlConnection npgsqlConn) await npgsqlConn.DisposeAsync();
            }
        }

        public async Task<T> ExecuteScalarAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            var realTx = TryGetRealTransaction(transaction);
            var connection = realTx?.Connection ?? new NpgsqlConnection(_connectionString);

            if (realTx == null) await ((NpgsqlConnection)connection).OpenAsync(cancellationToken);

            try
            {
                var def = new CommandDefinition(sql, param, realTx, cancellationToken: cancellationToken);
                return (await connection.ExecuteScalarAsync<T>(def))!;
            }
            finally
            {
                if (realTx == null && connection is NpgsqlConnection npgsqlConn) await npgsqlConn.DisposeAsync();
            }
        }
    }
}
