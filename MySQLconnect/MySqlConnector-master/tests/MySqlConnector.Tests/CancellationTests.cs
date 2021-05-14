using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MySqlConnector.Tests
{
	public class CancellationTests : IDisposable
	{
		public CancellationTests()
		{
			m_server = new();
			m_server.Start();

			m_csb = new()
			{
				Server = "localhost",
				Port = (uint) m_server.Port,
			};
		}

		public void Dispose() => m_server.Stop();

		// NOTE: Multiple nested classes in order to force tests to run in parallel against each other

		public class CancelExecuteXWithCommandTimeout : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetSyncMethodSteps))]
			public void Test(int step, int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT {4000 + step};";
				var stopwatch = Stopwatch.StartNew();
				var ex = Assert.Throws<MySqlException>(() => s_executeMethods[method](command));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 1500);
				Assert.Equal(MySqlErrorCode.QueryInterrupted, ex.ErrorCode);

				// connection should still be usable
				Assert.Equal(ConnectionState.Open, connection.State);
				command.CommandText = "SELECT 1;";
				Assert.Equal(1, command.ExecuteScalar());
			}
		}

		public class CancelExecuteXAsyncWithCommandTimeout : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetAsyncMethodSteps))]
			public async Task Test(int step, int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT {4000 + step};";
				var stopwatch = Stopwatch.StartNew();
				var ex = await Assert.ThrowsAsync<MySqlException>(async () => await s_executeAsyncMethods[method](command, default));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 1500);
				Assert.Equal(MySqlErrorCode.QueryInterrupted, ex.ErrorCode);
			}
		}

		public class CancelExecuteXAsyncWithCancellationToken : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetAsyncMethodSteps))]
			public async Task Test(int step, int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 0;
				command.CommandText = $"SELECT {4000 + step};";
				using var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
				var stopwatch = Stopwatch.StartNew();
				var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () => await s_executeAsyncMethods[method](command, source.Token));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 1500);
				var mySqlException = Assert.IsType<MySqlException>(ex.InnerException);
				Assert.Equal(MySqlErrorCode.QueryInterrupted, mySqlException.ErrorCode);
			}
		}

		public class ExecuteXBeforeCommandTimeoutExpires : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetSyncMethodSteps))]
			public void Test(int step, int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				var expected = 100 + step;
				command.CommandText = $"SELECT {expected};";
				var stopwatch = Stopwatch.StartNew();
				var result = s_executeMethods[method](command);
				if (method == 1)
					Assert.Equal(0, result); // ExecuteNonQuery
				else
					Assert.Equal(expected, result);
				Assert.InRange(stopwatch.ElapsedMilliseconds, 50, 250);
			}
		}

		public class ExecuteXAsyncBeforeCancellationTokenCancels : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetAsyncMethodSteps))]
			public async Task Test(int step, int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 0;
				var expected = 100 + step;
				command.CommandText = $"SELECT {expected};";
				using var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
				var stopwatch = Stopwatch.StartNew();
				var result = await s_executeAsyncMethods[method](command, source.Token);
				if (method == 1)
					Assert.Equal(0, result); // ExecuteNonQuery
				else
					Assert.Equal(expected, result);
				Assert.InRange(stopwatch.ElapsedMilliseconds, 50, 250);
			}
		}

		public class ExecuteXWithLongAggregateTime : CancellationTests
		{
			[SkipCITheory]
			[InlineData(0)]
			[InlineData(1)]
			public void Timeout(int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT 100;";
				var stopwatch = Stopwatch.StartNew();
				var ex = Assert.Throws<MySqlException>(() => s_executeMethods[method](command));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 1500);
				Assert.Equal(MySqlErrorCode.QueryInterrupted, ex.ErrorCode);
			}

			[SkipCITheory]
			[InlineData(2)]
			public void NoTimeout(int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT 100;";
				var stopwatch = Stopwatch.StartNew();
				var result = s_executeMethods[method](command);
				Assert.Equal(100, result);
				Assert.InRange(stopwatch.ElapsedMilliseconds, 1100, 1500);
			}
		}

		public class ExecuteXAsyncWithLongAggregateTime : CancellationTests
		{
			[SkipCITheory]
			[InlineData(0)]
			[InlineData(1)]
			public async Task Timeout(int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT 100;";
				var stopwatch = Stopwatch.StartNew();
				var ex = await Assert.ThrowsAsync<MySqlException>(async () => await s_executeAsyncMethods[method](command, default));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 1500);
				Assert.Equal(MySqlErrorCode.QueryInterrupted, ex.ErrorCode);
			}

			[SkipCITheory]
			[InlineData(2)]
			public async Task NoTimeout(int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT 100;";
				var stopwatch = Stopwatch.StartNew();
				var result = await s_executeAsyncMethods[method](command, default);
				Assert.Equal(100, result);
				Assert.InRange(stopwatch.ElapsedMilliseconds, 1100, 1500);
			}
		}

		public class ExecuteXTimeout : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetSyncMethodSteps))]
			public void Test(int step, int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT {10000 + step};";
				var stopwatch = Stopwatch.StartNew();
				var ex = Assert.Throws<MySqlException>(() => s_executeMethods[method](command));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 2900, 3500);
				Assert.Equal(MySqlErrorCode.CommandTimeoutExpired, ex.ErrorCode);

				// connection is unusable
				Assert.Equal(ConnectionState.Closed, connection.State);
			}
		}

		public class ExecuteXAsyncTimeout : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetAsyncMethodSteps))]
			public async Task Test(int step, int method)
			{
				using var connection = new MySqlConnection(m_csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT {10000 + step};";
				var stopwatch = Stopwatch.StartNew();
				var ex = await Assert.ThrowsAsync<MySqlException>(async () => await s_executeAsyncMethods[method](command, default));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 2900, 3500);
				Assert.Equal(MySqlErrorCode.CommandTimeoutExpired, ex.ErrorCode);

				// connection is unusable
				Assert.Equal(ConnectionState.Closed, connection.State);
			}
		}

		public class ExecuteXWithCancellationTimeoutIsNegativeOne : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetSyncMethodSteps))]
			public void Test(int step, int method)
			{
				var csb = new MySqlConnectionStringBuilder(m_csb.ConnectionString) { CancellationTimeout = -1 };
				using var connection = new MySqlConnection(csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT {10000 + step};";
				var stopwatch = Stopwatch.StartNew();
				var ex = Assert.Throws<MySqlException>(() => s_executeMethods[method](command));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 1500);
				Assert.Equal(MySqlErrorCode.CommandTimeoutExpired, ex.ErrorCode);

				// connection is unusable
				Assert.Equal(ConnectionState.Closed, connection.State);
			}
		}

		public class ExecuteXAsyncWithCancellationTimeoutIsNegativeOne : CancellationTests
		{
			[SkipCITheory]
			[MemberData(nameof(GetAsyncMethodSteps))]
			public async Task Test(int step, int method)
			{
				var csb = new MySqlConnectionStringBuilder(m_csb.ConnectionString) { CancellationTimeout = -1 };
				using var connection = new MySqlConnection(csb.ConnectionString);
				connection.Open();
				using var command = connection.CreateCommand();
				command.CommandTimeout = 1;
				command.CommandText = $"SELECT {10000 + step};";
				var stopwatch = Stopwatch.StartNew();
				var ex = await Assert.ThrowsAsync<MySqlException>(async () => await s_executeAsyncMethods[method](command, default));
				Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 1500);
				Assert.Equal(MySqlErrorCode.CommandTimeoutExpired, ex.ErrorCode);

				// connection is unusable
				Assert.Equal(ConnectionState.Closed, connection.State);
			}
		}

		public static IEnumerable<object[]> GetSyncMethodSteps()
		{
			for (var step = 1; step <=  12; step++)
			{
				for (var method = 0; method < s_executeMethods.Length; method++)
				{
					yield return new object[] { step, method };
				}
			}
		}

		public static IEnumerable<object[]> GetAsyncMethodSteps()
		{
			for (var step = 1; step <= 12; step++)
			{
				for (var method = 0; method < s_executeAsyncMethods.Length; method++)
				{
					yield return new object[] { step, method };
				}
			}
		}

		private static readonly Func<MySqlCommand, int>[] s_executeMethods = new Func<MySqlCommand, int>[] { ExecuteScalar, ExecuteNonQuery, ExecuteReader };
		private static readonly Func<MySqlCommand, CancellationToken, Task<int>>[] s_executeAsyncMethods = new Func<MySqlCommand, CancellationToken, Task<int>>[] { ExecuteScalarAsync, ExecuteNonQueryAsync, ExecuteReaderAsync };

		private static int ExecuteScalar(MySqlCommand command) => (int) command.ExecuteScalar();
		private static async Task<int> ExecuteScalarAsync(MySqlCommand command, CancellationToken token) => (int) await command.ExecuteScalarAsync(token);
		private static int ExecuteNonQuery(MySqlCommand command) { command.ExecuteNonQuery(); return 0; }
		private static async Task<int> ExecuteNonQueryAsync(MySqlCommand command, CancellationToken token) { await command.ExecuteNonQueryAsync(token); return 0; }
		private static int ExecuteReader(MySqlCommand command)
		{
			using var reader = command.ExecuteReader();
			int? value = null;
			do
			{
				while (reader.Read())
					value ??= reader.GetInt32(0);
			} while (reader.NextResult());
			return value.Value;
		}
		private static async Task<int> ExecuteReaderAsync(MySqlCommand command, CancellationToken token)
		{
			using var reader = await command.ExecuteReaderAsync(token);
			int? value = null;
			do
			{
				while (await reader.ReadAsync(token))
					value ??= reader.GetInt32(0);
			} while (await reader.NextResultAsync(token));
			return value.Value;
		}

		readonly FakeMySqlServer m_server;
		readonly MySqlConnectionStringBuilder m_csb;
	}
}
