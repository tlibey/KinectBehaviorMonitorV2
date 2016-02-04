using System;
using System.Linq;
using System.IO.Ports;
namespace KinectBehaviorMonitorV2
{
    /// <summary>
    /// Handles the communication between this program and the arduino control hub
    /// ComPortString needs to be set to whatever com port the main arduino is plugged in to (or could easily set this up to dynamically allocate the com port)
    /// checkSerialInput is called within the GMV calculation in the mainWindow.xaml.cs script
    ///     if there is information from the control hub sitting in the serial input stream, this class causes the mainwindow.xaml.cs script to save video and increment the event counter
    /// </summary>
    class KinectBehavior_PortHandler
    {
        string ComPortString = "COM5"; //change this to the port the arduino control hub is connected to 
        string lastTreat = ""; //holds serial information send by the control hub
        SerialPort serialPort1 = new SerialPort(); 
        double lastSerialRead = 0; // prevents reading the serial port too frequently
        KinectBehavior_FileHandler fileHandler; //allows direct saving of the event data to file
        bool usingSerial = false; //change this flag if you don't want to use the arduino control hub (useful for testing)

        //initialization called in mainwindow.xaml.cs
        public KinectBehavior_PortHandler(KinectBehavior_FileHandler fh) { //constructor
            fileHandler = fh;

            //can set this value to autoenable ports for specific environments or users (useful for developing on one computer and implementing on another)
            string environmentUserName = "";
            if(Environment.UserName == environmentUserName) 
            {
                usingSerial = true;
            }

            //checks to see if the comport exists, if it does then create the comport
            if (usingSerial && SerialPort.GetPortNames().Any(x => string.Compare(x, ComPortString, true) == 0))
            {
                serialPort1.PortName = ComPortString;
                serialPort1.BaudRate = 9600;
                serialPort1.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

                serialPort1.Open();
                serialPort1.ReadTimeout = 20;
            }
            else
            {
                Console.WriteLine("no comPort");
            }
        }

        //called by UI function to send a treat to the control hub (could be configured to send more complicated information)
        public void sendSerialTreat()
        {
            char[] test = new char[1];
            test[0] = 'A'; 
            if (usingSerial)
            {
                if (!serialPort1.IsOpen)
                {
                    serialPort1.Open();
                }
                serialPort1.Write(test, 0, 1);
                //serialPort1.Close();
            }
        }

        //called during GMV calculation in mainwindow.xaml.cs 
        //if enough time has passed and there is information in the serial port then read that information and send information back to the mainwindow
        //also saves the serial port info to file
        public bool checkSerialInput(double totalSecondsElapsed)
        {

            bool receivedTreat = false;
            if (usingSerial) { 
            if ((lastSerialRead) < totalSecondsElapsed - .5)
            {
                try
                {
                    lastTreat = serialPort1.ReadLine();
                }
                catch (Exception ex)
                {
                    lastTreat = null;
                    Console.WriteLine(ex.ToString());
                }
                lastSerialRead = totalSecondsElapsed;
                if (lastTreat != null)
                {
                    receivedTreat = true;
                    fileHandler.SaveEventData(totalSecondsElapsed, lastTreat); 
                }

            }
               }
            return receivedTreat;
        }

        //currently unused but could be used to handle more complicated information, or to more precisely sync incoming event data
        private static void DataReceivedHandler(
                       object sender,
                       SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            Console.WriteLine("Data Received:");
            Console.Write(indata);
        }
    }
}
