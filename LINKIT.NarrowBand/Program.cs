using nanoFramework.Device.Sim70xx;
using nanoFramework.Hardware.Esp32;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace LINKIT.NBLTE
{
    public class Program
    {
        static SerialPort _serialPort;
        static Sim70Xx _sim;

        // Provider variables
        static string _apn = "sam.iot-provider.com";                                                                                                                                        //"<YOUR-APN>";

        // Azure EventHub variables
        static string _deviceId = "hive7";                                                                                                                                                  //"<YOUR-DEVICE-NAME>";
        static string _hubName = "lnkt-dev-weu-769-iot";                                                                                                                                    //"<YOUR-IOT-HUB-NAME>";
        static string _sasToken = "SharedAccessSignature sr=lnkt-dev-weu-769-iot.azure-devices.net%2Fdevices%2Fhive7&sig=mwYnj8cAtY8yp6%2BZz7xSOc56O%2BS03vuxEGs9TAYd9zA%3D&se=1687118655"; //"<YOUR-SAS-TOKEN>";

        public static void Main()
        {
            //Open serial port
            OpenSerialPort();

            //Setup an event handler that will fire when a char is received in the serial device input stream
            _serialPort.DataReceived += SerialDevice_DataReceived;

            _sim = new Sim70Xx(_serialPort);

            //Switch to prefered network mode
            _sim.SetNetworkSystemMode(SystemMode.LTE_NB, false);

            //Connect to network access point
            _sim.NetworkConnect(_apn);

            //Display network operator
            Debug.WriteLine(_sim.Operator);

            //Display Public IP address
            Debug.WriteLine(_sim.IPAddress);

            //Connect to Endpoint
            if (_sim.NetworkConnected == ConnectionStatus.Connected)
            {
                _sim.ConnectAzureIoTHub(_deviceId, _hubName, _sasToken);
            }

            _sim.SendMessage(_serialPort, $"test{Guid.NewGuid()}");

            //Disconnect from Endpoint
            if (_sim.EndpointConnected == ConnectionStatus.Connected)
            {
                _sim.DisonnectAzureIoTHub(null, true);
            }

            //Disconnect from network access point
            if (_sim.NetworkConnected == ConnectionStatus.Connected)
            {
                _sim.NetworkDisconnect();
            }

            CloseSerialPort();

            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// Event raised when message is received from the serial port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void SerialDevice_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            _sim.ReadResponse();
        }

        #region serial

        /// <summary>
        /// Configure and open the serial port for communication
        /// </summary>
        /// <param name="port"></param>
        /// <param name="baudRate"></param>
        /// <param name="parity"></param>
        /// <param name="stopBits"></param>
        /// <param name="handshake"></param>
        /// <param name="dataBits"></param>
        /// <param name="readBufferSize"></param>
        /// <param name="readTimeout"></param>
        /// <param name="writeTimeout"></param>
        /// <param name="watchChar"></param>
        private static void OpenSerialPort(
            string port = "COM3",
            int baudRate = 115200,
            Parity parity = Parity.None,
            StopBits stopBits = StopBits.One,
            Handshake handshake = Handshake.XOnXOff,
            int dataBits = 8,
            int readBufferSize = 2048,
            int readTimeout = 1000,
            int writeTimeout = 1000,
            char watchChar = '\r')
        {
            //REMARK Configure GPIOs 16 and 17 to be used in UART2 (that's refered as COM3)
            Configuration.SetPinFunction(16, DeviceFunction.COM3_RX);
            Configuration.SetPinFunction(17, DeviceFunction.COM3_TX);

            _serialPort = new(port)
            {
                //REMARK Set parameters
                BaudRate = baudRate,
                Parity = parity,
                StopBits = stopBits,
                Handshake = handshake,
                DataBits = dataBits,

                //REMARK If dealing with massive data input, increase the buffer size
                ReadBufferSize = readBufferSize,
                ReadTimeout = readTimeout,
                WriteTimeout = writeTimeout
            };

            try
            {
                //REMARK Open the serial port
                _serialPort.Open();
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
            }

            //REMARK Set a watch char to be notified when it's available in the input stream
            _serialPort.WatchChar = watchChar;
        }

        /// <summary>
        /// Close the serial port
        /// </summary>
        private static void CloseSerialPort()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
        }

        #endregion
    }
}
