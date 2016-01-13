using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
namespace KinectBehaviorMonitorV2
{
    class KinectBehavior_FileHandler
    {
        static string CoreFileName = @"C:/MoveCalc/BetaMonitoring/";
        static string CoreFileNameThis = CoreFileName + DateTime.Now.DayOfYear.ToString() + "_" + DateTime.Now.Hour.ToString() + "_" + DateTime.Now.Minute.ToString() + "_" + DateTime.Now.Second.ToString();
        string movementfileName = CoreFileNameThis + "/MovementValues.txt";
        string vfileName = CoreFileNameThis + "/Videos/";
        string sfileName = CoreFileNameThis + "/settings.txt"; //this sessions end settings
        string sfileNameKey = CoreFileNameThis + "/settingsKey.txt"; //this sessions end settings
        string recentSettingsFileName = CoreFileName + "recentSettings.txt";
        string eventTimesfileName = CoreFileNameThis + "/timeofBehEvents.txt";
        string TITOfileName = CoreFileNameThis + "/TITOtimeStamps.txt";
        string depthImgfileNameDir = CoreFileNameThis + "/depthImages/";

       public int[] InitializeFileStructure()
        {
            int[] readValues = new int[10];
            Directory.CreateDirectory(CoreFileName);
            Directory.CreateDirectory(vfileName);
            Directory.CreateDirectory(depthImgfileNameDir);

            if (File.Exists(movementfileName))
            { File.Delete(movementfileName); }
            if (File.Exists(vfileName))
            {File.Delete(vfileName); }
            if (File.Exists(eventTimesfileName))
            { File.Delete(eventTimesfileName); }
            if (File.Exists(sfileName))
            { File.Delete(sfileName); }
            if (File.Exists(sfileNameKey))
            { File.Delete(sfileNameKey); }
            if (File.Exists(TITOfileName))
            {File.Delete(TITOfileName);}
            if (File.Exists(recentSettingsFileName))
            {
                using (StreamReader sr = new StreamReader(recentSettingsFileName))
                {
                    string thisLine;
                    int index = 0;
                    while ((thisLine = sr.ReadLine()) != null) {
                        readValues[index] = Convert.ToInt16(thisLine);
                        index++;

                    }
                }
                
            }
            else
            {

                Console.WriteLine("No recent settings");
            }

           /*
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(movementfileName, true))
            { file.WriteLine("Time,Movement");  }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(eventTimesfileName, true))
            { file.WriteLine("Time,EventType"); }
           */

            return readValues;

        }
       public string getCoreFileName()
       {
           return CoreFileNameThis;
       }

       public string getDepthImgFolderName()
       {
           return depthImgfileNameDir;
       }
       public void SaveMovementData(double time, double moveVal)
       {
           using (System.IO.StreamWriter file = new System.IO.StreamWriter(movementfileName, true))
           {
               file.WriteLine(time.ToString() + "," + moveVal.ToString());

           }


       }
       public void SaveMovementData(double time, double[] moveVal)
       {
           using (System.IO.StreamWriter file = new System.IO.StreamWriter(movementfileName, true))
           {
               string fullString = time.ToString() + ",";
               for (int ii = 0; ii < moveVal.Length; ii++)
               {
                   fullString = fullString + moveVal[ii].ToString() + ",";
               }
               file.WriteLine(fullString);

           }


       }
       public void SaveMovementData(double time, int[] moveVal)
       {
           using (System.IO.StreamWriter file = new System.IO.StreamWriter(movementfileName, true))
           {
               string fullString = time.ToString() + ",";
               for (int ii = 0; ii < moveVal.Length; ii++)
               {
                   fullString = fullString + moveVal[ii].ToString() + ",";
               }
               file.WriteLine(fullString);

           }


       }
       public void SaveEventData(double time, string eventType)
       {

           using (System.IO.StreamWriter file = new System.IO.StreamWriter(eventTimesfileName, true))
           { file.WriteLine(time.ToString() + "," + eventType); }

       }

       public string getVideoFileName() { return vfileName; }
       public string getThisCoreFileName() { return CoreFileNameThis; }
       public string getMovementFileName() { return movementfileName; }


       public void saveSettings(int[] settings)
       {

           if (File.Exists(sfileName))
           {
               File.Delete(sfileName);
           }
           if (File.Exists(recentSettingsFileName))
           {
               File.Delete(recentSettingsFileName);
           }

           using (System.IO.StreamWriter file = new System.IO.StreamWriter(recentSettingsFileName, true))
           {
               for (int ii = 0; ii < settings.Length; ii++)
               {
                   file.WriteLine(settings[ii]);
               }
           }

           using (System.IO.StreamWriter file = new System.IO.StreamWriter(sfileName, true))
           {
               for (int ii = 0; ii < settings.Length; ii++)
               {
                   file.WriteLine(settings[ii]);
               }
           }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(sfileNameKey, true))
           {
                file.WriteLine("quadMarginXL");
                file.WriteLine("quadMarginXR");
                file.WriteLine("quadMarginYT");
                file.WriteLine("quadMarginYB");
                file.WriteLine("loDepthThreshold");
                file.WriteLine("hiDepthThreshold");
           }

           

       }
    }
}
