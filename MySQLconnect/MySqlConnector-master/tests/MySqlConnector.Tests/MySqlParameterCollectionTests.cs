#if BASELINE
using MySql.Data.MySqlClient;
#endif
using System;
using Xunit;

namespace MySqlConnector.Tests
{
	public class MySqlParameterCollectionTests
	{
		public MySqlParameterCollectionTests()
		{
			m_collection = new MySqlCommand().Parameters;
		}

#if !BASELINE // https://bugs.mysql.com/bug.php?id=100522
		[Fact]
		public void InsertAtNegative() => Assert.Throws<ArgumentOutOfRangeException>(() => m_collection.Insert(-1, new MySqlParameter()));
#endif

		[Fact]
		public void InsertPastEnd() => Assert.Throws<ArgumentOutOfRangeException>(() => m_collection.Insert(1, new MySqlParameter()));

		[Fact]
		public void RemoveAtNegative() => Assert.Throws<ArgumentOutOfRangeException>(() => m_collection.RemoveAt(-1));

		[Fact]
		public void RemoveAtEnd() => Assert.Throws<ArgumentOutOfRangeException>(() => m_collection.RemoveAt(0));

		MySqlParameterCollection m_collection;
	}
}
