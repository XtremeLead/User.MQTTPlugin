# MQTT Plugin

MQTT Plugin is a SimHub plugin that shows MQTT data on your dashboards and sends MQTT messages to your broker through SimHub actions and button mappings.

For example, a button on your wheel can publish a message that triggers a Home Assistant automation, for instance one that controls your air conditioning. Home Assistant then publishes the result to the topic the plugin subscribes to, and your SimHub dashboard shows it.

## Installation/Building

You need SimHub installed in `C:\Program Files (x86)\SimHub` (the build references its assemblies) and a recent MSBuild (ToolsVersion 15 or later). You can download MSBuild from https://aka.ms/vs/17/release/vs_BuildTools.exe, or at https://visualstudio.microsoft.com/downloads/ under 'Build Tools for Visual Studio'.

Close SimHub first: it locks the plugin DLL. Then run this from PowerShell (adjust the location of MSBuild.exe if needed):

```powershell
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild .\User.MQTTPlugin.csproj
```

This Debug build copies the DLL straight into your SimHub folder. Start SimHub again to load it.

- If NuGet packages are missing, restore them first:
  `& $msbuild .\User.MQTTPlugin.sln -t:restore -p:RestorePackagesConfig=true`
- If the build fails with `MC1000`, run it again with `-t:Rebuild`.

## Usage

### Connecting

Open the plugin's settings page in SimHub and fill in your broker, port, user name, password and the topic to subscribe to.

1. Click **Test Connection**. **Save** only becomes available after a successful test, and any change to a field disables it again until you test once more.
2. Click **Save**. The plugin reconnects right away with the new settings; a new topic replaces the old one.

Tick **Use TLS/SSL** for an encrypted connection; the port switches between 1883 and 8883 for you. The plugin doesn't check the broker's certificate against a certificate authority.

The plugin connects in the background when SimHub starts. If the broker can't be reached, it keeps retrying, waiting 2, 5, 10, 30 and then 60 seconds between attempts.

Tip: a triple click in any field selects all of its text.

### Publishing (actions)

1. Click **+ Add row** and enter a name, a topic to publish to and the message to publish.
2. Click **Save actions**. Each action appears in SimHub as `MQTTPlugin.<name>`.
3. Bind the action to a button with the editor that appears below the row, or under **Controls and events**.

Actions work as soon as SimHub has started; you don't need to open the settings page first.

- Rows with an empty field are skipped.
- Action names must be unique, ignoring upper and lower case. If two rows have the same name, nothing is saved and you get a message.
- Renaming an action creates a new action, so map its button again. SimHub offers to remove the mapping for the old name.
- To remove an action, click ✕ on its row and then **Save actions**.

### Subscribing

You subscribe to one topic, which may use MQTT wildcards (for example `simhub/status/#`). Every message received is exposed as the SimHub property `MQTTPlugin.MQTTMessage`. It holds a JSON object like this:

```json
{
  "topic": "simhub/status/mancave",
  "payload": { "temperature": 21.5, "power": "on" },
  "timestamp": "24-10-2026 11:08:31",
  "timestampIso": "2026-10-24T11:08:31.5120000+02:00"
}
```

- **`topic`**: the topic the message arrived on.
- **`payload`**: the message itself.
  - If the payload is valid JSON (an object, array, number, `true`/`false` or `null`), it is included as JSON, exactly as the sender wrote it, including date strings and decimals such as `1.10`.
  - Anything else, such as `on`, is included as a string.
- **`timestamp`**: when the plugin received the message, in Dutch date format (`d-M-yyyy HH:mm:ss`).
- **`timestampIso`**: the same moment in ISO 8601 with time zone offset, which is easier to parse.

Use JavaScript in your dashboard to read it:

```javascript
const message = JSON.parse($prop("MQTTPlugin.MQTTMessage"));
if (message.topic == "simhub/status/mancave")
  return message.payload.temperature;
```

The property always holds the latest message. With a wildcard topic, check `message.topic` to see which topic it came from.

### Where the settings are stored

The settings and actions are stored in `C:\Program Files (x86)\SimHub\PluginsData\Common\MQTTPlugin.GeneralSettings.json`, so they survive SimHub updates. SimHub keeps earlier versions of this file in the `_Backups` folder next to it. The password is encrypted for your Windows user account; another user or PC can't decrypt it, and then has to enter it again.

Versions before 3.0 stored their settings in a `user.config` file that was lost on SimHub updates. The first time version 3.0 or later starts, it takes over those settings and actions once. The old file is left untouched.

### Logging

The plugin writes to the SimHub log:

- connecting, reconnecting and subscribing at Info;
- failed connections and messages that couldn't be sent at Warn;
- problems that lose data, such as a password or settings that couldn't be read or saved, at Error.

Received and published messages are only logged at Debug level.

## Versions

The changes in each version are listed in [CHANGELOG.md](CHANGELOG.md). Downloads are under Releases on GitHub.

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

The unit tests (MSTest) live in `User.MQTTPlugin.Tests`. Build the solution in Release, so the build doesn't try to write to the SimHub folder, and run the tests with the test runner from the NuGet packages:

```powershell
& $msbuild .\User.MQTTPlugin.sln -p:Configuration=Release
& .\packages\Microsoft.TestPlatform.17.11.1\tools\net462\Common7\IDE\Extensions\TestPlatform\vstest.console.exe .\User.MQTTPlugin.Tests\bin\Release\User.MQTTPlugin.Tests.dll
```

Please add or update tests with your changes.

Version 3.0 was developed with the help of [Claude Code](https://claude.com/claude-code). That is why the repository contains `CLAUDE.md` (instructions for Claude Code), `PLAN.md` and `PROGRESS.md` (the improvement plan and its progress, in Dutch), and the skills in `.claude/skills/`. You don't need any of them to build or use the plugin.

## License

[MIT](https://choosealicense.com/licenses/mit/)
