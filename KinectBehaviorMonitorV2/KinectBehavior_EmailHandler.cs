using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectBehaviorMonitorV2
{
    class KinectBehavior_EmailHandler
    {
        KinectBehavior_FileHandler fileHandler;
        //email parameters
        int emailUpdateFrequency = 15 * 60; // in seconds
        int emailCounter = 0; // how many emails have been sent
        double lastEmail = 0;
        bool sendData = false;
        public KinectBehavior_EmailHandler(KinectBehavior_FileHandler fh)
        {
            fileHandler = fh;
        }

        public void CheckEmailSend(double curTime,int events)
        {
            if (curTime > emailUpdateFrequency && lastEmail < curTime - emailUpdateFrequency)
            {
                sendEmailUpdate(curTime,events);
                lastEmail = curTime;
            }

        }

        private void sendEmailUpdate(double curTime, int events)
        {
            //send Counter, timeElapsed, video?
            try
            {
                emailCounter++;
                System.Net.NetworkCredential cred = new System.Net.NetworkCredential("tylerplab@gmail.com", "*venta*venta");
                System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
                message.To.Add("tylerplab@gmail.com");
                message.To.Add("tlibey1@gmail.com");
                //message.To.Add("zroberts318@gmail.com");
                message.From = new System.Net.Mail.MailAddress("tylerplab@gmail.com");
                message.Subject = "Update" + DateTime.Today.Date + emailCounter.ToString();
                message.Body = "Number of Events Completed: " + events.ToString() + "\n" +
                               "Time Elapsed: " + curTime.ToString() + "\n";
                
                System.Net.Mail.Attachment data = null;
                if(sendData){
                 data = new System.Net.Mail.Attachment(fileHandler.getMovementFileName());
                 message.Attachments.Add(data);}
                
                System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient("smtp.gmail.com");
                client.Credentials = cred;
                client.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                client.EnableSsl = true;
                client.Port = 587;
                client.Send(message);
                if (sendData && data !=null) { data.Dispose(); }
                

            }
            catch
            {
                //no email
            }
        }

    }
}
