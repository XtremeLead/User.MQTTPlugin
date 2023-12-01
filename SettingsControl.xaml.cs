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
        public SettingsControl()
        {
            InitializeComponent();
            var setting = new MQTTSettings();
            txtBroker.Text = setting.GetSetting("mqttserver");
            txtPort.Text = setting.GetSetting("mqttport");
            txtUser.Text = setting.GetSetting("mqttuser");
            txtPassword.Text = setting.GetSetting("mqttpass");
            txtTopic.Text = setting.GetSetting("mqtttopic");
            settings = MQTTSettings.LoadSettings();
            ConnectionStatus.Content = MQTTClient.CLIENT.IsConnected ?
               "Successfully connected. Happy simracing!" :
               "Connection failed. Please check your settings or broker availability.";
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
            if (btnSave != null) btnSave.IsEnabled = false;
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                string value = textBox.Text;
                MQTTSettings.RememberSetting(key, value);
            }
        }

        private void SHButtonPrimary_Click(object sender, RoutedEventArgs e)
        {
            //save
            MQTTSettings.SaveSettings();
            ConnectionStatus.Content = MQTTClient.CLIENT.IsConnected ?
               "Successfully connected. Happy simracing!" :
               "Connection failed. Please check your settings or broker availability.";
        }

        private void SHButtonPrimary_Click_1(object sender, RoutedEventArgs e)
        {
            bool connectionSuccessful = MQTTClient.TestConnection(txtBroker.Text, int.Parse(txtPort.Text), txtUser.Text, txtPassword.Text);
            ConnectionStatus.Content = connectionSuccessful ?
                "Successfully connected. Click Save to Save your settings and connect." :
                "Connection failed. Please check your settings or broker availability.";
            Properties.Settings.Default["lastConnectionSuccessful"] = connectionSuccessful;
            btnSave.IsEnabled = connectionSuccessful;
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
