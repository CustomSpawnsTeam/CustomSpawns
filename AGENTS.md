# AGENTS.md

CustomSpawns is a Mount & Blade II: Bannerlord module (C# 9, netstandard2.0) that spawns,
drives and reinforces custom parties, including the invasions.

## Rules

Read these before writing code; they apply to all new and edited code.

- [docs/coding-rules.md](docs/coding-rules.md): design. Hexagonal ports and adapters, interfaces
  as methods, no public static methods, no `internal`, game extension points over Harmony, unit
  tests.
- [docs/code-style.md](docs/code-style.md): layout. Braces, paragraphs, comments, field names.

## Bannerlord agent

See [docs/agent-setup.md](docs/agent-setup.md) for the pinned APM installation.
Before Rider, GABS, playtest or log work, load the `bannerlord-agent-settings`
skill for this project. Its source is
[.apm/skills/bannerlord-agent-settings/SKILL.md](.apm/skills/bannerlord-agent-settings/SKILL.md).
Before investigating native behavior, search the generic brain starting at
`submodules/bannerlord-agent/brain/INDEX.md`.

## Build and test

```sh
dotnet build CustomSpawns/CustomSpawns.csproj -p:BuildForWindows=false -p:BuildForWindowsStore=false
dotnet test CustomSpawns.Tests/CustomSpawns.Tests.csproj -p:BuildForWindows=false -p:BuildForWindowsStore=false
```
