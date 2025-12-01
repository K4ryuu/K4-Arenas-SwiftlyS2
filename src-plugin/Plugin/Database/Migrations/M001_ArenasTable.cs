using FluentMigrator;

namespace K4Arenas.Database.Migrations;

/// <summary>
/// Initial migration for K4 Arenas
/// Creates k4_arenas_players, k4_arenas_weapons, and k4_arenas_rounds tables
/// </summary>
[Migration(202512010250, "Arena_M001")]
public class M001_ArenasTable : Migration
{
	public override void Up()
	{
		// Players table
		if (!Schema.Table("k4_arenas_players").Exists())
		{
			Create.Table("k4_arenas_players")
				.WithColumn("steamid64").AsInt64().NotNullable().PrimaryKey()
				.WithColumn("lastseen").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
		}

		// Weapons preferences table
		if (!Schema.Table("k4_arenas_weapons").Exists())
		{
			Create.Table("k4_arenas_weapons")
				.WithColumn("id").AsInt32().NotNullable().PrimaryKey().Identity()
				.WithColumn("steamid64").AsInt64().NotNullable()
				.WithColumn("weapon_type").AsString(32).NotNullable()
				.WithColumn("weapon_id").AsInt32().NotNullable();

			Create.Index("idx_weapons_steamid").OnTable("k4_arenas_weapons").OnColumn("steamid64");
			Create.Index("idx_weapons_unique").OnTable("k4_arenas_weapons")
				.OnColumn("steamid64").Ascending()
				.OnColumn("weapon_type").Ascending()
				.WithOptions().Unique();
		}

		// Round preferences table
		if (!Schema.Table("k4_arenas_rounds").Exists())
		{
			Create.Table("k4_arenas_rounds")
				.WithColumn("id").AsInt32().NotNullable().PrimaryKey().Identity()
				.WithColumn("steamid64").AsInt64().NotNullable()
				.WithColumn("round_name").AsString(64).NotNullable()
				.WithColumn("enabled").AsBoolean().NotNullable();

			Create.Index("idx_rounds_steamid").OnTable("k4_arenas_rounds").OnColumn("steamid64");
			Create.Index("idx_rounds_unique").OnTable("k4_arenas_rounds")
				.OnColumn("steamid64").Ascending()
				.OnColumn("round_name").Ascending()
				.WithOptions().Unique();
		}
	}

	public override void Down()
	{
		if (Schema.Table("k4_arenas_rounds").Exists())
			Delete.Table("k4_arenas_rounds");

		if (Schema.Table("k4_arenas_weapons").Exists())
			Delete.Table("k4_arenas_weapons");

		if (Schema.Table("k4_arenas_players").Exists())
			Delete.Table("k4_arenas_players");
	}
}
