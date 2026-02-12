<a name="readme-top"></a>

![GitHub tag (with filter)](https://img.shields.io/github/v/tag/K4ryuu/K4-Arenas-SwiftlyS2?style=for-the-badge&label=Version)
![GitHub Repo stars](https://img.shields.io/github/stars/K4ryuu/K4-Arenas-SwiftlyS2?style=for-the-badge)
![GitHub issues](https://img.shields.io/github/issues/K4ryuu/K4-Arenas-SwiftlyS2?style=for-the-badge)
![GitHub](https://img.shields.io/github/license/K4ryuu/K4-Arenas-SwiftlyS2?style=for-the-badge)
![GitHub all releases](https://img.shields.io/github/downloads/K4ryuu/K4-Arenas-SwiftlyS2/total?style=for-the-badge)
[![Discord](https://img.shields.io/badge/Discord-Join%20Server-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://dsc.gg/k4-fanbase)

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <h1 align="center">KitsuneLab©</h1>
  <h3 align="center">K4 - Arenas</h3>
  <a align="center">An all-in-one arena plugin for Counter-Strike 2 with ladder-style gameplay. Supports any map, 2v2/3v3 modes, weapon preferences and a developer API for custom round types.</a>

  <p align="center">
    <br />
    <a href="https://github.com/K4ryuu/K4-Arenas-SwiftlyS2/releases/latest">Download</a>
    ·
    <a href="https://github.com/K4ryuu/K4-Arenas-SwiftlyS2/issues/new?assignees=K4ryuu&labels=bug&projects=&template=bug_report.md&title=%5BBUG%5D">Report Bug</a>
    ·
    <a href="https://github.com/K4ryuu/K4-Arenas-SwiftlyS2/issues/new?assignees=K4ryuu&labels=enhancement&projects=&template=feature_request.md&title=%5BREQ%5D">Request Feature</a>
  </p>
</div>

### Support My Work

I create free, open-source Counter-Strike 2 plugins for the community. If you'd like to support my work, consider becoming a sponsor!

#### 💖 GitHub Sponsors

Support this project through [GitHub Sponsors](https://github.com/sponsors/K4ryuu) with flexible options:

- **One-time** or **monthly** contributions
- **Custom amount** - choose what works for you
- **Multiple tiers available** - from basic benefits to priority support or private project access

Every contribution helps me dedicate more time to development, support, and creating new features. Thank you! 🙏

<p align="center">
  <a href="https://github.com/sponsors/K4ryuu">
    <img src="https://img.shields.io/badge/sponsor-30363D?style=for-the-badge&logo=GitHub-Sponsors&logoColor=#EA4AAA" alt="GitHub Sponsors" />
  </a>
</p>

⭐ **Or support me for free by starring this repository!**
#### 💖 GitHub Sponsors

Support this project through [GitHub Sponsors](https://github.com/sponsors/K4ryuu) with flexible options:

- **One-time** or **monthly** contributions
- **Custom amount** - choose what works for you
- **Multiple tiers available** - from basic benefits to priority support or private project access

Every contribution helps me dedicate more time to development, support, and creating new features. Thank you! 🙏

<p align="center">
  <a href="https://github.com/sponsors/K4ryuu">
    <img src="https://img.shields.io/badge/sponsor-30363D?style=for-the-badge&logo=GitHub-Sponsors&logoColor=#EA4AAA" alt="GitHub Sponsors" />
  </a>
</p>

⭐ **Or support me for free by starring this repository!**

### Dependencies

- [**SwiftlyS2**](https://github.com/swiftly-solution/swiftlys2): Server plugin framework for Counter-Strike 2
- **Database**: One of the following supported databases:
  - **MySQL / MariaDB** - Recommended for production
  - **PostgreSQL** - Full support
  - **SQLite** - Great for single-server setups

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- INSTALLATION -->

## Installation

1. Install [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) on your server
2. Configure your database connection in SwiftlyS2's `database.jsonc` (MySQL, PostgreSQL, or SQLite)
3. [Download the latest release](https://github.com/K4ryuu/K4-Arenas-SwiftlyS2/releases/latest)
4. Extract to your server's `swiftlys2/plugins/` directory
5. Configure `config.json` in the plugin folder
6. Restart your server - database tables will be created automatically

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- CONFIGURATION -->

## Configuration

| Option               | Description                                      | Default    |
| -------------------- | ------------------------------------------------ | ---------- |
| `DatabaseConnection` | Database connection name for storing preferences | `""`       |
| `MinPlayersToStart`  | Minimum players required to start arenas         | `2`        |
| `RoundTimeSeconds`   | Round duration in seconds                        | `60`       |
| `ArenaAfkCommand`    | Command to toggle AFK status                     | `"afk"`    |
| `ArenaGunsCommand`   | Command to open weapon preferences               | `"guns"`   |
| `ArenaRoundsCommand` | Command to open round preferences                | `"rounds"` |

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- COMMANDS -->

## Commands

| Command   | Description                      |
| --------- | -------------------------------- |
| `!guns`   | Open weapon preferences menu     |
| `!rounds` | Open round type preferences menu |
| `!afk`    | Toggle AFK status                |

<p align="right">(<a href="#readme-top">back to top</a>)</p>

---

## Database

The plugin uses automatic schema management with FluentMigrator. Tables are created automatically on first run.

### Supported Databases

| Database        | Status  | Notes                                      |
| --------------- | ------- | ------------------------------------------ |
| MySQL / MariaDB | ✅ Full | Recommended for multi-server setups        |
| PostgreSQL      | ✅ Full | Alternative for existing Postgres setups   |
| SQLite          | ✅ Full | Perfect for single-server, no setup needed |

### Database Tables

- `k4_arenas_players` - Player records and last seen timestamps
- `k4_arenas_weapons` - Player weapon preferences per weapon type
- `k4_arenas_rounds` - Player round type preferences

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- LICENSE -->

## License

Distributed under the GPL-3.0 License. See [`LICENSE.md`](LICENSE.md) for more information.

<p align="right">(<a href="#readme-top">back to top</a>)</p>
