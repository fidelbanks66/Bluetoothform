using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace WindowsFormsApp1
{
    public static class BluetoothLE
    {
        static List<DeviceInformation> devices = new List<DeviceInformation>();
        static BluetoothLEDevice Device;
        static DeviceInformation DeviceInfo;
        static GattCharacteristic characteristic;
        static List<string> message_buffer = new List<string>();

        public static async Task<Dictionary<string, string>> FindDevices()
        {
            Dictionary<string, string> devices = new Dictionary<string, string>();

            try
            {
                DeviceInformationCollection device_info_collection;

                // returns paired devices only
                device_info_collection = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));

                foreach (DeviceInformation device in device_info_collection)
                {
                    string device_id = device.Id;
                    string device_name = device.Name;

                    Console.WriteLine(device_name + " " + device_id);
                    devices.Add(device_id, device_name);
                }

                Console.WriteLine("FindAllAsync");

                
            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return devices;
        }

        public static async void ConnectAndPair(string device_id)
        {
            try
            {
                if (device_id.Length > 0)
                {
                    Device = await BluetoothLEDevice.FromIdAsync(device_id); // opening a connection to the device

                    if (!Device.DeviceInformation.Pairing.IsPaired) // pair if the device is not aleady
                    {
                        DevicePairingResult pair_results = await Device.DeviceInformation.Pairing.PairAsync(); // pair request
                        Console.WriteLine(pair_results);
                    }

                    Thread.Sleep(1000); // we have to sleep becasue the RN4871 is really slow

                    GetDeviceGATTServices();
                }
            } catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        // handles closing the bluetooth connection
        public static void Disconnect()
        {
            try
            {
                Device.Dispose(); // this call doesn't actually disconnect from the bt device, why???
                Console.WriteLine(Device.Name + " disconnected");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        // retrieves the gatt services available from the device
        // for each service it retreives charactistics and properties
        // this will determin which charactistic we use to read and write data
        private static async void GetDeviceGATTServices()
        {
            Console.WriteLine("get available device gatt services");

            if (Device != null)
            {
                GattDeviceServicesResult GattServices;
                GattCharacteristicsResult GattServiceCharacteristics;
                GattCharacteristicProperties GattCharacteristicProperties;

                try
                {
                    GattServices = await Device.GetGattServicesAsync(); // get all the available gatt services from the device

                    foreach (var service in GattServices.Services)
                    {
                        Console.WriteLine("service: " + service.Uuid);

                        GattServiceCharacteristics = await service.GetCharacteristicsAsync(); // returns the charactistics of the service

                        if (GattServiceCharacteristics.Status == GattCommunicationStatus.Success)
                        {
                            foreach (GattCharacteristic charact in GattServiceCharacteristics.Characteristics)
                            {
                                GattCharacteristicProperties = charact.CharacteristicProperties; // returns the properties of the charactistic i.e. write, read, notify etc...
                                                                                                 //Console.WriteLine("characteristic " + characteristic);
                                Console.WriteLine("properties: " + GattCharacteristicProperties);

                                // check if the charateristic has the properties needed
                                if (GattCharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse) &&
                                    GattCharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
                                {
                                    Console.WriteLine("charactistic found with the properties we need");

                                    //Writing to the CCCD tells the Server device that this client wants to know each time that particular characteristic value changes. To do this:
                                    Console.WriteLine("telling the ble we want to be notified when new data is available");
                                    GattCommunicationStatus status = await charact.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

                                    if (status == GattCommunicationStatus.Success)
                                    {
                                        // Server has been informed of clients interest, no subscribing to the valuechanged event.
                                        charact.ValueChanged += Characteristic_ValueChanged;
                                        Console.WriteLine("subscribed to the value changed event");
                                    }

                                    characteristic = charact; // making the charactistic public for other methods to use.
                                }
                            }
                        }
                    }

                    // ble should show as connected now we've requested the gatt services.
                    Console.WriteLine(Device.Name + " connection status: " + Device.ConnectionStatus);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private static void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            try
            {
                // device has notified us new data is ready
                var data_reader         = DataReader.FromBuffer(args.CharacteristicValue);
                string buffer_string    = data_reader.ReadString(args.CharacteristicValue.Length);
                string message_str      = "";

                message_buffer.Add(buffer_string);

                message_str = String.Join("", message_buffer);

                Console.WriteLine(message_str);
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
        }

        public static async Task<bool> Send_Command(string message_str)
        {
            bool message_sent = false;

            // obviously we don't want to send anything unless we have a connection and a command to send
            if (message_str.Length > 0 && Device.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                Console.WriteLine("send_command " + message_str);
                var data_writer = new DataWriter(); //get new data writer 

                byte[] cmd = Encoding.ASCII.GetBytes(message_str + Environment.NewLine); // convert ascii command to bytes

                try
                {
                    data_writer.WriteBytes(cmd);

                    // write bytes to the device
                    GattCommunicationStatus status = await characteristic.WriteValueAsync(data_writer.DetachBuffer());
                    if (status == GattCommunicationStatus.Success)
                    {
                        Console.WriteLine("command sent");
                        message_sent = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return message_sent;
        }
    }
}