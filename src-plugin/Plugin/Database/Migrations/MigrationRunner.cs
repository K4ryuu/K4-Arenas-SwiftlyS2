using System.Data;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Conventions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Npgsql;

namespace K4Arenas.Database.Migrations;

/// <summary>
/// FluentMigrator runner for database schema migrations
/// Supports MySQL/MariaDB, PostgreSQL, and SQLite
/// </summary>
public static class MigrationRunner
{
	private const string VersionTableName = "k4_arenas_version_info";

	/// <summary>
	/// Run all pending migrations
	/// </summary>
	/// <param name="dbConnection">Database connection</param>
	public static void RunMigrations(IDbConnection dbConnection)
	{
		var serviceProvider = new ServiceCollection()
			.AddFluentMigratorCore()
			.ConfigureRunner(rb =>
			{
				ConfigureDatabase(rb, dbConnection);
				rb.ScanIn(typeof(MigrationRunner).Assembly).For.Migrations();
			})
			.AddLogging(lb => lb.AddFluentMigratorConsole())
			.AddSingleton<IConventionSet>(new DefaultConventionSet(null, VersionTableName))
			.BuildServiceProvider(false);

		using var scope = serviceProvider.CreateScope();
		var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
		runner.MigrateUp();
	}

	/// <summary>
	/// Configure the FluentMigrator runner for the appropriate database type
	/// </summary>
	private static void ConfigureDatabase(IMigrationRunnerBuilder rb, IDbConnection dbConnection)
	{
		switch (dbConnection)
		{
			case MySqlConnection:
				rb.AddMySql5();
				break;
			case NpgsqlConnection:
				rb.AddPostgres();
				break;
			case SqliteConnection:
				rb.AddSQLite();
				break;
			default:
				throw new NotSupportedException($"Unsupported database connection type: {dbConnection.GetType().Name}");
		}

		rb.WithGlobalConnectionString(dbConnection.ConnectionString);
	}
}
