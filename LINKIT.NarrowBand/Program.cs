using nanoFramework.Hardware.Esp32;
using nanoFramework.Runtime.Native;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace LINKIT.NBLTE
{
    public class Program
    {
        static SerialPort _serialPort;
        static string _apn = "<YOUR-APN>";
        static int _preferedNetworkMode = 9;

        static bool _success = false;
        static int _retry = 0;
        static int _maximumRetry = 3;

        public static void Main()
        {
            //REMARK Display available serial ports
            AvailableSerialPorts();

            //REMARK Open serial port
            do
            {
                _retry++;

                Notify("SerialPort", $"Attempt {_retry}", true);

                _success = OpenSerialPort();

            } while (!_success && _retry < _maximumRetry);

            CheckStatus();

            //REMARK Setup an event handler that will fire when a char is received in the serial device input stream
            _serialPort.DataReceived += SerialDevice_DataReceived;

            //REMARK Switch to prefered network mode
            SetNetworkSystemMode(false, _preferedNetworkMode);

            //REMARK Connect to narrow band network
            do
            {
                _retry++;

                Notify("APN", $"Attempt {_retry}", true);

                ConnectAccessPoint();

            } while (!_success && _retry < _maximumRetry);

            DisconnectAccessPoint();

            CloseSerialPort();

            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// Write Console Notification
        /// </summary>
        /// <param name="category"></param>
        /// <param name="message"></param>
        private static void Notify(string category, string message, bool isDebug)
        {
            var notification = $"[{category.PadRight(15, '.')}] {message}";

            if (isDebug)
            {
                //REMARK Development only
                Debug.WriteLine(notification);
            }
            else
            {
                //REMARK Production and development
                Console.WriteLine(notification);
            }
        }

        /// <summary>
        /// Get list of available serial ports
        /// </summary>
        private static void AvailableSerialPorts()
        {
            //REMARK  get available ports
            var ports = SerialPort.GetPortNames();

            Notify("Port", "Scan available ports", true);

            foreach (string port in ports)
            {
                Notify("Port", $"\t{port}", true);
            }
        }

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
        private static bool OpenSerialPort(
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

            if (_serialPort == null || !_serialPort.IsOpen)
            {
                _serialPort = new SerialPort(port);

                //REMARK Set parameters
                _serialPort.BaudRate = baudRate;
                _serialPort.Parity = parity;
                _serialPort.StopBits = stopBits;
                _serialPort.Handshake = handshake;
                _serialPort.DataBits = dataBits;

                //REMARK If dealing with massive data input, increase the buffer size
                _serialPort.ReadBufferSize = readBufferSize;
                _serialPort.ReadTimeout = readTimeout;
                _serialPort.WriteTimeout = writeTimeout;

                try
                {
                    //REMARK Open the serial port
                    _serialPort.Open();

                    Notify("SerialPort", $"Port {_serialPort.PortName} opened", false);
                }
                catch (Exception exception)
                {
                    Notify("SerialPort", $"{exception.Message}", true);
                }

                //REMARK Set a watch char to be notified when it's available in the input stream
                _serialPort.WatchChar = watchChar;
            }

            return _serialPort.IsOpen;
        }

        /// <summary>
        /// Execute AT (attention) Command on modem
        /// </summary>
        /// <param name="command"></param>
        /// <param name="wait"></param>
        private static void ExecuteCommand(string command, int wait = 1000)
        {
            _serialPort.WriteLine($"{command}\r");
            Thread.Sleep(wait);
        }

        /// <summary>
        /// Set system connectivity mode
        /// </summary>
        /// <paramref name="enableReporting">Enable auto reporting of the network system mode information</paramref>
        /// <paramref name="mode">1 GSM, 3 EGPRS, 7 LTE M1,9 LTE NB<paramref name="mode"/>
        internal static void SetNetworkSystemMode(bool enableReporting, int mode)
        {
            string systemMode = "LTE M1";

            switch (mode)
            {
                case 1:
                    systemMode = "GSM";
                    break;
                case 3:
                    systemMode = "EGPRS";
                    break;
                case 9:
                    systemMode = "LTE NB";
                    break;
                default:
                    mode = 7;
                    break;
            }

            //REMARK Toggle reporting
            var reportingEnabled = (enableReporting) ? 1 : 0;

            //REMARK Set mode
            ExecuteCommand($"AT+CNSMOD={reportingEnabled},{mode}");

            Notify("NetworkMode", $"Switching to {systemMode}", false);

            Thread.Sleep(5000);
        }

        /// <summary>
        /// Reset <see cref="_retry"/> and <see cref="_success"/> after successfull finish
        /// Reboot on failure
        /// </summary>
        private static void CheckStatus()
        {
            Notify("STATUS", $"\r\nSuccess: {_success}\r\n", true);

            if (_retry >= _maximumRetry && !_success) Power.RebootDevice();

            _success = false;
            _retry = 0;
        }

        /// <summary>
        /// Connect to the provider access point
        /// </summary>
        private static void ConnectAccessPoint()
        {
            try
            {
                //REMARK Indicates if password is required
                //ExecuteCommand("AT+CGPSIN");

                //REMARK Read Signal Quality
                ExecuteCommand("AT+CSQ");

                //REMARK Return current Operator
                ExecuteCommand("AT+COPS?");

                //REMARK Get Network APN in CAT-M or NB-IoT
                ExecuteCommand("AT+CGNAPN");

                //REMARK Define PDP Context, saves APN
                ExecuteCommand($"AT+CGDCONT=1,\"IP\",\"{_apn}\"");

                if (_retry > 2)
                {
                    //REMARK Deactive App Network on error
                    ExecuteCommand("AT+CNACT=0,0");
                }

                //REMARK App Network Active, assign IP
                ExecuteCommand("AT+CNACT=0,2");

                //REMARK Read IP
                ExecuteCommand("AT+CNACT?");

                Notify("APN", $"Connected to Access Point {_apn}", false);
            }
            catch (Exception exception)
            {
                Notify("APN", $"{exception.Message}", true);
            }
        }

        /// <summary>
        /// Event raised when message is received from the serial port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void SerialDevice_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort serialDevice = (SerialPort)sender;
            ReadMessage(serialDevice);
        }

        /// <summary>
        /// Read message from the serial port
        /// </summary>
        /// <param name="serialDevice"></param>
        private static void ReadMessage(SerialPort serialDevice)
        {
            if (serialDevice.BytesToRead > 0)
            {
                Notify("Read", $"{serialDevice.BytesToRead} Bytes to read from {serialDevice.PortName}", true);

                byte[] buffer = new byte[serialDevice.BytesToRead];

                var bytesRead = serialDevice.Read(buffer, 0, buffer.Length);

                Notify("Read", $"Completed: {bytesRead} bytes were read from {serialDevice.PortName}", true);

                try
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    _success = true;

                    switch (message)
                    {
                        //REMARK On error
                        case string m when m.Contains("ERROR"):
                            _success = false;
                            break;
                    }

                    Notify("Read", $"Acknowledgement:\r\n{message}\r\n", true);
                }
                catch (Exception exception)
                {
                    Notify("Read", $"Acknowledgement:\r\n{exception.Message}\r\n", true);

                    _success = false;
                }
            }
            else
            {
                //Notify("Read", "Noting to read", true);
            }
        }

        /// <summary>
        /// Disconnect to the provider access point
        /// </summary>
        private static void DisconnectAccessPoint()
        {
            //REMARK Simcom module MQTT open the disconnect from APN
            ExecuteCommand("AT+CNACT=0,0");

            Notify("APN", $"Disconnect", false);
        }

        /// <summary>
        /// Close the serial port
        /// </summary>
        private static void CloseSerialPort()
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();

                Notify("SerialPort", $"Port closed", false);
            }
        }
    }
}
