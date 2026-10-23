using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace User.MQTTPlugin
{
    public partial class SettingsControlV2 : UserControl
    {
        public MQTTPlugin Plugin { get; }
        private IDictionary<string, string> settings = new Dictionary<string, string>();
        private Dictionary<string, ValueTuple<string, string>> ActionsToStore = new Dictionary<string, ValueTuple<string, string>>();

        private static readonly Brush PlaceholderBrush = Brushes.Gray;

        public SettingsControlV2() : this(MQTTPlugin.Instance) { }

        public SettingsControlV2(MQTTPlugin plugin)
        {
            Plugin = plugin;
            InitializeComponent();

            settings = MQTTSettings.LoadSettings();
            txtBroker.Text = settings["mqttserver"];
            txtPort.Text = settings["mqttport"];
            txtUser.Text = settings["mqttuser"];
            txtTopic.Text = settings["mqtttopic"];
            chkUseTls.IsChecked = SettingsRules.IsTlsEnabled(settings);

            txtPassword.Password = Crypt.UnprotectString(settings["mqttpass"]);

            LoadExistingActions();

            ConnectionStatus.Content = SettingsViewLogic.InitialConnectionStatus(
                SettingsRules.HasBrokerConfigured(settings), MqttConnectionManager.Shared.IsConnected);
        }

        private void LoadExistingActions()
        {
            if (string.IsNullOrEmpty(settings["mqttactions"])) return;

            try
            {
                ActionsToStore = ActionsSerializer.Deserialize(settings["mqttactions"]);
                foreach (var action in ActionsToStore)
                {
                    AddRowToUi(action.Key, action.Value.Item1, action.Value.Item2, registerImmediately: true);
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Could not parse MQTT Actions! {ex.Message}");
                ActionsStatus.Content = "Could not load previously saved actions — check the log.";
            }
        }

        // --- Connection section (unchanged behavior from SettingsControl) ---

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e) => StoreInSettings("mqttserver", sender);
        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e) => StoreInSettings("mqttport", sender);
        private void TextBox_TextChanged_2(object sender, TextChangedEventArgs e) => StoreInSettings("mqttuser", sender);
        private void PasswordBox_TextChanged_3(object sender, RoutedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;
            if (passwordBox != null)
            {
                if (btnSave != null) btnSave.IsEnabled = false;
                string encrypted = Crypt.ProtectString(passwordBox.Password);
                MQTTSettings.RememberSetting("mqttpass", encrypted);
            }
        }
        private void TextBox_TextChanged_4(object sender, TextChangedEventArgs e) => StoreInSettings("mqtttopic", sender);

        private void chkUseTls_Changed(object sender, RoutedEventArgs e)
        {
            MQTTSettings.RememberSetting("mqtttls", chkUseTls.IsChecked == true ? "True" : "False");
            string port = SettingsViewLogic.PortAfterTlsChange(chkUseTls.IsChecked, txtPort.Text);
            if (port != txtPort.Text) txtPort.Text = port;
        }

        private void StoreInSettings(string key, object sender)
        {
            if (btnSave != null) btnSave.IsEnabled = false;
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                string value = textBox.Text;
                if (key == "mqttpass") value = Crypt.ProtectString(value);
                MQTTSettings.RememberSetting(key, value);
            }
        }

        private async void SHButtonPrimary_Click(object sender, RoutedEventArgs e)
        {
            btnSave.IsEnabled = false;
            ConnectionStatus.Content = "Connecting...";
            try
            {
                await MQTTSettings.SaveSettingsAsync();
            }
            catch (Exception)
            {
                // SaveSettingsAsync has logged the reason and discarded the staged values.
                ConnectionStatus.Content = SettingsViewLogic.SaveFailedMessage;
                btnSave.IsEnabled = true;
                return;
            }
            ConnectionStatus.Content = MqttConnectionManager.Shared.IsConnected ? SettingsViewLogic.ConnectionSuccessMessage : SettingsViewLogic.ConnectionFailedMessage;
            btnSave.IsEnabled = true;
        }

        private async void SHButtonPrimary_Click_1(object sender, RoutedEventArgs e)
        {
            if (!SettingsRules.TryParsePort(txtPort.Text, out int port))
            {
                ConnectionStatus.Content = "Invalid port.";
                return;
            }

            ConnectionStatus.Content = "Testing connection...";
            try
            {
                var testSettings = new ConnectionSettings(txtBroker.Text, port, txtUser.Text, txtPassword.Password, txtTopic.Text, chkUseTls.IsChecked == true);
                bool connectionSuccessful = await MqttConnectionManager.Shared.TestAsync(testSettings);
                ConnectionStatus.Content = connectionSuccessful ? SettingsViewLogic.ConnectionTestSuccessMessage : SettingsViewLogic.ConnectionFailedMessage;
                btnSave.IsEnabled = connectionSuccessful;
            }
            catch (Exception)
            {
                ConnectionStatus.Content = SettingsViewLogic.ConnectionFailedMessage;
            }
        }

        private void txtPort_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = !SettingsViewLogic.IsPortKey(e.Key);
        }

        // --- Actions section (redesigned) ---

        private void btnAddAction_Click(object sender, RoutedEventArgs e)
        {
            AddRowToUi("", "", "", registerImmediately: false);
        }

        private void AddRowToUi(string name, string topic, string message, bool registerImmediately)
        {
            Grid row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            TextBox txtName = new TextBox { Margin = new Thickness(0, 0, 10, 0), Text = name };
            TextBox txtTopic = new TextBox { Margin = new Thickness(0, 0, 10, 0), Text = topic };
            TextBox txtMessage = new TextBox { Margin = new Thickness(0, 0, 10, 0), Text = message };
            Button btnDelete = new Button { Content = "✕", Width = 28, Height = 22, ToolTip = "Remove this row" };

            if (string.IsNullOrEmpty(name)) SetupPlaceholder(txtName, "e.g. ToggleClimate");
            if (string.IsNullOrEmpty(topic)) SetupPlaceholder(txtTopic, "e.g. simhub/commands/climate");
            if (string.IsNullOrEmpty(message)) SetupPlaceholder(txtMessage, "e.g. toggle");

            Grid.SetColumn(txtName, 0);
            Grid.SetColumn(txtTopic, 1);
            Grid.SetColumn(txtMessage, 2);
            Grid.SetColumn(btnDelete, 3);
            row.Children.Add(txtName);
            row.Children.Add(txtTopic);
            row.Children.Add(txtMessage);
            row.Children.Add(btnDelete);

            btnDelete.Click += (s, e) => DeleteActionRow(row);

            stackPanelActions.Children.Add(row);

            if (registerImmediately && !string.IsNullOrEmpty(name))
            {
                SimHub.Plugins.UI.ControlsEditor controlsEditor = new SimHub.Plugins.UI.ControlsEditor
                {
                    Name = $"ControlsEditor{stackPanelActions.Children.Count}",
                    FriendlyName = name,
                    ActionName = $"MQTTPlugin.{name}",
                    Width = (3 * 95) + (2 * 10),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                stackPanelActions.Children.Add(controlsEditor);
                row.Tag = controlsEditor;
                Plugin?.AddAction(name, topic, message);
            }
        }

        private void DeleteActionRow(Grid row)
        {
            int index = stackPanelActions.Children.IndexOf(row);
            if (index < 0) return;

            SimHub.Plugins.UI.ControlsEditor editor = row.Tag as SimHub.Plugins.UI.ControlsEditor;
            stackPanelActions.Children.Remove(row);

            if (editor != null)
            {
                string actionName = editor.FriendlyName;
                stackPanelActions.Children.Remove(editor);
                MessageBox.Show(
                    $"'{actionName}' was removed from this list. SimHub may still keep its Control Mapping under Controls and events — " +
                    "you can delete that mapping there if you no longer need it. This plugin can't remove it automatically.",
                    "Action removed");
            }
        }

        private void SetupPlaceholder(TextBox box, string placeholder)
        {
            box.Tag = placeholder;
            box.Text = placeholder;
            box.Foreground = PlaceholderBrush;

            box.GotFocus += (s, e) =>
            {
                if (Equals(box.Foreground, PlaceholderBrush))
                {
                    box.Text = "";
                    box.ClearValue(TextBox.ForegroundProperty);
                }
            };
            box.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = (string)box.Tag;
                    box.Foreground = PlaceholderBrush;
                }
            };
        }

        private string GetRealText(TextBox box)
        {
            return SettingsViewLogic.RealText(box.Text, Equals(box.Foreground, PlaceholderBrush));
        }

        private void btnSaveActions_Click(object sender, RoutedEventArgs e)
        {
            ActionsToStore.Clear();
            int skipped = 0;

            var rows = stackPanelActions.Children.OfType<Grid>().ToList();
            foreach (Grid row in rows)
            {
                TextBox txtName = (TextBox)row.Children[0];
                TextBox txtTopic = (TextBox)row.Children[1];
                TextBox txtMessage = (TextBox)row.Children[2];

                string name = GetRealText(txtName);
                string topic = GetRealText(txtTopic);
                string message = GetRealText(txtMessage);

                if (!SettingsViewLogic.IsCompleteAction(name, topic, message))
                {
                    skipped++;
                    continue;
                }

                ActionsToStore[name] = (topic, message);
                Plugin?.AddAction(name, topic, message);

                if (row.Tag is SimHub.Plugins.UI.ControlsEditor existingEditor)
                {
                    existingEditor.FriendlyName = name;
                    existingEditor.ActionName = $"MQTTPlugin.{name}";
                }
                else
                {
                    int index = stackPanelActions.Children.IndexOf(row);
                    SimHub.Plugins.UI.ControlsEditor controlsEditor = new SimHub.Plugins.UI.ControlsEditor
                    {
                        Name = $"ControlsEditor{index}",
                        FriendlyName = name,
                        ActionName = $"MQTTPlugin.{name}",
                        Width = (3 * 95) + (2 * 10),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    stackPanelActions.Children.Insert(index + 1, controlsEditor);
                    row.Tag = controlsEditor;
                }
            }

            MQTTSettings.Store["mqttactions"] = ActionsSerializer.Serialize(ActionsToStore);
            MQTTSettings.Store.Save();

            ActionsStatus.Content = SettingsViewLogic.ActionsSavedMessage(ActionsToStore.Count, skipped);
        }
    }
}