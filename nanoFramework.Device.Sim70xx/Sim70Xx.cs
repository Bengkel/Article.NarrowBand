using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace nanoFramework.Device.Sim70xx
{
    public class Sim70Xx
    {
        /// <summary>
        /// 
        /// </summary>
        private readonly SerialPort _serialPort;

        /// <summary>
        /// Initiate sim device
        /// </summary>
        /// <param name="serialPort"></param>
        public Sim70Xx(SerialPort serialPort) => _serialPort = serialPort;

        /// <summary>
        /// 
        /// </summary>
        public int Retry { get; set; } = 3;

        /// <summary>
        /// 
        /// </summary>
        public SystemMode SystemMode { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public ConnectionStatus NetworkConnected { get; set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 
        /// </summary>
        public ConnectionStatus EndpointConnected { get; set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 
        /// </summary>
        public ConnectionStatus TopicConnected { get; set; } = ConnectionStatus.Disconnected;

        /// <summary>
        /// 
        /// </summary>
        public string Operator { get; private set; } = "Unknown";

        /// <summary>
        /// 
        /// </summary>
        public string IPAddress { get; private set; } = "0.0.0.0";

        /// <summary>
        /// 
        /// </summary>
        public string SubTopic { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string PubTopic { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="systemMode"></param>
        /// <param name="enableReporting"></param>
        /// <param name="wait"></param>
        public void SetNetworkSystemMode(SystemMode systemMode = SystemMode.GSM, bool enableReporting = true, int wait = 5000)
        {
            SimController.SetSystemMode(_serialPort, systemMode, enableReporting);

            Thread.Sleep(wait);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apn"></param>
        public void NetworkConnect(string apn)
        {
            if (!_serialPort.IsOpen)
            {
                NetworkConnected = ConnectionStatus.Disconnected;
                return;
            }

            var retryCount = 0;

            do
            {
                retryCount++;

                SimController.NetworkConnect(_serialPort, apn, retryCount);

            } while (NetworkConnected == ConnectionStatus.Disconnected && retryCount < Retry);
        }

        /// <summary>
        /// 
        /// </summary>
        public void NetworkDisconnect()
        {
            if (_serialPort.IsOpen &&
                NetworkConnected == ConnectionStatus.Disconnected)
            {
                SimController.NetworkDisconnect(_serialPort);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="hubName"></param>
        /// <param name="sasToken"></param>
        /// <param name="portNumber"></param>
        /// <param name="apiVersion"></param>
        /// <param name="wait"></param>
        public void ConnectAzureIoTHub(string deviceId, string hubName, string sasToken, int portNumber = 8883, string apiVersion = "2021-04-12", int wait = 5000)
        {
            if (!_serialPort.IsOpen || NetworkConnected == ConnectionStatus.Disconnected)
            {
                EndpointConnected = ConnectionStatus.Disconnected;
                return;
            }

            string username = $"{hubName}.azure-devices.net/{deviceId}/?api-version={apiVersion}";
            string endpointUrl = $"{hubName}.azure-devices.net";

            var retryCount = 0;

            do
            {
                retryCount++;

                EndpointConnected = SimController.EndpointConnect(_serialPort, deviceId, endpointUrl, portNumber, username, sasToken, wait);

            } while (EndpointConnected == ConnectionStatus.Disconnected && retryCount < Retry);

            //https://learn.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d
            SubTopic = $"devices/{deviceId}/messages/devicebound/#";

            //https://learn.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-d2c
            PubTopic = $"devices/{deviceId}/messages/events/";
        }

        //TODO ConnectEndpoint

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        public void Subscribe2Topic(string topic)
        {
            var retryCount = 0;

            do
            {
                retryCount++;

                TopicConnected = SimController.SubscribeToTopic(_serialPort, topic);

            } while (TopicConnected == ConnectionStatus.Disconnected && retryCount < Retry);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool SendMessage(SerialPort serialPort, string message)
        {
            return SimController.SendMessage(serialPort, message, PubTopic);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="skipSubscription"></param>
        public void DisonnectAzureIoTHub(string topic, bool skipSubscription = false)
        {
            if (_serialPort.IsOpen &&
                NetworkConnected == ConnectionStatus.Disconnected &&
                EndpointConnected == ConnectionStatus.Connected)
            {
                TopicConnected = (TopicConnected == ConnectionStatus.Connected) ?
                    SimController.UnsubscribeFromTopic(_serialPort, topic) :
                    TopicConnected;

                EndpointConnected = SimController.EndpointDisconnect(_serialPort);
            }
        }

        //TODO DisconnectEndpoint

        /// <summary>
        /// 
        /// </summary>
        public void ReadResponse()
        {
            if (!_serialPort.IsOpen)
            {
                NetworkConnected = ConnectionStatus.Disconnected;
                return;
            }

            if (_serialPort.BytesToRead > 0)
            {
                byte[] buffer = new byte[_serialPort.BytesToRead];

                var bytesRead = _serialPort.Read(buffer, 0, buffer.Length);

                try
                {
                    string responseMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    switch (responseMessage)
                    {
                        case string m when m.Contains("ERROR"):

                            Debug.WriteLine(responseMessage);

                            break;
                        case string m when m.Contains("+COPS:"):

                            Operator = SimController.ExtractATResponse(responseMessage);

                            break;
                        case string m when m.Contains("+CNACT:"):

                            IPAddress = (IPAddress == "0.0.0.0") ?
                                SimController.ExtractATResponse(responseMessage) :
                                IPAddress;

                            NetworkConnected = (IPAddress == "0.0.0.0") ?
                                ConnectionStatus.Disconnected :
                                ConnectionStatus.Connected;

                            break;
                    }
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception.Message);
                }
            }
        }
    }
}