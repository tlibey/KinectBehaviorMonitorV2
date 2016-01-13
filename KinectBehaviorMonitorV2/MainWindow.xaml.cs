using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.IO.Ports;
using Microsoft.Kinect;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.UI;
using Emgu.CV.Structure;
using System.IO;
using ImageManipulationExtensionMethods;

/*TODO: 
 *  
 * 
 * */

//Program Summary:
/*
 *Using the kinect 2.0, work in progress. 
 Video saving slower in kinect 2.0 due to higher resolution
    *super messy -- need to clean
 * snapshot saving in pictures?
 * needs better motion measure
 * 
 * 
 * 
 */


namespace KinectBehaviorMonitorV2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Member Variables


        //colorVar
        private readonly uint bytesPerPixel = 0;
      
        private KinectSensor kinectSensor = null;
        private ColorFrameReader colorFrameReader = null;
        private WriteableBitmap colorBitmap = null;
        private byte[] colorPixels = null;

        private DepthFrameReader depthFrameReader = null;
        private WriteableBitmap depthBitmap = null;
        private byte[]  depthPixels = null;
        private FrameDescription depthFrameDescription = null;


        List<Image<Rgb, Byte>> _videoArray = new List<Image<Rgb, Byte>>(); //video file initializer

        int currentFrame = 0;
        static int[] depthOld = { 0 };
        static int[] iLeftPosOld = null;
        static int[] iTopPosOld = null;
        static int[] iRightPosOld = null;
        static int[] iDepthMinOld = null;

        // initial values defined by slider widths
        int quadMarginXL = 20;
        int quadMarginXR = 40;
        int quadMarginYT = 20;
        int quadMarginYB = 40;//
        int loDepthThreshold = 1000;
        int hiDepthThreshold = 1548;

        bool saveDepthPicture = true; 
        int frameAccDepthSave = 32 * 2;
        int currentFrameDepthSave = 0;
        bool rgbDepthMode = true;
        DateTime lastFrame;

        //logistics and file names
        static DateTime timeStart;
        KinectBehavior_FileHandler fileHandler = new KinectBehavior_FileHandler();
        KinectBehavior_EmailHandler emailHandler;// = new KinectBehavior_EmailHandler(fileHandler);
        KinectBehavior_PortHandler portHandler;

        //starter values
       
        int timeOutFrameCount = 0; // initializes framecounter 
       
        private int counter = 0; //successful event counter, displayed in top right corner and saved at the end of videos (event{0}.avi)

        //Things to play with
        int videoRecordLength = 32 * 8;  //framerate*seconds to record (half before event and half after event)
        int frameAcceptance = 4; //x of n frames used in depth calculations
        bool savingVideo = false;
      
       

        

        #endregion Member Variables

        public MainWindow() //main window constructor
        {
            //Start time here since its the first call
            timeStart = DateTime.Now;
            lastFrame = timeStart;
            emailHandler = new KinectBehavior_EmailHandler(fileHandler);
            portHandler = new KinectBehavior_PortHandler(fileHandler);
            int[] loadedSettings = fileHandler.InitializeFileStructure();
         
            Console.WriteLine("Initialized");
            
                    this.kinectSensor = KinectSensor.GetDefault();
                    this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();
                    this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

                    FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
                    this.bytesPerPixel = colorFrameDescription.BytesPerPixel;
                    this.colorPixels = new byte[colorFrameDescription.Width * colorFrameDescription.Height * this.bytesPerPixel];
                    this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

                    this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();
                    this.depthFrameReader.FrameArrived += this.Reader_DepthFrameArrived;

                    depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
                  this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
                   this.depthBitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
                   
                    this.DataContext = this;

                    this.kinectSensor.Open();

           

            InitializeComponent();
            if (loadedSettings != null)
            {
                quadMarginXL = loadedSettings[0];
                quadMarginXR = loadedSettings[1];
                quadMarginYT = loadedSettings[2];
                quadMarginYB = loadedSettings[3];
                Console.WriteLine(loadedSettings[0]);
                loDepthThreshold = loadedSettings[4];
                hiDepthThreshold = loadedSettings[5];
                xQuadMarginSliderR.Value = quadMarginXR;
                xQuadMarginSliderL.Value = quadMarginXL;
                yQuadMarginSliderT.Value = quadMarginYT;
                yQuadMarginSliderB.Value = quadMarginYB;
                loDepthSlider.Value = loDepthThreshold;
                hiDepthSlider.Value = hiDepthThreshold;
            }
            savingVideo = portHandler.checkSerialInput(DateTime.Now.Subtract(timeStart).TotalSeconds); //check portData to see if arduino has triggered an event

        }
       
        
          private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }
            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }
            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }
        
        private void Reader_DepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            bool depthFrameProcessed = false;
            
            // ColorFrame is IDisposable
            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, depthFrame.DepthMaxReliableDistance);
                            depthFrameProcessed = true;
                        }
                    }
                }
            }

            // we got a frame, render
            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }
        }

        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            //only grab depth values if in low resource??

            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;
            byte[] enhPixelData = new byte[depthFrameDescription.Width * depthFrameDescription.Height * 4];
            int[] depth = new int[depthFrameDescription.Width * depthFrameDescription.Height * bytesPerPixel];

            // convert depth to a visual representation
            for (int i = 0, j =0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i, j+=4)
            {
                // Get the depth for this pixel
                 depth[i] = (int)frameData[i];

                // To convert to a byte, we're discarding the most-significant
                // rather than least-significant bits.
                // We're preserving detail, although the intensity will "wrap."
                // Values outside the reliable depth range are mapped to 0 (black).
                 this.depthPixels[i] = (byte)(depth[i] >= minDepth && depth[i] <= maxDepth ? depth[i] : 0);
               
                int gray;
                if (depth[i] < loDepthThreshold || depth[i] > hiDepthThreshold)
                {
                    gray = 0xFF;
                    depth[i] = 0;
                }
                else
                {
                    if (rgbDepthMode) { 
                    int dist = hiDepthThreshold - loDepthThreshold;
                    int adjDepth = depth[i] - loDepthThreshold;
                    double remappedDepth = adjDepth / (double)dist * (255 * 3);
                    if (remappedDepth > 255 * 2)
                    {
                        enhPixelData[j] = 0;
                        enhPixelData[j + 1] = 0;
                        enhPixelData[j + 2] = (byte)(remappedDepth - (255 * 2));
                    }
                    else if (remappedDepth > 255)
                    {
                        enhPixelData[j] = 0;
                        enhPixelData[j + 1] = (byte)(remappedDepth - (255));
                        enhPixelData[j + 2] = 0;
                    }
                    else
                    {
                        enhPixelData[j] = (byte)remappedDepth;
                        enhPixelData[j + 1] = 0;
                        enhPixelData[j + 2] = 0;
                    }
                  }
                    gray = (255 * depth[i] / 0xFFF);
                }
                if (!rgbDepthMode)
                {
                    enhPixelData[j] = (byte)gray;
                    enhPixelData[j + 1] = (byte)gray;
                    enhPixelData[j + 2] = (byte)gray;
                }
            }


            // draw margins
            for (int iiy = 0; iiy < depthFrameDescription.Height; iiy++)
                for (int iix = 0; iix < depthFrameDescription.Width; iix++)
                {
                    if (iix == quadMarginXR || iiy == quadMarginYB)
                    {

                        enhPixelData[(iix + iiy * depthFrameDescription.Width) * 4] = 0;
                        enhPixelData[(iix + iiy * depthFrameDescription.Width) * 4 + 1] = 0;
                        enhPixelData[(iix + iiy * depthFrameDescription.Width) * 4 + 2] = 255;

                    }
                    if (iix == quadMarginXL || iiy == quadMarginYT)
                    {

                        enhPixelData[(iix + iiy * depthFrameDescription.Width) * 4] = 255;
                        enhPixelData[(iix + iiy * depthFrameDescription.Width) * 4 + 1] = 0;
                        enhPixelData[(iix + iiy * depthFrameDescription.Width) * 4+ 2] = 255;

                    }
                }

            DepthImageModified.Source = BitmapSource.Create(depthFrameDescription.Width, depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null, enhPixelData, depthFrameDescription.Width * 4);

            TimeSpan elapsed = DateTime.Now.Subtract(timeStart);
            if (currentFrame % frameAcceptance == 0)
            {
                calculateMovement(depth, depthFrameDescription.Width, depthFrameDescription.Height);
                currentFrame = 0;
            }
            if (currentFrameDepthSave % frameAccDepthSave == 0 && saveDepthPicture)
            {
                calculateMovement(depth, depthFrameDescription.Width, depthFrameDescription.Height);
                string depthfilename = fileHandler.getDepthImgFolderName() + "depthImg_" + ((int)elapsed.TotalSeconds).ToString() + ".jpg";
                Console.WriteLine("saving img");
                using (FileStream savedBitmap = new FileStream(depthfilename, FileMode.CreateNew))
                {
                    BitmapSource img = (BitmapSource)(DepthImageModified.Source);
                    JpegBitmapEncoder jpgEncoder = new JpegBitmapEncoder();
                    jpgEncoder.QualityLevel = 70;
                    jpgEncoder.Frames.Add(BitmapFrame.Create(img));
                    jpgEncoder.Save(savedBitmap);
                    savedBitmap.Flush();
                    savedBitmap.Close();
                    savedBitmap.Dispose();
                }
                currentFrameDepthSave = 0;
            }
            currentFrameDepthSave++;
            currentFrame++;

            if (savingVideo) //placed here to ensure that half of the recorded time is after the event (time outPause defined as true when event happens
            {
                timeOutFrameCount++;
                if (timeOutFrameCount >= videoRecordLength / 2)
                {
                    timeOutFrameCount = 0;
                    savingVideo = false;
                    SaveVideo(false);
                }
            }
        }
        private void RenderDepthPixels()
        {
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);
        }

         private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            bool colorFrameProcessed = false;
           
            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    // verify data and write the new color frame data to the display bitmap
                    if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                    {
                        if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
                        {
                            colorFrame.CopyRawFrameDataToArray(this.colorPixels);
                        }
                        else
                        {
                            colorFrame.CopyConvertedFrameDataToArray(this.colorPixels, ColorImageFormat.Bgra);
                        }

                        colorFrameProcessed = true;
                    }
                    
                    if (currentFrame % frameAcceptance == 0) //set to only add every frameAcceptanceth'd frame
                        _videoArray.Add(colorFrame.ToOpenCVImage<Rgb, Byte>());
                    if (_videoArray.Count() > videoRecordLength / frameAcceptance) // Frame limiter (ideally 4x where x is length of event)
                        _videoArray.RemoveAt(0);
                }
            }

            // we got a frame, render
            if (colorFrameProcessed)
            {
                this.RenderColorPixels();
            }
        }
          private void RenderColorPixels()
        {
            this.colorBitmap.WritePixels(
                new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                this.colorPixels,
                this.colorBitmap.PixelWidth * (int)this.bytesPerPixel,
                0);
        }

         public ImageSource ColorSource
        {
            get
            {
                return this.colorBitmap;
            }
        }

         public ImageSource DepthSource
         {
             get
             {
                 return this.depthBitmap;
             }
         }

      

        private void calculateMovement(int[] depth, int fwidth, int fheight)
        {

            int quadrantDiv = 20;
            int numberofQuadrants = quadrantDiv * quadrantDiv;
            int[] iLeftPos = new int[numberofQuadrants];
            int[] iTopPos = new int[numberofQuadrants];
            int[] iRightPos = new int[numberofQuadrants];
            int[] iDepthMin = new int[numberofQuadrants];
            int[] allMovementValue = new int[numberofQuadrants];

            for (int quadY = 0; quadY < quadrantDiv; quadY++)
            {
                for (int quadX = 0; quadX < quadrantDiv; quadX++)
                {
                    //calculate leftmost and rightmost depths
                    for (int iiy = (quadY * (quadMarginYB - quadMarginYT) / quadrantDiv + quadMarginYT); iiy < (quadY * (quadMarginYB - quadMarginYT) / quadrantDiv + (quadMarginYB - quadMarginYT) / quadrantDiv + quadMarginYT); iiy++)
                    {
                        for (int iii = (quadX * (quadMarginXR - quadMarginXL) / quadrantDiv + quadMarginXL); iii < (quadX * (quadMarginXR - quadMarginXL) / quadrantDiv + (quadMarginXR - quadMarginXL) / quadrantDiv + quadMarginXL); iii++)
                        {
                            int depthIndex = iii + iiy * fwidth;
                            int quadDex = quadX + quadY * quadrantDiv;
                            if (depth[depthIndex] != 0)
                            {

                                if (iii > iLeftPos[quadDex])
                                    iLeftPos[quadDex] = iii;
                                if (iTopPos[quadDex] == 0)
                                    iTopPos[quadDex] = iiy;
                                if (depth[depthIndex] < iDepthMin[quadDex])
                                    iDepthMin[quadDex] = depth[depthIndex];
                              
                                break;
                            }
                        }
                        //calculate rightmost points
                        for (int iii = (quadX * (quadMarginXR - quadMarginXL) / quadrantDiv + (quadMarginXR - quadMarginXL) / quadrantDiv + quadMarginXL); iii > (quadX * (quadMarginXR - quadMarginXL) / quadrantDiv + quadMarginXL); iii--)
                        {
                            if (depth[iii + iiy * fwidth] != 0)
                            {

                                if (iii > iRightPos[quadX + quadY * quadrantDiv])
                                    iRightPos[quadX + quadY * quadrantDiv] = iii;
                                break;
                            }
                        }
                    }
                }
            }
            if (iLeftPosOld == null) // initializer when no previous depth value exists
            {
                iLeftPosOld = iLeftPos;
                iTopPosOld = iTopPos;
                iRightPosOld = iRightPos;
                iDepthMinOld = iDepthMin;

                
            }

            int iMovementValue = 0;
            for (int quadY = 0; quadY < quadrantDiv; quadY++)
            {
                for (int quadX = 0; quadX < quadrantDiv; quadX++)
                {
                    int quadDex = quadX + quadY * quadrantDiv;
                    int leftDiff = 0;
                    int rightDiff = 0;
                    int topDiff = 0;
                    int depthDiff = 0;

                    //dont want to subtract zero values from old or new
                    if (iLeftPos[quadDex] != 0 && iLeftPosOld[quadDex] != 0)
                        leftDiff = Math.Abs(iLeftPos[quadDex] - iLeftPosOld[quadDex]);

                    if (iRightPos[quadDex] != 0 && iRightPosOld[quadDex] != 0)
                        rightDiff = Math.Abs(iRightPos[quadDex] - iRightPosOld[quadDex]);

                    if (iTopPos[quadDex] != 0 && iTopPosOld[quadDex] != 0)
                        topDiff = Math.Abs(iTopPos[quadDex] - iTopPosOld[quadDex]);

                    if (iDepthMin[quadDex] != 0 && iDepthMinOld[quadDex] != 0)
                        depthDiff = Math.Abs(iDepthMin[quadDex] - iDepthMinOld[quadDex]);
                    // end section

                    if (leftDiff < 100 && rightDiff < 100 && topDiff < 100 && depthDiff < 100)
                        allMovementValue[quadDex] = leftDiff + rightDiff + topDiff + depthDiff;

                    iMovementValue += allMovementValue[quadDex]; //calculate final movement value
                }
            }

            this.DepthDiff.Dispatcher.BeginInvoke(new Action(() =>
            {
                DepthDiff.Text = string.Format("{0} mm", iMovementValue);
            }));

            
 
            //assign old values for next frame comparison
            depthOld = depth;
            iLeftPosOld = iLeftPos;
            iRightPosOld = iRightPos;
            iTopPosOld = iTopPos;
            iDepthMinOld = iDepthMin;



            TimeSpan elapsed = DateTime.Now.Subtract(timeStart);
            this.TimeElapsed.Dispatcher.BeginInvoke(new Action(() =>
            {
                TimeElapsed.Text = string.Format("{0} s", elapsed.TotalSeconds);
            }));

            double currentTimeElapsed = Math.Floor(elapsed.TotalSeconds);
            emailHandler.CheckEmailSend(currentTimeElapsed,counter);
            fileHandler.SaveMovementData(elapsed.TotalMilliseconds, allMovementValue);



            if (!savingVideo) //timeoutpause is lockout from event for video saving, 
            {

                bool KinectFeederTrigger = false; 
 
                //define event to count
                if (KinectFeederTrigger) //
                {
                    counter++;
                    fileHandler.SaveEventData(elapsed.TotalMilliseconds, "TIEvent");

                    this.TimeElapsed.Dispatcher.BeginInvoke(new Action(() =>
                    {Counter.Text = string.Format("{0} Events", counter);}));
                    savingVideo = true;
                    portHandler.sendSerialTreat();
                    
                }
            }
        }

        
        private void SaveVideo(bool fromTestButton)
        {

            string vEventfileName = fileHandler.getVideoFileName() + "event" + (fromTestButton?"test":"")+(counter).ToString() + ".avi"; //
            using (VideoWriter vw = new VideoWriter(vEventfileName, 0, 32 / frameAcceptance, 640, 480, true))
            {
                for (int i = 0; i < _videoArray.Count(); i++)
                {
                    vw.WriteFrame<Emgu.CV.Structure.Rgb, Byte>(_videoArray[i]);
                }
            }
        }

       

       

       


        //slider controls
        private void xMarginRight_ValChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            quadMarginXR = (int)xQuadMarginSliderR.Value;
            xMarginRightDisp.Text = quadMarginXR.ToString();
        }
        private void xMarginLeft_ValChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            quadMarginXL = (int)xQuadMarginSliderL.Value;
            xMarginLeftDisp.Text = quadMarginXL.ToString();
        }
        private void yMarginTop_ValChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            quadMarginYT = (int)yQuadMarginSliderT.Value;
            yMarginTopDisp.Text = quadMarginYT.ToString();
        }
        private void yMarginBottom_ValChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            quadMarginYB = (int)yQuadMarginSliderB.Value;
            yMarginBottomDisp.Text = quadMarginYB.ToString();
        }

       
        private void loDepthSlider_ValChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            loDepthThreshold = (int)loDepthSlider.Value;
            loDepthDisp.Text = string.Format("{0} mm", loDepthThreshold);
        }
        private void hiDepthSlider_ValChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            hiDepthThreshold = (int)hiDepthSlider.Value;
            hiDepthDisp.Text = string.Format("{0} mm", hiDepthThreshold);
        }
      

        public void SaveSettingsButton(object sender, EventArgs a)
        {
            int[] settings = new int[]{quadMarginXL, quadMarginXR, quadMarginYT, quadMarginYB, loDepthThreshold, hiDepthThreshold};
            fileHandler.saveSettings(settings);
        }


       // #endregion Properties

        private void FreeTreatFeederTest(object sender, EventArgs a)
        {
            portHandler.sendSerialTreat();

        }

        private void saveVideoTest(object sender, EventArgs a)
        {
            SaveVideo(true);

        }



    }
}
