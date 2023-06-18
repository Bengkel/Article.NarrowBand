using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Threading;

namespace nanoFramework.Device.Sim70xx
{
    internal static class SimController
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="systemMode"></param>
        /// <param name="enableReporting"></param>
        public static void SetSystemMode(SerialPort serialPort, SystemMode systemMode, bool enableReporting)
        {
            try
            {
                var reportingEnabled = (enableReporting) ? 1 : 0;

                ExecuteCommand(serialPort, $"AT+CNSMOD={reportingEnabled},{(int)systemMode}");
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="apn"></param>
        /// <param name="retryCount"></param>
        /// <returns></returns>
        public static ConnectionStatus NetworkConnect(SerialPort serialPort, string apn, int retryCount)
        {
            try
            {
                //Read Signal Quality
                ExecuteCommand(serialPort, "AT+CSQ");

                //Return current Operator
                ExecuteCommand(serialPort, "AT+COPS?");

                //Get Network APN in CAT-M or NB-IoT
                ExecuteCommand(serialPort, "AT+CGNAPN");

                //Define PDP Context, saves APN
                ExecuteCommand(serialPort, $"AT+CGDCONT=1,\"IP\",\"{apn}\"");

                //if (retryCount > 2)
                //{
                //    // Deactive App Network on error
                //    ExecuteCommand(serialPort, "AT+CNACT=0,0");
                //}

                ////App Network Active, assign IP
                //ExecuteCommand(serialPort, "AT+CNACT=0,2");

                //Read IP
                ExecuteCommand(serialPort, "AT+CNACT?");

                return ConnectionStatus.Connected;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);

                return ConnectionStatus.Error;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <returns></returns>
        public static ConnectionStatus NetworkDisconnect(SerialPort serialPort)
        {
            try
            {
                ExecuteCommand(serialPort, "AT+CNACT=0,0");

                return ConnectionStatus.Disconnected;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);

                return ConnectionStatus.Error;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="clientId"></param>
        /// <param name="endpointUrl"></param>
        /// <param name="portNumber"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="wait"></param>
        /// <returns></returns>
        public static ConnectionStatus EndpointConnect(SerialPort serialPort, string clientId, string endpointUrl, int portNumber, string username, string password, int wait = 5000)
        {
            try
            {
                //Simcom module MQTT parameter that sets the client id
                ExecuteCommand(serialPort, $"AT+SMCONF=\"CLIENTID\",\"{clientId}\"");

                //Set MQTT time to connect server
                ExecuteCommand(serialPort, "AT+SMCONF=\"KEEPTIME\",60");

                //Simcom module MQTT parameter that sets the server URL and port
                ExecuteCommand(serialPort, $"AT+SMCONF=\"URL\",\"{endpointUrl}\",\"{portNumber}\"");

                //Delete messages after they have been successfully sent
                ExecuteCommand(serialPort, "AT+SMCONF=\"CLEANSS\",1");

                //Quality of Service 
                ExecuteCommand(serialPort, "AT+SMCONF=\"QOS\",1");

                //Simcom module MQTT parameter that sets the api endpoint for the specific device
                ExecuteCommand(serialPort, $"AT+SMCONF=\"USERNAME\",\"{username}\"");

                //Simcom module MQTT parameter that sets the secure access token
                ExecuteCommand(serialPort, $"AT+SMCONF=\"PASSWORD\",\"{password}\"");

                //Simcom module MQTT open the connection
                ExecuteCommand(serialPort, "AT+SMCONN", wait);

                return ConnectionStatus.Connected;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);

                ExecuteCommand(serialPort, "+CEDUMP=1");

                return ConnectionStatus.Error;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="subTopic"></param>
        public static ConnectionStatus SubscribeToTopic(SerialPort serialPort, string topic)
        {
            try
            {
                ExecuteCommand(serialPort, $"AT+SMSUB=\"{topic}\",1");

                return ConnectionStatus.Connected;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);

                return ConnectionStatus.Error;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="subTopic"></param>
        public static ConnectionStatus UnsubscribeFromTopic(SerialPort serialPort, string topic)
        {
            try
            {
                ExecuteCommand(serialPort, $"AT+SMUNSUB=\"{topic}\"");

                return ConnectionStatus.Connected;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);

                return ConnectionStatus.Error;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="topic"></param>
        /// <returns></returns>
        public static ConnectionStatus EndpointDisconnect(SerialPort serialPort)
        {
            try
            {
                ExecuteCommand(serialPort, "AT+SMDISC");

                return ConnectionStatus.Disconnected;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);

                return ConnectionStatus.Error;
            }
        }

        /// <summary>
        /// Send message to the serial port
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static bool SendMessage(SerialPort serialPort, string message, string pubTopic)
        {
            try
            {
                //Simcom module MQTT subscribe to D2C topic
                ExecuteCommand(serialPort, $"AT+SMPUB=\"{pubTopic}\",{message.Length},1,1");

                //Simcom module MQTT sends the message
                ExecuteCommand(serialPort, message);

                return true;
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);

                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="responseMessage"></param>
        /// <returns></returns>
        public static string ExtractATResponse(string responseMessage)
        {
            return Regex.Match(responseMessage, @"""([^""]*)""").Groups[1].Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serialPort"></param>
        /// <param name="command"></param>
        /// <param name="wait"></param>
        private static void ExecuteCommand(SerialPort serialPort, string command, int wait = 1000)
        {
            serialPort.WriteLine($"{command}\r");

            Debug.WriteLine(command);

            Thread.Sleep(wait);
        }
    }
}
