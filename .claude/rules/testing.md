---
paths:
  - "User.MQTTPlugin.Tests/**"
---

# Unit test details

## Test project

- Old-style .NET Framework 4.8 project with `packages.config`, like the main project. It runs on `vstest.console.exe` from the `Microsoft.TestPlatform` package; the commands are in CLAUDE.md.
- It isn't SDK-style because the BuildTools MSBuild can't resolve `Microsoft.NET.Sdk` here (the only .NET SDK installed is 5.0).
- Stay on MSTest 3.1.x: `packages.config` doesn't restore transitive dependencies, and 3.2 and later pull in `Microsoft.Testing.Platform` packages that would all have to be listed by hand.
- The main assembly has `InternalsVisibleTo("User.MQTTPlugin.Tests")`, so tests can reach `internal` types.
- **Adding test files:** the project doesn't glob, so add each new `.cs` file as `<Compile Include>` in the test `.csproj`. Add a new package to `packages.config` together with its `HintPath` reference, and list every dependency it has.

## Seams and fakes

- **Connection tests:** never start `MqttConnectionManager.Shared`. Build a manager with `ManagerHarness` from [ConnectionFakes.cs](../../User.MQTTPlugin.Tests/ConnectionFakes.cs). It wires up `FakeMqttClientFactory`/`FakeMqttClient` (connect, drop, receive, block a connect through `ConnectHook`), `FakePluginLog`, `FakeClock`, `FakeTimer`, an instant backoff delay and editable saved settings. Wait on the returned tasks with a timeout.
- **Settings:** `MQTTSettings.Reconnect` and `MQTTSettings.Log` are static fields that tests replace, and `MQTTSettings.Initialize` takes the store (`IPluginSettingsStore`) and the legacy loader. `TestState.Reset()` ([TestState.cs](../../User.MQTTPlugin.Tests/TestState.cs)) initializes `MQTTSettings` from a `FakePluginSettingsStore` with migrated defaults, a `FakePluginLog` and a no-op reconnect. Call it in `[TestInitialize]` and `[TestCleanup]`.
- Only `LegacySettingsTests` reads the real `Properties.Settings` (the test host has no `user.config`); nothing writes it.
- **Actions:** `IActionRegistry` is the seam; `SimHubActionRegistry` is the real one.
- **Log assertions:** code with an `IPluginLog` is checked with `FakePluginLog`. For code that logs through `SimHub.Logging.Current` directly (`Crypt`), [LogCapture.cs](../../User.MQTTPlugin.Tests/LogCapture.cs) attaches a log4net `MemoryAppender`; use it in a `using` block.
- **Failing host:** use `broker.invalid`, which always fails DNS and never connects.
- **Pure helpers** with their own tests: `MessageFormatter`, `SettingsMigration`, `ActionsSerializer`, `ActionRules`, `SettingsRules`, `SettingsViewLogic`, `ConnectionSettings.FromSettings`, `MultiClickCounter` and `MqttConnectionManager.GetBackoffDelaySeconds`.
- **Characterization tests:** to pin a known bug before fixing it, start the test's comment with `Current behaviour (Bx)`; the fix changes that test on purpose. None are left.

## Skills for test work

- `code-testing` is the entry skill for designing new tests; use `writing-mstest-tests` alongside it for the MSTest APIs.
  - Our per-phase test work counts as its **focused** scope, so do it inline. Keep any broad-scope `research.md`/`plan.md` in the scratchpad, never in the repo.
  - It refers to agents (`test-engineer`, `code-testing-*`) and skills (`code-testing-extensions`, `test-gap-analysis`, `assertion-quality`, `run-tests`) that aren't installed. Skip those references; don't spawn substitutes.
  - No mocking library is installed. Use hand-written fakes behind the existing interfaces unless a library is agreed on.
  - Its rule "fix the assertion, not the production code" applies to tests of existing behaviour. In bug-fix phases the tests pin the fixed behaviour from PLAN.md; if a test shows the plan is wrong, report it instead of bending the test.
- `writing-mstest-tests`: use only APIs that exist in MSTest 3.1.1 (for example `Assert.ThrowsException`, not `Assert.ThrowsExactly`). Verify with the build and vstest commands from CLAUDE.md, not `dotnet test`.
- Both skills' examples use newer C# (target-typed `new()`, collection expressions); write tests in C# 7.3.
