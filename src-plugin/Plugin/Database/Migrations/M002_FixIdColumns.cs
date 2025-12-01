using FluentMigrator;

namespace K4Arenas.Database.Migrations;

/// <summary>
/// Fix migration - properly adds id columns by handling existing primary keys correctly
/// </summary>
[Migration(202512010300, "Arena_M003")]
public class Arena_M003_FixIdColumns : Migration
{
	public override void Up()
	{
		// For weapons table - add id column if missing
		if (Schema.Table("k4_arenas_weapons").Exists() && !Schema.Table("k4_arenas_weapons").Column("id").Exists())
		{
			Execute.Sql("ALTER TABLE k4_arenas_weapons DROP PRIMARY KEY, ADD COLUMN id INT NOT NULL AUTO_INCREMENT PRIMARY KEY FIRST");
		}

		// For rounds table - add id column if missing
		if (Schema.Table("k4_arenas_rounds").Exists() && !Schema.Table("k4_arenas_rounds").Column("id").Exists())
		{
			Execute.Sql("ALTER TABLE k4_arenas_rounds DROP PRIMARY KEY, ADD COLUMN id INT NOT NULL AUTO_INCREMENT PRIMARY KEY FIRST");
		}
	}

	public override void Down()
	{
		// No rollback
	}
}
