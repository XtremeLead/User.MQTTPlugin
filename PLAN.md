# Verbeterplan User.MQTTPlugin

Opgesteld op 2026-10-21 na een analyse van de codebase op commit `0693bec`. De voortgang staat in [PROGRESS.md](PROGRESS.md).

Elk punt heeft een vaste code (B = bug, A = architectuur, L = leesbaarheid, T = testbaarheid). Gebruik die codes in commits, in `PROGRESS.md` en in gesprekken. Regelnummers horen bij commit `0693bec` en kunnen verschuiven. De methodenamen blijven leidend.

**Zekerheid:**
- **bevestigd:** aangetoond met een experiment
- **uit code:** volgt duidelijk uit de code
- **te verifiëren:** aannemelijk, maar hangt af van gedrag van SimHub of de omgeving

## Bugs (op volgorde van ernst)

### B1. Plugin werkt de hele sessie niet als de DNS-lookup van de broker mislukt bij het opstarten
*Bevestigd.*
- **Oorzaak:** de `MqttClient`-constructor lost de hostnaam direct op en gooit dan een `SocketException`. In `MQTTClient.ConnectMQTT` ([MQTTClient.cs:40](MQTTClient.cs#L40)) staat die constructor buiten de `try`. In `MQTTPlugin.Init` ([MQTTPlugin.cs:126-135](MQTTPlugin.cs#L126-L135)) breekt de exceptie de methode af vóór `StartWatchdog()`, het laden van de instellingen en `AttachDelegate("MQTTMessage")`.
- **Gevolg:** als de pc offline is, de hostnaam een typfout bevat of de DNS hapert, is er geen MQTT. Er wordt ook nooit opnieuw verbonden.
- **Ook geraakt:** bij Save in de UI komt dezelfde exceptie terecht in een `async void`-handler.

### B2. Na aanraken van het TLS-vinkje kunnen instellingen niet meer worden opgeslagen
*Bevestigd.*
- **Oorzaak:** `mqtttls` is een `bool`-instelling. `MQTTSettings.SaveSettingsAsync` ([MQTTSettings.cs:36](MQTTSettings.cs#L36)) schrijft er de string `"True"`/`"False"` in, wat een `SettingsPropertyWrongTypeException` geeft.
- **Gevolg:** `Save()` wordt nooit bereikt, dus er wordt niets opgeslagen. De foute waarde blijft in de statische `SETTINGS` staan, waardoor elke volgende poging tot de herstart van SimHub ook faalt. TLS kan in feite nooit via de UI opgeslagen worden.

### B3. "Test Connection" verbreekt de live verbinding en "Save" past nieuwe instellingen vaak niet toe
*Uit code.*
- **Stap 1:** `TestConnectionAsync` ([MQTTClient.cs:84](MQTTClient.cs#L84)) verbreekt de live `CLIENT`.
- **Stap 2:** de watchdog verbindt binnen 2–7 seconden opnieuw, maar met de *oude* opgeslagen instellingen. Nieuwe waarden staan nog alleen in `MQTTSettings.SETTINGS`.
- **Stap 3:** bij Save is `CLIENT` dan verbonden, dus `ConnectAndSubscribe` ([MQTTClient.cs:216-223](MQTTClient.cs#L216-L223)) verbindt niet opnieuw.
- **Gevolg:** een nieuwe broker, gebruiker, wachtwoord of TLS-instelling wordt pas na een herstart actief. Een nieuw topic wordt *bovenop* het oude geabonneerd, en het oude wordt nooit opgezegd.
- **Ook:** Test overschrijft de statische `IsConnected` en `lastConnectionSuccessful` met de status van de testclient.

### B4. Acties werken na een herstart pas als de instellingenpagina geopend is
*Uit code.*
- **Oorzaak:** acties worden alleen in `SettingsControlV2` geregistreerd (`LoadExistingActions` en `btnSaveActions_Click`), niet in `MQTTPlugin.Init`.
- **Gevolg:** gekoppelde knoppen naar `MQTTPlugin.<naam>` doen niets tot iemand die pagina opent. Een hernoemde actie blijft daarnaast onder de oude naam geregistreerd, en dubbele namen worden stil overschreven.
- **Te verifiëren:** wat SimHub doet met acties van de oude plugininstantie na een rebuild (gamewissel). `SettingsControlV2.Plugin` kan dan naar een oude instantie wijzen.

### B5. Instellingen gaan verloren bij een SimHub-update
*Bevestigd.*
- **Oorzaak:** `Properties.Settings` slaat op in `%LOCALAPPDATA%\<bedrijfsnaam>\SimHubWPF.exe_Url_…\1.0.0.0\user.config`. De versiemap blijft `1.0.0.0`, maar de bedrijfsnaam (CompanyName van het SimHub-exe) verandert per release. Op de ontwikkel-pc staan daardoor 6 `user.config`-bestanden met MQTT-instellingen, in de mappen `SimHub`, `뙆`, `삎`, `퉡`, `퍒` en `퍒kak`.
- **Gevolg:** na een SimHub-update zijn broker, wachtwoord en acties leeg.
- **Geen oplossing:** `Settings.Upgrade()` zoekt alleen naar oudere versies binnen dezelfde bedrijfsmap en helpt hier dus niet. De opslag van `ReadCommonSettings` (`PluginsData\Common\MQTTPlugin.GeneralSettings.json` in de SimHub-installatiemap) heeft wel meerdere updates overleefd.

### B6. Race conditions rond de verbinding
*Uit code.*
- `reconnecting` is een gewone `bool` die door de timer- en taakthreads gedeeld wordt ([MQTTClient.cs:26](MQTTClient.cs#L26)).
- `ConnectAndSubscribe` leest `CLIENT` en abonneert buiten `ClientLock`. De UI (Save) en de watchdog kunnen daardoor tegelijk verbinden.
- Een reconnect-taak die al loopt, kan ná `End()` nog een verbinding opbouwen.

### B7. Een verse installatie toont "Connection failed" in plaats van "Please provide MQTT settings"
*Uit code.*
- **Oorzaak:** `mqtttls` is standaard `"False"` en telt daardoor als ingevulde instelling. De check "geen instellingen" slaagt dus nooit ([MQTTClient.cs:205-213](MQTTClient.cs#L205-L213), [SettingsControlV2.xaml.cs:50-71](SettingsControlV2.xaml.cs#L50-L71)).
- **Gevolg:** `Init` probeert ook zonder instellingen te verbinden. Dat stopt alleen bij de poortcontrole.

### B8. `AttachDelegate` wordt bij elk bericht opnieuw aangeroepen
*Uit code.*
- **Oorzaak:** de setter van `MQTTPlugin.MqttMessage` ([MQTTPlugin.cs:45](MQTTPlugin.cs#L45)) roept `AttachDelegate` aan vanaf de M2Mqtt-ontvangstthread.
- **Gevolg:** dat is onnodig, omdat `Init` de property al koppelt. Het is ook onzeker of dit thread-safe is ten opzichte van SimHub.

### B9. Foutpaden rond het wachtwoord
*Bevestigd voor het lege wachtwoord.*
- **Leeg wachtwoord:** dat is de standaardwaarde. Het geeft bij elke (re)connect een `CryptographicException` in `Crypt.UnprotectString` en een melding in het log.
- **Mislukte versleuteling:** dan geeft `Crypt.Protect` `null` terug, en daarna gooit `Convert.ToBase64String(null)` een exceptie in de PasswordChanged-handler ([Crypt.cs:15-19](Crypt.cs#L15-L19)).

### B10. Ruis in het log en stille fouten
*Uit code.*
- Elk ontvangen bericht en elke publish wordt op Info-niveau gelogd, inclusief de volledige payload.
- Een gewone tekstpayload zoals `on` logt bij elk bericht "not valid JSON".
- `Publish` gooit een bericht zonder melding weg als er geen verbinding is ([MQTTClient.cs:170-180](MQTTClient.cs#L170-L180)).

## Architectuur

### A1. Globale statische toestand in `MQTTClient`
- **Huidige situatie:**
  - `CLIENT` is een publiek statisch veld.
  - Het statische `IsConnected` wordt alleen geschreven en is vaak fout. `lastConnectionSuccessful` wordt ook alleen geschreven.
  - De vlaggen staan verspreid over de klasse.
- **Voorstel:** één verbindingsbeheerder met een duidelijke API: `Connect`/`Reconnect(settings)`, `Publish`, `Test`, en events voor ontvangen berichten en verbindingsstatus.
  - De watchdog zit in de beheerder, en één lock beschermt de client.
  - `Test` gebruikt altijd een aparte client en raakt de live verbinding nooit aan.
- **Lost op:** B3, B6.

### A2. Twee instellingenopslagen met tekstsleutels zonder type
- **Huidige situatie:**
  - `Properties.Settings` wordt benaderd via `Dictionary<string,string>` in `MQTTSettings`.
  - `MQTTPluginSettings` (via `ReadCommonSettings`) is een dode template-rest.
  - De acties staan als XML-string in `mqttactions`.
- **Voorstel:** één getypeerd model (verbinding en acties) met één opslag, namelijk de eigen `ReadCommonSettings` van SimHub. Daarbij hoort een eenmalige migratie van de bestaande `user.config`. Zie beslissing D1.
- **Lost op:** B2 en B5.

### A3. Acties horen bij de plugin, niet bij de UI
- `Init` registreert de acties uit de instellingen.
- De UI past alleen het model aan en vraagt de plugin om opnieuw te registreren.
- **Lost op:** B4.

### A4. De berichtverwerking zit vermengd met bijwerkingen
- **Huidige situatie:** parsen en verpakken (`{topic, payload, timestamp}`) zitten in `client_MqttMsgPublishReceived`, samen met logging en `MQTTPlugin.Instance`.
- **Voorstel:**
  - De verwerking gaat naar een pure formatter.
  - De plugin ontvangt berichten via een event in plaats van via de statische `Instance`.
  - Naast `timestamp` komt een veld `timestampIso` (beslissing D2).

### A5. Oude code en template-resten opruimen
- Het oude `SettingsControl` en de schakelaar in `GetWPFSettingsControl` gaan weg. `DeserializeDict` verhuist naar een serializer-helper. Zie beslissing D3.
- Daarnaast verdwijnen `MQTTPluginSettings.SpeedWarningLevel`, de property `CurrentDateTime` en de lege `if`-blokken in `DataUpdate`.

## Leesbaarheid

- **L1.** Handlers krijgen betekenisvolle namen: `TextBox_TextChanged_1`, `SHButtonPrimary_Click_1` en dergelijke worden bijvoorbeeld `txtPort_TextChanged` en `btnTest_Click`.
- **L2.** De magische strings voor instellingssleutels (`"mqttserver"` enzovoort) gaan naar constanten of het getypeerde model.
- **L3.** Uitgecommentarieerde code verwijderen ([MQTTPlugin.cs:43](MQTTPlugin.cs#L43), [MQTTClient.cs:149-159](MQTTClient.cs#L149-L159)).
- **L4.** Dubbele logica samenvoegen: de "settings found"-check, de statusteksten, het aanmaken van de `ControlsEditor` en de breedte `(3 * 95) + (2 * 10)`.
- **L5.** Consequente naamgeving: statische velden heten nu `CLIENT`/`SETTINGS`, lokale variabelen `LogMessage`/`MqttSettingsFound`, en velden `ActionsToStore`.
- **L6.** Logniveaus differentiëren in Debug, Info, Warn en Error. Nu staat alles op `Info`.

## Testbaarheid

- **T1.** Pure helpers afsplitsen:
  - het bericht-JSON opbouwen uit topic, bytes en tijdstip
  - de instellingen controleren, zoals de poort
  - de acties (de)serialiseren
  - de vertraging berekenen uit `BackoffSchedule`
  - De `Crypt`-round-trip is nu al testbaar.
- **T2.** Een dunne adapter om M2Mqtt heen, zodat reconnect-gedrag, "Test raakt de live verbinding niet" en "opnieuw abonneren bij een topicwissel" met een fake te testen zijn.
- **T3.** Een interface voor de instellingenopslag, zodat tests niet naar de echte `Properties.Settings` schrijven.
- **T4.** Een kleine logger-interface in plaats van direct `SimHub.Logging.Current`. Het is nog onbekend of die in een testproces werkt.
- **T5.** Een klok en een timer als injecteerbare afhankelijkheden voor de timestamp en de watchdog.

## Fasen

Elke fase levert een werkende, bouwbare plugin op met MSTest-tests, zoals [CLAUDE.md](CLAUDE.md) voorschrijft. Na elke fase commit de gebruiker zelf.

| Fase | Inhoud | Lost op |
|---|---|---|
| **F0. Testproject** | `User.MQTTPlugin.Tests` (net48, MSTest) aanmaken en opnemen in `User.MQTTPlugin.sln`. De build- en testcommando's, inclusief één losse test draaien, vastleggen in CLAUDE.md. | Voorwaarde voor alles |
| **F1. Snelle, veilige bugfixes** | Exceptie van de client-constructor afvangen, en de watchdog vóór de connect starten. Bool-instellingen correct opslaan en de staging-lijst wissen na een fout. Het pad voor een leeg wachtwoord en het `null`-resultaat van `Protect` afvangen. De "geen instellingen"-check repareren. `AttachDelegate` uit de setter halen. Logniveaus aanpassen en een melding toevoegen bij een publish zonder verbinding. De berichtformatter afsplitsen, met tests. | B1, B2, B7, B8, B9, B10, deels A4, T1, L6 |
| **F2. Verbindingsbeheerder** | De statische `MQTTClient` vervangen door een beheerder met adapter en één lock. Test gebruikt een aparte client. Save forceert een reconnect en zegt het oude topic op. Dode velden opruimen. Tests met een fake client. | B3, B6, A1, T2, T4, T5 |
| **F3. Acties naar de plugin** | Registreren in `Init` vanuit de instellingen. De UI roept alleen de plugin aan. Hernoemen, verwijderen en dubbele namen correct afhandelen. Serialisatie-helper met tests. | B4, A3 |
| **F4. Instellingenmodel en opslag** | Getypeerd model in `ReadCommonSettings` en eenmalige migratie van de huidige `user.config` (zie D1). | B5, A2, T3, L2 |
| **F5. Opruimen en leesbaarheid** | Oude `SettingsControl` en template-resten weg (zie D3). Handlers hernoemen en dubbele logica samenvoegen. | A5, L1, L3, L4, L5 |
| **F6. Documentatie** | README en de architectuursectie van CLAUDE.md bijwerken naar de nieuwe opzet. | — |

## Beslissingen

- **D1. Opslag:** overstappen naar de eigen opslag van SimHub (`ReadCommonSettings`), met migratie van de bestaande instellingen?
  - **Besluit (2026-10-21):** ja. Er komt een getypeerd model in de opslag van `ReadCommonSettings`. De eenmalige migratie leest de `user.config` van de huidige SimHub-versie (via `Properties.Settings`), en alleen als de nieuwe opslag nog leeg is. Oude bedrijfsmappen worden niet doorzocht.
- **D2. Timestamp:** nu is dat een `nl-NL`-string. ISO 8601 is robuuster, maar breekt dashboards die de huidige tekst parsen.
  - **Besluit (2026-10-21):** `timestamp` blijft exact zoals nu (`nl-NL`). Er komt een extra veld `timestampIso` bij in ISO 8601 met offset (`DateTimeOffset.ToString("o")`, bijvoorbeeld `2026-10-21T14:03:12.3450000+02:00`).
- **D3. Oude `SettingsControl` verwijderen?**
  - **Besluit (2026-10-21):** ja, in F5. Het scherm wordt nooit getoond, omdat `GetWPFSettingsControl` via een constante altijd V2 kiest. Bij laten staan zou het in F2–F4 telkens mee aangepast moeten worden. `DeserializeDict` verhuist in F3 naar de serializer-helper. Terughalen kan via git.
