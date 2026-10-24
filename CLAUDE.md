# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A SimHub plugin (.NET Framework 4.8, WPF, old-style non-SDK `.csproj` with `packages.config`) that subscribes to one MQTT topic and exposes received messages as the SimHub property `MQTTPlugin.MQTTMessage`. It also publishes MQTT messages through user-defined SimHub Actions. MQTT is handled by M2Mqtt 4.3; JSON by Newtonsoft.Json.

**Language level:** the project sets no `LangVersion`, so it compiles as **C# 7.3**. Don't use file-scoped namespaces, nullable reference types, records, `init`, switch expressions, target-typed `new()`, collection expressions or other newer features. SimHub only loads .NET Framework plugins, so upgrading to modern .NET is not an option.

## Git

- **Never commit or push.** The user makes all commits themselves. Leave changes in the working tree and say what changed.
- Work on the `claude` branch (branched from `develop`; `master` is the main branch). Don't switch branches unless the user asks.

## Plan and progress

- [PLAN.md](PLAN.md) is the agreed improvement plan. Bugs, architecture, readability, testability and wish items have fixed codes (B1, A1, L1, T1, W1, ...), work is grouped into phases (F0, F0b, F1–F6, all done by 2026-10-24; F7 for B13 is open), and decisions are listed (D1–D7, all decided). The wishes W1–W5 (new features) are not in a phase yet. Refer to items by code; give new bugs, wishes or decisions the next free code (B14, W6, D8, ...). Don't change the plan's content without the user's agreement.
- [PROGRESS.md](PROGRESS.md) tracks progress. Update it after every completed step: the phase and item status, any decisions with their date, and a new log line at the top. Mark an item ✅ only after it is built and its tests pass.
- Both files are written in Dutch, so keep them in Dutch.

## Build and test

```powershell
$msb = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$vstest = '.\packages\Microsoft.TestPlatform.17.11.1\tools\net462\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
& $msb .\User.MQTTPlugin.csproj                                          # Debug: deploys into the SimHub folder
& $msb .\User.MQTTPlugin.sln -t:restore -p:RestorePackagesConfig=true   # only when packages\ is missing
& $msb .\User.MQTTPlugin.sln -p:Configuration=Release                   # use this before running tests
& $vstest .\User.MQTTPlugin.Tests\bin\Release\User.MQTTPlugin.Tests.dll                    # all tests
& $vstest .\User.MQTTPlugin.Tests\bin\Release\User.MQTTPlugin.Tests.dll /TestCaseFilter:"FullyQualifiedName~CryptTests.ProtectString_DoesNotReturnPlainText"   # one test
```

- **Debug** builds write straight into `C:\Program Files (x86)\SimHub\`, which is how the plugin gets deployed. They need write access there and fail while SimHub runs, because SimHub locks the DLL. A Debug build of the solution does this too, so build the solution in **Release** for tests (output in `bin\Release\`).
- If the WPF markup compiler fails with `MC1000` ("Unknown build error, 'Object reference not set…'"), usually after a XAML file was removed, build again with `-t:Rebuild`.
- SimHub assemblies (`SimHub.Plugins`, `GameReaderCommon`, `SimHub.Logging`, `log4net`) are referenced from the SimHub install folder, so SimHub must be installed to build.
- There is no linter. Behaviour that unit tests can't cover (`Init`, `End`, `AddAction`, the WPF controls, a real broker) is verified by building, restarting SimHub and checking the SimHub log. Log levels: lifecycle at Info, failed connects and dropped publishes at Warn, failures that lose data at Error, every received or published message at Debug only.

## Unit tests

Unit tests are required, with **MSTest 3.1.1** in [User.MQTTPlugin.Tests/](User.MQTTPlugin.Tests/).

- Every change comes with unit tests for the logic it adds or modifies. All testable existing logic is covered; logic you move or refactor keeps its tests.
- Put new testable logic in pure helpers (like `MessageFormatter`, `ActionRules`, `SettingsViewLogic`), not in the WPF code-behind.
- Tests never connect to a real broker, write SimHub's settings file or write `Properties.Settings`.
- The details (fakes, harnesses, adding files and packages, test skills) are in [.claude/rules/testing.md](.claude/rules/testing.md), which loads when working in the test project.

## Architecture

Only what the code doesn't show by itself. The message format for dashboards is in the [README](README.md).

- **Plugin lifecycle:** SimHub rebuilds the plugin on every game change, so `Init`/`End` run many times per session, and `End` of the old instance runs before `Init` of the new one. `MQTTPlugin.Instance` points to the live instance. `DataUpdate` is intentionally empty: data arrives from MQTT, not from the game loop.
- **Connection:** one shared `MqttConnectionManager.Shared` (D4). `Init` calls `Start(this)`, which connects in the background so `Init` doesn't block (D5); `End` calls `Stop(this)`. Start and Stop take the plugin as owner, so the `End` of an old instance can't stop the connection of a newer one.
  - One connect at a time (`connectGate`); the blocking M2Mqtt `Connect` runs outside the `sync` lock, and a connect that finishes after `Stop` is discarded.
  - Save (`ReconnectAsync`) always drops and reconnects with the saved settings, so a new topic replaces the old one. Test (`TestAsync`) uses a separate client and never touches the live one.
  - The watchdog reconnects with the backoff from `BackoffSchedule` (2, 5, 10, 30, 60 s).
  - `MessageFormatter` keeps the payload text unchanged (no date or decimal conversion). `timestamp` stays `nl-NL` for existing dashboards; `timestampIso` was added next to it (D2).
- **Settings:** one typed model, `MQTTPluginSettings`, stored by SimHub (`ReadCommonSettings`) in `PluginsData\Common\MQTTPlugin.GeneralSettings.json` in the SimHub folder, because `user.config` was lost on every SimHub update (B5).
  - `MQTTSettings` saves a copy and only then replaces `Current`, so other threads always see a complete set and a failed save changes nothing. `End` doesn't save, and the settings page writes nothing while typing.
  - **Migration (D1):** at `SettingsVersion` 0, `Initialize` reads the old `user.config` once through `Properties.Settings` and saves version 1. `user.config` is never written; `Properties/Settings.settings` stays only for this.
- **Password:** DPAPI (`CurrentUser`) in [Crypt.cs](Crypt.cs), base64 in `EncryptedPassword`. `UnprotectString` never throws; failures give `""` and an Error log.
- **Actions:** each one is the SimHub action `MQTTPlugin.<name>`. The plugin owns registration (A3): `RegisterActions` clears all of the plugin's actions (`PluginManager.ClearActions`) and adds the current set, so renamed and removed actions disappear. "Save actions" skips incomplete rows and refuses the whole save on duplicate names, ignoring case (D6).
- **Settings UI:** `SettingsControlV2`; its conventions are in [.claude/rules/settings-ui.md](.claude/rules/settings-ui.md).

## Known pitfalls

- Renaming an action creates a new action name. A control mapping made under Controls and events for the old name stays behind and has to be mapped again.
- `M2MqttClientFactory` passes no certificate validation callback, and M2Mqtt then accepts any broker certificate, even one Windows rejects (confirmed against `test.mosquitto.org:8883`). Fixing this is B13 (F7, D7).
- The UI event handlers update WPF controls after `await`. Don't add `ConfigureAwait(false)` there.

## Project skills

[.claude/skills/](.claude/skills/) holds third-party skills from skills.sh; [skills-lock.json](skills-lock.json) records their source and hash. Update them with `npx skills update -p`. If the CLI installs a skill into `.agents/skills/` with only a junction in `.claude/skills/`, replace the junction with a real copy and delete `.agents/`.
- `code-testing` and `writing-mstest-tests`: for test work; their caveats are in [.claude/rules/testing.md](.claude/rules/testing.md).
- `csharp-async`: its `ValueTask`/`IAsyncEnumerable` suggestions need extra NuGet packages on .NET Framework 4.8, and its `ConfigureAwait(false)` advice doesn't apply to the UI handlers.
- `mqtt-development`: general MQTT guidance, not specific to M2Mqtt; check its suggestions against the M2Mqtt 4.3 API.
