---
name: bannerlord-agent-settings
description: >
  CustomSpawns values for the generic bannerlord-agent skills: Rider project,
  launch profile, evaluation breakpoint, logs, console commands and module paths.
  Load before Rider, JetBrains debugger, GABS, playtest or log work.
---

# CustomSpawns Bannerlord Agent Settings

## Rider / JetBrains debugging

- `project_path`: `D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/CustomSpawns`.
  Pass it on every JetBrains MCP call. The `&` is literal.
- The GABS-enabled launch profile is `CalradiaAtWar.Debug` in
  `CustomSpawns/Properties/launchSettings.json`. Other profiles do not enable GABP.
  Ask which profile to launch when the user has not already selected one.
- Read the selected profile's `GABP_SERVER_PORT`, `GABS_GAME_ID` and `GABS_BRIDGE_PATH`
  locally. The profile's `GABP_TOKEN` is `$(GabpToken)`, set in the untracked `local.props`
  at the repository root; read the token there. `bridge.json` must match. Do not copy tokens
  into tracked files, instructions or MCP configuration.
- CLR exception breakpoints: `.rider-debugger-settings` beside `CustomSpawns.sln`,
  enforced by the rider-clr-exception-override plugin.
- Campaign evaluation anchor:
  `CustomSpawns/Diplomacy/CustomSpawnsDiplomacyBehaviour.cs` line 48, inside
  `OnTick(float deltaTime)`. It requires an unpaused campaign map. Use one-shot
  evaluation breakpoints; never logging breakpoints. Remove your breakpoint before resuming.

## Tool checkouts and knowledge

- Editing checkout and generic brain: `submodules/bannerlord-agent/`; start at
  `brain/INDEX.md` and search for the symptom before investigating native behavior.
- Installed skills and MCP bridge: `apm_modules/JoeFwd/bannerlord-agent/`.
  Edit the submodule, not generated APM files.
- Tool checkouts: `submodules/bannerlord-agent/tools/BannerlordCraftingTool.CLI`
  and `submodules/bannerlord-agent/tools/BannerlordSceneStudio`. Initialize with
  `git submodule update --init --recursive` from this repository.
- `GABS_EXE`, `ASSERT_AUTO_IGNORE_EXE`, `BANNERLORD_CRAFTING_TOOL` and
  `BANNERLORD_NAVMESH_STUDIO` use the generic package's environment-variable rules.

## Logs and campaign setup

- Mod log: `Logs/<YYYY-MM-DD>.log`, relative to the CustomSpawns module folder.
- Engine mod log:
  `%USERPROFILE%/Documents/Mount and Blade II Bannerlord/Configs/ModLogs/default<YYYYMMDD>.log`.
- Start every new playtest campaign in **Sandbox** mode (main menu > Campaign >
  Sandbox), never Story Mode: story quests and the story kingdoms' scripted
  diplomacy skew wars and invasions. CustomSpawns has no `DADGNewGame` option.
  Load saves through the game menu.
- Discover available console commands before use. CustomSpawns defines
  `campaign.cs_spawn`, `campaign.cs_declare_war`, `campaign.cs_declare_peace` and
  `campaign.cs_clan_join_kingdom_as_mercenary`. Verify the changed object after a command.

## Modules and builds

- This repository is the `CustomSpawns` module and API. `CalradiaAtWar` is a
  separate sibling content module; use the selected profile's module list.
- The live install is `D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord`.
  Check `bin/Win64_Shipping_Client/Version.xml` for the actual installed version;
  `supported-game-versions.txt` is a build target, not runtime evidence.
- Build/test commands are in `AGENTS.md`. When game-directory properties are
  disabled, build output is not deployed automatically. The running module loads
  `bin/Win64_Shipping_Client/CustomSpawns.dll`; verify deployment before a playtest.
