using Dibix;
using StoreLake.TestStore.Server;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp4
{
    public class xDatabaseAccessorFactory : Dibix.IDatabaseAccessorFactory
    {
        private readonly StoreLakeDabaseAccessorGate accessorGate;
        private readonly DbProviderFactory dbClient;
        private readonly string connectionString;
        private readonly StoreLakeDbServer dbServer;
        public xDatabaseAccessorFactory(StoreLakeDbServer dbServer, StoreLakeDabaseAccessorGate accessorGate, DbProviderFactory dbClient, string connectionString)
        {
            this.dbServer = dbServer;
            this.accessorGate = accessorGate;
            this.dbClient = dbClient;
            this.connectionString = connectionString;
        }

        public Dibix.IDatabaseAccessor Create()
        {
            var connection = CreateConnection();
            DataSet db =  dbServer.GetDatabaseForConnection(connection);
            return new StoreLakeDabaseAccessor(db, accessorGate, new Dibix.Dapper.DapperDatabaseAccessor(connection));
        }

        public DbConnection CreateConnection()
        {
            DbConnection connection = dbClient.CreateConnection();
            connection.ConnectionString = connectionString;
            return connection;
        }
    }
    public sealed class StoreLakeDabaseAccessor : Dibix.IDatabaseAccessor
    {
        private DataSet db;
        private StoreLakeDabaseAccessorGate gate;
        private Dibix.IDatabaseAccessor accessor;
        public StoreLakeDabaseAccessor(DataSet db, StoreLakeDabaseAccessorGate accessorGate, Dibix.IDatabaseAccessor accessor)
        {
            this.db = db;
            this.gate = accessorGate;
            this.accessor = accessor;
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                gate = null;

                var tmp = accessor;
                accessor = null;
                if (tmp != null)
                {
                    tmp.Dispose();
                }
            }

        }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        int IDatabaseAccessor.Execute(string sql, CommandType commandType, int? commandTimeout, IParametersVisitor parameters)
        {
            return accessor.Execute(sql, commandType, commandTimeout, parameters);
        }

        Task<int> IDatabaseAccessor.ExecuteAsync(string sql, CommandType commandType, int? commandTimeout, IParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IParameterBuilder IDatabaseAccessor.Parameters()
        {
            return accessor.Parameters();
        }

        IEnumerable<T> IDatabaseAccessor.QueryMany<T>(string sql, CommandType commandType, IParametersVisitor parameters)
        {
            return accessor.QueryMany<T>(sql, commandType, parameters);
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TReturn, TSecond>(string sql, CommandType commandType, IParametersVisitor parameters, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TReturn, TSecond, TThird>(string sql, CommandType commandType, IParametersVisitor parameters, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TFirst, TSecond, TReturn>(string sql, CommandType commandType, IParametersVisitor parameters, Func<TFirst, TSecond, TReturn> map, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TFirst, TSecond, TThird, TReturn>(string sql, CommandType commandType, IParametersVisitor parameters, Func<TFirst, TSecond, TThird, TReturn> map, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TFirst, TSecond, TThird, TFourth, TReturn>(string sql, CommandType commandType, IParametersVisitor parameters, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sql, CommandType commandType, IParametersVisitor parameters, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(string sql, CommandType commandType, IParametersVisitor parameters, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TReturn>(string sql, CommandType commandType, IParametersVisitor parameters, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TReturn> map, string splitOn)
        {
            throw new NotImplementedException();
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, TEleventh, TReturn>(string sql, CommandType commandType, IParametersVisitor parameters, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TEighth, TNinth, TTenth, TEleventh, TReturn> map, string splitOn)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<T>> IDatabaseAccessor.QueryManyAsync<T>(string sql, CommandType commandType, IParametersVisitor parameters, bool buffered, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IMultipleResultReader IDatabaseAccessor.QueryMultiple(string sql, CommandType commandType, IParametersVisitor parameters)
        {
            throw new NotImplementedException();
        }

        Task<IMultipleResultReader> IDatabaseAccessor.QueryMultipleAsync(string sql, CommandType commandType, IParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        T IDatabaseAccessor.QuerySingle<T>(string sql, CommandType commandType, IParametersVisitor parameters)
        {
            return accessor.QuerySingle<T>(sql, commandType, parameters);
        }

        TReturn IDatabaseAccessor.QuerySingle<TReturn, TSecond>(string sql, CommandType commandType, IParametersVisitor parameters, string splitOn)
        {
            throw new NotImplementedException();
        }

        TReturn IDatabaseAccessor.QuerySingle<TReturn, TSecond, TThird>(string sql, CommandType commandType, IParametersVisitor parameters, string splitOn)
        {
            throw new NotImplementedException();
        }

        TReturn IDatabaseAccessor.QuerySingle<TReturn, TSecond, TThird, TFourth>(string sql, CommandType commandType, IParametersVisitor parameters, string splitOn)
        {
            throw new NotImplementedException();
        }

        Task<T> IDatabaseAccessor.QuerySingleAsync<T>(string sql, CommandType commandType, IParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        T IDatabaseAccessor.QuerySingleOrDefault<T>(string sql, CommandType commandType, IParametersVisitor parameters)
        {
            Func<DataSet, IParametersVisitor, object> handler = gate.TryGetHandlerRead(sql); // 1xSet
            if (handler != null)
            {
                object result_object = handler(db, parameters);
                return (T)result_object;
            }
            return accessor.QuerySingleOrDefault<T>(sql, commandType, parameters);
        }


    }

}
