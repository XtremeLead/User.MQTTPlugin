using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        public MQTTPlugin Plugin { get; }
        public MQTTClient Client { get; }
        private IDictionary<string, string> settings = new Dictionary<string, string>();
        private static readonly string CONNECTION_FAILED_MESSAGE = "Connection failed. Please check your settings or broker availability.";
        private static readonly string CONNECTION_SUCCESS_MESSAGE = "Successfully connected. Happy simracing!";
        private static readonly string CONNECTION_TEST_SUCCESS_MESSAGE = "Successfully connected. Click Save to Save your settings and connect.";

        public SettingsControl()
        {
            InitializeComponent();
            settings = MQTTSettings.LoadSettings();
            txtBroker.Text = settings["mqttserver"];
            txtPort.Text = settings["mqttport"];
            txtUser.Text = settings["mqttuser"];
            txtPassword.Text = settings["mqttpass"];
            txtTopic.Text = settings["mqtttopic"];

            Dictionary<string, string> MqttSettingsFound = settings
                .Where(kvp => kvp.Key.IndexOf("mqtt") > -1 && kvp.Value != "")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            SimHub.Logging.Current.Info($"Found {MqttSettingsFound.Count} MQTT settings!");

            if (MqttSettingsFound.Count == 0)
            {
                ConnectionStatus.Content = "Please provide MQTT settings.";
            }

            if (MQTTClient.CLIENT != null || MqttSettingsFound.Count > 0)
            {
                ConnectionStatus.Content = MQTTClient.CLIENT.IsConnected ?
                 CONNECTION_SUCCESS_MESSAGE :
                 CONNECTION_FAILED_MESSAGE;
            }
        }
        public SettingsControl(MQTTPlugin plugin) : this()
        {
            Plugin = plugin;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttserver", sender);

        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttport", sender);
        }

        private void TextBox_TextChanged_2(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttuser", sender);
        }

        private void TextBox_TextChanged_3(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttpass", sender);
        }

        private void TextBox_TextChanged_4(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqtttopic", sender);
        }

        private void StoreInSettings(string key, object sender)
        {
            if (btnSave != null)
            {
                btnSave.IsEnabled = false;
            }
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                string value = textBox.Text;
                MQTTSettings.RememberSetting(key, value);
            }
        }

        private void SHButtonPrimary_Click(object sender, RoutedEventArgs e)
        {
            MQTTSettings.SaveSettings();
            ConnectionStatus.Content = MQTTClient.CLIENT.IsConnected ?
               CONNECTION_SUCCESS_MESSAGE :
               CONNECTION_FAILED_MESSAGE;
        }

        private void SHButtonPrimary_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                bool connectionSuccessful = MQTTClient.TestConnection(txtBroker.Text, int.Parse(txtPort.Text), txtUser.Text, txtPassword.Text);
                ConnectionStatus.Content = connectionSuccessful ?
                    CONNECTION_TEST_SUCCESS_MESSAGE :
                    CONNECTION_FAILED_MESSAGE;
                Properties.Settings.Default["lastConnectionSuccessful"] = connectionSuccessful;
                btnSave.IsEnabled = connectionSuccessful;
            }
            catch (Exception)
            {
                ConnectionStatus.Content = CONNECTION_FAILED_MESSAGE;
            }
        }


        private void txtPort_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) ||
                (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }
    }
}
