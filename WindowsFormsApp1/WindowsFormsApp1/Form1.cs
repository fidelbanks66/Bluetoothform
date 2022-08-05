using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async void button1_Click_1(object sender, EventArgs e)
        {
            Display_Message("start device scan");

            Dictionary<string, string> devices = await BluetoothLE.FindDevices();
            
            comboBox1.DataSource        = new BindingSource(devices, null);
            comboBox1.DisplayMember     = "Value";
            comboBox1.ValueMember       = "Key";

            Display_Message("device scan complete");
        }

        private void btConnect_Click(object sender, EventArgs e)
        {
            string device_id    = comboBox1.SelectedValue.ToString();
            string device_name  = comboBox1.SelectedText.ToString();

            Display_Message("connecting to " + device_name);
            
            BluetoothLE.ConnectAndPair(device_id);
        }

        private void btDisconnect_Click(object sender, EventArgs e)
        {
            Display_Message("disconnect device");
            BluetoothLE.Disconnect();
        }

        public void Display_Message(string info)
        {
            tbNewsReader.AppendText(info + Environment.NewLine);
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            string command = tbCommand.Text;
            Display_Message("sending message: " + command);
            BluetoothLE.Send_Command(command);
            Display_Message("message sent");
        }
    }
}
