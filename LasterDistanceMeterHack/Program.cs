using System;
using System.Threading;
using Windows.Devices.Bluetooth;
using Windows;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace LaserDistanceMeter
{
    class Program
    {

        private GattCharacteristic sendCommandCharacteristic;

        string deviceAddress = "34:03:de:41:06:31"; // bluetooth device address, could change
        static void Main(string[] args) { new Program(); }

        public Program()
        {
            string[] requestedProperties = {};
            DeviceWatcher deviceWatcher =
                        DeviceInformation.CreateWatcher(
                                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                                requestedProperties,
                                DeviceInformationKind.AssociationEndpoint);
            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Updated += DeviceWatcher_Updated;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.EnumerationCompleted += DeviceWatcher_EnumerationCompleted;
            deviceWatcher.Stopped += DeviceWatcher_Stopped;
            deviceWatcher.Start();

            Console.WriteLine("Enter char to get value...");
            while (System.Console.ReadLine() != null)
            {
                SendCommandToDevice();
                Thread.Sleep(2000);
                SendCommandToDevice();
            }
        }

        private void DeviceWatcher_Stopped(DeviceWatcher sender, object args) { }
        private void DeviceWatcher_EnumerationCompleted(DeviceWatcher sender, object args) { }
        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args) { }
        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args) { }
        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args) {
            if (args.Id.Contains(deviceAddress)) { ConnectDevice(args.Id); } 
        }

        async void ConnectDevice(string id)
        {
            BluetoothLEDevice bluetoothLeDevice = await BluetoothLEDevice.FromIdAsync(id);
            var srvResultList = await bluetoothLeDevice.GetGattServicesAsync();
            var srvResult = await bluetoothLeDevice.GetGattServicesForUuidAsync(new Guid("0000ffb0-0000-1000-8000-00805f9b34fb"), BluetoothCacheMode.Cached);
            if (srvResult.Status != GattCommunicationStatus.Success || !srvResult.Services.Any())
            {
                Console.WriteLine("Cannot find service for device");
                return;
            }
            Console.WriteLine("connected");
            var service = srvResult.Services.First();
            var chrResult = await service.GetCharacteristicsAsync();
            if (chrResult.Status != GattCommunicationStatus.Success) { return; }
            var chrs = from x in chrResult.Characteristics
                       select x;
            var gattCharacteristics = chrs as GattCharacteristic[] ?? chrs.ToArray();
            if (!gattCharacteristics.Any()) { return; }
            await gattCharacteristics[1].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
            gattCharacteristics[1].ValueChanged += gattCharacteristics_1_ValueChanged;

            sendCommandCharacteristic = gattCharacteristics[1];
        }

        private void gattCharacteristics_1_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] buffer = args.CharacteristicValue.ToArray();
           
            int counter = 0;
            foreach (var elem in buffer)
            {
                Console.WriteLine(String.Format("{0, 0:d3}: {1, 0:d3} 0x{2, 0:X2}", counter, elem, elem));
                counter++;
            }

            Console.WriteLine(new System.Text.ASCIIEncoding().GetString(buffer));
        }

        private async void SendCommandToDevice()
        {
            var writer = new DataWriter();
            writer.WriteBytes(new byte[] { 0x64, 0x74, 0x0d, 0x0a, 0x00 });
            GattCommunicationStatus result = await sendCommandCharacteristic.WriteValueAsync(writer.DetachBuffer());
            if (result == GattCommunicationStatus.Success)
            {
                Console.WriteLine("Command was sent successfully...");
            }
        }
    }
}
