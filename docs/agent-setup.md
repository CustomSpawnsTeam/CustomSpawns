# Agent setup

CustomSpawns uses [bannerlord-agent](https://github.com/JoeFwd/bannerlord-agent)
through APM, pinned to `v0.2.1`, the same release used by DADG.

From this repository, with APM, git, PowerShell 7, .NET and uv installed:

```powershell
$env:CI = 'true'
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'never'
git submodule update --init --recursive
apm install
```

Run setup non-interactively. These HTTPS remotes must fail on missing credentials
instead of opening credential prompts; APM 0.33 has no `install --yes` option.

APM installs the generic skills, the Bannerlord playtester agent, session hooks
and MCP configuration for Claude Code and Codex. Project values are maintained in
`.apm/skills/bannerlord-agent-settings/SKILL.md`.

Generated `apm_modules/`, `.agents/`, `.claude/`, `.codex/` and `.mcp.json` are
ignored. Commit `apm.yml`, `apm.lock.yaml` and the source under `.apm/` instead.
The `submodules/bannerlord-agent` checkout is for editing the shared skills,
brain and tools; it does not replace the pinned APM installation.

Open Codex in the CustomSpawns project and approve the installed hooks with
`/hooks`; Codex skips untrusted hooks. Restart the client to discover new skills
and MCP servers. Installing the package does not start or restart Bannerlord.

For live debugging, install the Rider Debugger MCP Server and
rider-clr-exception-override plugins, and follow the package's prerequisites.
The project settings skill identifies the CustomSpawns launch profile, log and
campaign evaluation anchor. Keep launch-profile secrets in the existing local
configuration; generated MCP configuration does not need them.
