using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectBehaviorMonitorV2
{
    /// <summary>
    /// Hands the email functionality of the behavior monitor
    /// emails are send periodically based on emailUpdateFrequency
    /// needs SMTP enable email client (gmail should work) and that account's password
    /// CheckEmailSend(double,int) is called each time the GMV is calculated in the MainWindow.cs script
    /// </summary>
    class KinectBehavior_EmailHandler
    {
        KinectBehavior_FileHandler fileHandler;
        int emailUpdateFrequency = 15 * 60; // in seconds
        int emailCounter = 0; // how many emails have been sent
        double lastEmail = 0; //time last email was sent
        bool sendData = false; //whether or not to send additional data with the email

        string sender = ""; //SMTP enabled email account
        string password = ""; //that accounts password

        string recipient = ""; //who are you sending the email to (can be same as sender)

        public KinectBehavior_EmailHandler(KinectBehavior_FileHandler fh)
        {
            fileHandler = fh;
        }

        //called from mainwindow.xaml.cs each time the GMV is calculated
        //checks to see whether an email should be sent
        public void CheckEmailSend(double curTime,int events)
        {
            if (curTime > emailUpdateFrequency && lastEmail < curTime - emailUpdateFrequency)
            {
                sendEmailUpdate(curTime,events);
                lastEmail = curTime;
            }

        }

        //called within CheckEmailSend, sends email with the appropriate data
        private void sendEmailUpdate(double curTime, int events)
        {
            //send Counter, timeElapsed, video?
            
            try
            {
                emailCounter++;
                System.Net.NetworkCredential cred = new System.Net.NetworkCredential(sender, password);
                System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
                message.To.Add(recipient); //can add multiple recipients by duplicating message.To.Add(recipient) line
                message.From = new System.Net.Mail.MailAddress(sender);
                message.Subject = "Update" + DateTime.Today.Date + emailCounter.ToString();
                message.Body = "Number of Events Completed: " + events.ToString() + "\n" +
                               "Time Elapsed: " + curTime.ToString() + "\n";
                
                //can attach data to the email (such as the movement value file demonstrated below)
                System.Net.Mail.Attachment data = null;
                if(sendData){
                 data = new System.Net.Mail.Attachment(fileHandler.getMovementFileName());
                 message.Attachments.Add(data);}
                
                //Change the client if not using gmail
                System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient("smtp.gmail.com");
                client.Credentials = cred;
                client.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                client.EnableSsl = true;
                client.Port = 587;

                //send the email
                client.Send(message);
                if (sendData && data !=null) { data.Dispose(); }
                

            }
            catch
            {
                Console.WriteLine("unable to send email from " + sender);
                if (sender == "")
                {
                    Console.WriteLine("no sender provided for email");

                }
            }
        }

    }
}
