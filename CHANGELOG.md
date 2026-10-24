# Changelog

All notable changes to MQTT Plugin are listed here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and version numbers follow [Semantic Versioning](https://semver.org/).

## [3.0.1] - 2026-10-24

### Added

- A `LICENSE` file (MIT).

### Changed

- The plugin has its own icon in SimHub's left menu instead of the SimHub SDK sample icon.

## [3.0.0] - 2026-10-24

A major rework of the plugin. Settings are stored in a new place, so this is a new major version: after upgrading, going back to 2.x means entering your settings again.

### Changed

- Settings and actions are now stored in `C:\Program Files (x86)\SimHub\PluginsData\Common\MQTTPlugin.GeneralSettings.json`, so they survive SimHub updates. SimHub keeps earlier versions in `_Backups`.
- The first start takes over the settings and actions from the old `user.config` once. The old file is left untouched.
- The plugin connects in the background, so an unreachable broker no longer delays SimHub's start.
- If the connection drops, the plugin retries with increasing waits of 2, 5, 10, 30 and then 60 seconds.
- **Save** reconnects right away with the new settings. A new topic replaces the old one instead of being added to it.
- **Test Connection** uses a separate connection and no longer drops the live one.
- A JSON payload is passed on exactly as the sender wrote it: date strings keep their time zone and decimals such as `1.10` keep their trailing zeros. Nested objects, arrays, numbers, `true`/`false` and `null` are included as JSON; anything else as a string.
- Log levels: connecting at Info, failed connections and dropped publishes at Warn, lost data at Error. Received and published messages are logged at Debug only.
- The settings page scrolls as a whole, and long status messages wrap instead of being cut off.

### Added

- The `timestampIso` field in `MQTTPlugin.MQTTMessage`: the receive time in ISO 8601 with time zone offset. The existing `timestamp` field is unchanged.
- Actions are registered when SimHub starts, so button mappings work without opening the settings page first.
- Renamed and removed actions disappear from SimHub after **Save actions**.
- Duplicate action names (ignoring upper and lower case) are refused with a message, and nothing is saved.
- A warning in the log when a message can't be published because there is no connection.
- A triple click in any field selects all of its text.
- Unit tests (MSTest).

### Removed

- The old settings page, which was no longer shown.
- The `MQTTPlugin.CurrentDateTime` property, a leftover from the plugin template.

### Fixed

- The plugin stopped working for the whole session if the broker's host name couldn't be resolved at startup.
- Settings couldn't be saved after the **Use TLS/SSL** box was ticked or unticked.
- Settings were lost after a SimHub update.
- A fresh install showed "Connection failed" instead of asking for MQTT settings.
- An empty password filled the log with decryption errors on every connect.
- A failed password encryption crashed the settings page.
- Race conditions when the watchdog and **Save** connected at the same time, or when a connect finished after SimHub stopped the plugin.

## [2.0.2] - 2026-09-27

### Changed

- The plugin always connects when SimHub starts.

### Fixed

- An invalid TCP port cancels the connect instead of failing.
- A password that can't be decrypted is treated as empty.
- Received messages and the password are decoded as UTF-8.

## [2.0.1] - 2026-09-26

### Changed

- Code cleanup.

## [2.0.0] - 2026-09-26

### Added

- A new settings page with an improved actions editor.
- TLS 1.2 support.
- A connection watchdog that reconnects when the connection drops.

### Changed

- The password field hides what you type.
- The settings page no longer freezes while connecting.

### Fixed

- The same topic was subscribed to more than once.

## [1.0.0] - 2023-12-13

First release.

- Subscribes to one MQTT topic and exposes each message as the SimHub property `MQTTPlugin.MQTTMessage`, as JSON with `topic`, `payload` and `timestamp`.
- Publishes MQTT messages through configurable SimHub actions.
- Stores the password encrypted for the Windows user.

[3.0.1]: https://github.com/XtremeLead/SimHub-MQTT-Plugin/compare/v3.0.0...v3.0.1
[3.0.0]: https://github.com/XtremeLead/SimHub-MQTT-Plugin/compare/v2.0.2...v3.0.0
[2.0.2]: https://github.com/XtremeLead/SimHub-MQTT-Plugin/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/XtremeLead/SimHub-MQTT-Plugin/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/XtremeLead/SimHub-MQTT-Plugin/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/XtremeLead/SimHub-MQTT-Plugin/releases/tag/v1.0.0
