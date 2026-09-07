using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace BusNova_System.DataAccess
{
    /// <summary>
    /// The single place BusNova talks to SQL Server.
    ///
    /// Everything goes through parameterised commands. There is deliberately
    /// no method that accepts a pre-built SQL string with values already
    /// concatenated into it, because that is how SQL injection gets in.
    /// If you need a new query, add a method here or pass parameters via P().
    /// </summary>
    public static class Db
    {
        /// <summary>
        /// Reads the BusNovaDb connection string from Web.config.
        /// Throws a clear message rather than a NullReferenceException if it
        /// is missing, because "connection string not configured" is a much
        /// easier thing to debug than "object reference not set".
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                var setting = ConfigurationManager.ConnectionStrings["BusNovaDb"];
                if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
                {
                    throw new ConfigurationErrorsException(
                        "The 'BusNovaDb' connection string is missing from Web.config. " +
                        "Add it under <connectionStrings> before running the application.");
                }
                return setting.ConnectionString;
            }
        }

        /// <summary>Opens a new connection. The caller owns it and must dispose it.</summary>
        public static SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Builds a parameter, turning a null value into DBNull so that
        /// callers never have to remember to do it.
        /// </summary>
        public static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        /// <summary>Runs INSERT / UPDATE / DELETE. Returns rows affected.</summary>
        public static int Execute(string sql, params SqlParameter[] parameters)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>Runs a query that returns one value (COUNT, an inserted id, and so on).</summary>
        public static object Scalar(string sql, params SqlParameter[] parameters)
        {
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                return command.ExecuteScalar();
            }
        }

        /// <summary>Convenience wrapper around Scalar for counts and existence checks.</summary>
        public static int ScalarInt(string sql, params SqlParameter[] parameters)
        {
            var result = Scalar(sql, parameters);
            if (result == null || result == DBNull.Value) return 0;
            return Convert.ToInt32(result);
        }

        /// <summary>Runs a SELECT and maps every row with the supplied function.</summary>
        public static List<T> Query<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters)
        {
            var results = new List<T>();
            using (var connection = OpenConnection())
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null) command.Parameters.AddRange(parameters);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(map(reader));
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Runs a SELECT expected to return at most one row.
        /// Returns default(T) - null for reference types - when nothing matched.
        /// </summary>
        public static T QuerySingle<T>(string sql, Func<IDataRecord, T> map, params SqlParameter[] parameters)
        {
            var results = Query(sql, map, parameters);
            return results.Count > 0 ? results[0] : default(T);
        }

        // ---------- null-safe readers ----------
        // SqlDataReader throws if you read a NULL into a value type, so every
        // nullable column is read through one of these.

        public static string GetString(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? null : record.GetString(i);
        }

        public static int GetInt(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? 0 : Convert.ToInt32(record.GetValue(i));
        }

        public static int? GetNullableInt(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? (int?)null : Convert.ToInt32(record.GetValue(i));
        }

        public static decimal GetDecimal(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? 0m : Convert.ToDecimal(record.GetValue(i));
        }

        public static decimal? GetNullableDecimal(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? (decimal?)null : Convert.ToDecimal(record.GetValue(i));
        }

        public static DateTime GetDateTime(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? DateTime.MinValue : Convert.ToDateTime(record.GetValue(i));
        }

        public static DateTime? GetNullableDateTime(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? (DateTime?)null : Convert.ToDateTime(record.GetValue(i));
        }

        public static TimeSpan GetTimeSpan(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? TimeSpan.Zero : (TimeSpan)record.GetValue(i);
        }

        public static TimeSpan? GetNullableTimeSpan(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return record.IsDBNull(i) ? (TimeSpan?)null : (TimeSpan)record.GetValue(i);
        }

        public static bool GetBool(IDataRecord record, string column)
        {
            var i = record.GetOrdinal(column);
            return !record.IsDBNull(i) && Convert.ToBoolean(record.GetValue(i));
        }

        /// <summary>
        /// True when the exception is a unique-key or unique-index violation.
        /// Used so that screens can turn a race-condition collision into a
        /// friendly message instead of an error page.
        /// </summary>
        public static bool IsUniqueViolation(SqlException ex)
        {
            if (ex == null) return false;
            foreach (SqlError error in ex.Errors)
            {
                // 2627 = unique constraint, 2601 = unique index
                if (error.Number == 2627 || error.Number == 2601) return true;
            }
            return false;
        }
    }
}
