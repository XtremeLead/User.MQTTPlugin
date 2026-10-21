# Voortgang

Voortgang op het [verbeterplan](PLAN.md). Werk dit bestand bij na elke afgeronde stap. Zet de nieuwste logregel bovenaan.

**Status:** ⬜ open · 🔄 bezig · ✅ klaar (gebouwd en getest) · ⏸️ wacht op beslissing

## Fasen

| Fase | Status | Opmerking |
|---|---|---|
| F0. Testproject | ⬜ | |
| F1. Snelle, veilige bugfixes | ⬜ | |
| F2. Verbindingsbeheerder | ⬜ | |
| F3. Acties naar de plugin | ⬜ | |
| F4. Instellingenmodel en opslag | ⬜ | |
| F5. Opruimen en leesbaarheid | ⬜ | |
| F6. Documentatie | ⬜ | |

## Punten

| Code | Omschrijving | Fase | Status |
|---|---|---|---|
| B1 | DNS-fout bij opstarten zet de plugin uit | F1 | ⬜ |
| B2 | TLS-vinkje blokkeert het opslaan | F1 | ⬜ |
| B3 | Test verbreekt de live verbinding, Save past niet toe | F2 | ⬜ |
| B4 | Acties pas actief na openen van de instellingen | F3 | ⬜ |
| B5 | Instellingen weg na SimHub-update | F4 | ⬜ |
| B6 | Race conditions rond de verbinding | F2 | ⬜ |
| B7 | "Please provide MQTT settings" wordt nooit getoond | F1 | ⬜ |
| B8 | `AttachDelegate` bij elk bericht | F1 | ⬜ |
| B9 | Foutpaden rond het wachtwoord | F1 | ⬜ |
| B10 | Ruis in het log, stille publish | F1 | ⬜ |
| A1 | Verbindingsbeheerder in plaats van statische toestand | F2 | ⬜ |
| A2 | Eén getypeerd instellingenmodel | F4 | ⬜ |
| A3 | Acties bij de plugin | F3 | ⬜ |
| A4 | Berichtverwerking los van bijwerkingen | F1/F2 | ⬜ |
| A5 | Oude code en template-resten weg | F5 | ⬜ |
| L1–L6 | Leesbaarheid | F1/F4/F5 | ⬜ |
| T1–T5 | Testbaarheid | F1/F2/F4 | ⬜ |

## Beslissingen

| Code | Vraag | Besluit | Datum |
|---|---|---|---|
| D1 | Overstappen naar SimHub-opslag met migratie? | Ja: getypeerd model in `ReadCommonSettings`, eenmalige migratie uit de huidige `user.config` als de nieuwe opslag leeg is | 2026-10-21 |
| D2 | Timestamp: extra ISO 8601-veld? | Ja: `timestamp` blijft `nl-NL`, extra veld `timestampIso` (`DateTimeOffset.ToString("o")`) | 2026-10-21 |
| D3 | Oude `SettingsControl` verwijderen? | Ja, in F5; `DeserializeDict` gaat in F3 naar de serializer-helper | 2026-10-21 |

## Log

- **2026-10-21:** Skill `code-testing` (dotnet/skills) geïnstalleerd in `.claude/skills/` voor het ontwerpen van nieuwe tests. De kanttekeningen staan in CLAUDE.md onder "Project skills".
- **2026-10-21:** Beslissingen D1, D2 en D3 genomen (zie tabel). B5 bevestigd: op de ontwikkel-pc staan 6 `user.config`-bestanden met MQTT-instellingen in steeds andere bedrijfsmappen (`SimHub`, `뙆`, `삎`, `퉡`, `퍒`, `퍒kak`). SimHub wijzigt per release de CompanyName, de versiemap blijft `1.0.0.0`, en `Upgrade()` lost het daarom niet op. `PluginsData\Common\MQTTPlugin.GeneralSettings.json` heeft updates wel overleefd.
- **2026-10-21:** Codebase geanalyseerd en [PLAN.md](PLAN.md) opgesteld. B1, B2 en het lege-wachtwoordpad van B9 zijn met experimenten bevestigd:
  - B1: de `MqttClient`-constructor gooit `SocketException` bij een onbekende host.
  - B2: een string toekennen aan een bool-instelling gooit `SettingsPropertyWrongTypeException`.
  - B9: `ProtectedData.Unprotect` op een lege waarde gooit `CryptographicException`.
- **2026-10-21:** Opzet: CLAUDE.md geschreven en de skills `csharp-async`, `mqtt-development` en `writing-mstest-tests` geïnstalleerd in `.claude/skills/`. Er wordt gewerkt op de branch `claude`.
