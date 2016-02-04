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

/// <summary>
/// Kinect behavior monitor for use with Kinect2.0 and Arduino Control hub created by Tyler Libey in the lab of Eberhard Fetz at the University of Washington
/// 
/// Collects depth and color data from kinect
/// Displays color data to UI window
/// Displays modified depth data to UI window
/// Calculates GMV based on depth data
/// Saves GMV to file
/// Receives events from Arduino Control Hub
/// Saves video surrounding event to file
/// Periodically saves depth image to file
/// Sends email updates on experiment progress using emailHandler
/// </summary>
namespace KinectBehaviorMonitorV2
{
    
    public partial class MainWindow : Window
    {
        #region Member Variables
        //Kinect Variables
        private KinectSensor kinectSensor = null;
        private ColorFrameReader colorFrameReader = null;
        private WriteableBitmap colorBitmap = null;
        private byte[] colorPixels = null;
        private DepthFrameReader depthFrameReader = null;
        private byte[]  depthPixels = null;
        private FrameDescription depthFrameDescription = null;
        private readonly uint bytesPerPixel = 0;

        //Can save videos after event triggers And/Or depth images at a set frequency depending on the application
        private bool saveDepthPicture = true;
        private bool saveVideoOnEvent = true;

        //Video Saving variables
        private List<Image<Rgb, Byte>> _videoArray = new List<Image<Rgb, Byte>>(); //Collection of Images that maintains buffer for saving video on event trigger
        private int videoRecordLength = 32 * 8;  //framerate*seconds to record (half before event and half after event)
        private bool savingVideo = false;

        private int timeOutFrameCount = 0; // triggered to zero and ++ when saving video => allows buffer to fill 1/2*videoRecordLength after the trigger occurs

        //Depth Picture saving variables
        private int frameAccDepthSave = 32 * 2;
        private int currentFrameDepthSave = 0;
        private bool rgbDepthMode = true; //Modifies depth stream display to show rgb depth scale instead of cycled grey scale


        //GMV Calculation variables
        private int frameAcceptance = 4; //every "frameAcceptance" frame will be used in depth calculations and video saving, {This should be decreased on fast machines for more accurate values}
        private int currentFrame = 0; //used to track frameAcceptance trigger
        private int quadrantDiv = 20; //how many quadrants to divide into for the Complex GMV
        private int noiseCutOff = 100; //if any given quadrant has a move value > noiseCutoff => it will not be used in the complex GMV calculation

        //history values for calculating difference between frames
        private static int[] iLeftPosOld = null; 
        private static int[] iRightPosOld = null;
        private static int[] iTopPosOld = null;
        private static int[] iBotPosOld = null;
        private static int[] iDepthMinOld = null;
        private static int[] iDepthMaxOld = null;

        // initial values defined by slider widths (Only pixels within the margins will be used for GMV)
        int quadMarginXL = 20;
        int quadMarginXR = 40;
        int quadMarginYT = 20;
        int quadMarginYB = 40;//
        int loDepthThreshold = 1000;
        int hiDepthThreshold = 1548;

        //logistics
        private static DateTime timeStart; //used for timers
        private KinectBehavior_FileHandler fileHandler = new KinectBehavior_FileHandler(); //handles file/data saving
        private KinectBehavior_EmailHandler emailHandler; //Handles email updates
        private KinectBehavior_PortHandler portHandler; //Handles communication between kinect and arduino control hub
        private int counter = 0; //successful event counter, displayed in top right corner and saved at the end of videos (event{0}.avi)

        #endregion Member Variables

        public MainWindow() //main window constructor
        {
            
            timeStart = DateTime.Now; //Start time here

            //start kinect, initialize all the variables, streams, and event handlers 
            #region initialize Kinect 
            this.kinectSensor = KinectSensor.GetDefault();
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;

            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            this.bytesPerPixel = colorFrameDescription.BytesPerPixel;//  

            this.colorPixels = new byte[colorFrameDescription.Width * colorFrameDescription.Height * this.bytesPerPixel];
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();
            this.depthFrameReader.FrameArrived += this.Reader_DepthFrameArrived;

            depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
            this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

            this.DataContext = this;

            this.kinectSensor.Open();
            #endregion

            int[] loadedSettings = fileHandler.InitializeFileStructure(); //start file handler, and load the settings for setting later
            emailHandler = new KinectBehavior_EmailHandler(fileHandler); //start email handler
            portHandler = new KinectBehavior_PortHandler(fileHandler); //start port handler (and listeners for port events)
            InitializeComponent(); //Initialize UI Elements

            if (loadedSettings != null)
            {
                initializeSettingsAndSliders(loadedSettings);
            }

        }

        private void initializeSettingsAndSliders(int[] loadedSettings)
        {
            //assign previous settings to local variables and then set the sliders to that value
            quadMarginXL = loadedSettings[0];
            quadMarginXR = loadedSettings[1];
            quadMarginYT = loadedSettings[2];
            quadMarginYB = loadedSettings[3];
            loDepthThreshold = loadedSettings[4];
            hiDepthThreshold = loadedSettings[5];
            xQuadMarginSliderR.Value = quadMarginXR;
            xQuadMarginSliderL.Value = quadMarginXL;
            yQuadMarginSliderT.Value = quadMarginYT;
            yQuadMarginSliderB.Value = quadMarginYB;
            loDepthSlider.Value = loDepthThreshold;
            hiDepthSlider.Value = hiDepthThreshold;
        }
       
       
        //event called when MainWindow.xaml/WPF closes => this is used to clean up all the kinect streams
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

        //subscribed event set during kinect initialization (called each time a color frame is available)
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

                    //update the image collection buffer with the most recent frame and remove the old frames if the buffer is full
                    if (currentFrame % frameAcceptance == 0) //set to only add every frameAcceptanceth'd frame
                        _videoArray.Add(colorFrame.ToOpenCVImage<Rgb, Byte>());
                    if (_videoArray.Count() > videoRecordLength / frameAcceptance) // Frame limiter 
                        _videoArray.RemoveAt(0);
                }
            }

            // push the color frame data to the bitmap (set to UI from ColorSource call below)
            if (colorFrameProcessed)
            {
                this.colorBitmap.WritePixels(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                               this.colorPixels, this.colorBitmap.PixelWidth * (int)this.bytesPerPixel, 0);
            }
        }

        //Source for MainWindow to render the Color frame to the UI
        public ImageSource ColorSource
        {
            get
            { return this.colorBitmap; }
        }

        //subscribed event set during kinect initialization (called each time a depth frame is available)
        private void Reader_DepthFrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            {
                if (depthFrame != null)
                {
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and begin processing the data
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) )
                        {
                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, depthFrame.DepthMaxReliableDistance);
                        }
                    }
                }
            }
        }

        //called within reader_DepthFrameArrived Event iff depth frame is valid
        //modifies depth frame display and then calls the GMV calculation function
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {

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
               
                int gray; //used to set grayScale image if in !rgbDepthMode

                //if depth is outside slider set thresholds => set to zero, else => set the color for the enhpixel data structure for creating the UI display
                if (depth[i] < loDepthThreshold || depth[i] > hiDepthThreshold)
                {
                    gray = 0xFF;
                    depth[i] = 0;
                }
                else
                {
                    //rgbDepthMode scales the pixel data's color value based on the depth thresholds
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


            // draw margins over the depth frame so that the sliders are easier to use
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
            //create the bitmap and send it to the UI
            DepthImageModified.Source = BitmapSource.Create(depthFrameDescription.Width, depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null, enhPixelData, depthFrameDescription.Width * 4);


            //Determine if we should calculate the depth value based on how many frames have passed
            TimeSpan elapsed = DateTime.Now.Subtract(timeStart);
            if (currentFrame % frameAcceptance == 0)
            {
                calculateMovement(depth, depthFrameDescription.Width, depthFrameDescription.Height);
                currentFrame = 0;
            }            

            //Determine if we should save the depth image based on how many frames have passed
            if (currentFrameDepthSave % frameAccDepthSave == 0 && saveDepthPicture)
            {
                calculateMovement(depth, depthFrameDescription.Width, depthFrameDescription.Height);
                string depthfilename = fileHandler.getDepthImgFolderName() + "depthImg_" + ((int)elapsed.TotalSeconds).ToString() + ".jpg";
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

            //increment counters for next frame
            currentFrameDepthSave++;
            currentFrame++;

            //if saving video was triggered from the kinect we need to check if it has been T/2 since the event. if so then we save the video, 
            //if not, then we still the counter for next frame
            if (savingVideo) 
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

        //called from within ProcessDepthFrameData iff the frame is the nth frame of n = frameAcceptance
        //calculates movement, checks for events from arduino, and sends email updates
        private void calculateMovement(int[] depth, int fwidth, int fheight)
        {
            #region GMVCalculation

            //breaks the area of interest into a set number of quadrands
            //calculates the leftmost, rightmost, topmost, bottommost, closest, and furthest pixels associated with the viewable object
            //adds the magnitude of these values together for each quadrant to create the GMV
            //This section can be easily adapted for more complex behavioral measurements
            int numberofQuadrants = quadrantDiv * quadrantDiv;
            int quadHeight = (quadMarginYB - quadMarginYT) / quadrantDiv;
            int quadWidth = (quadMarginXR - quadMarginXL) / quadrantDiv;

            int[] iLeftPos = new int[numberofQuadrants];
            int[] iRightPos = new int[numberofQuadrants];
            int[] iTopPos = new int[numberofQuadrants];
            int[] iBotPos = new int[numberofQuadrants];
            int[] iDepthMin = new int[numberofQuadrants];
            int[] iDepthMax = new int[numberofQuadrants];
            int[] allMovementValue = new int[numberofQuadrants];

            for (int quadY = 0; quadY < quadrantDiv; quadY++)
            {
                for (int quadX = 0; quadX < quadrantDiv; quadX++)
                {
                    
                    int yStart = (quadY * quadHeight + quadMarginYT);
                    int yMax = (quadY * quadHeight + quadHeight + quadMarginYT);
                    int quadDex = quadX + quadY * quadrantDiv;

                    for (int iiy = yStart; iiy < yMax; iiy++)
                    {
                        int xStart = (quadX * quadWidth + quadMarginXL);
                        int xMax = (quadX * quadWidth + quadWidth + quadMarginXL);
                        for (int iii = xStart; iii < xMax; iii++)
                        {
                            int depthIndex = iii + iiy * fwidth;
                            if (depth[depthIndex] != 0) //only use values that have a depth value (if above or below depth thresholds=> gets set to zero)
                            {

                                if (iii < iLeftPos[quadDex]) //if a value is "more left" than the current value then update the value
                                    iLeftPos[quadDex] = iii;
                                if (iii > iRightPos[quadDex])
                                    iRightPos[quadDex] = iii;
                                if (iTopPos[quadDex] == 0) //can just grab first value since starting at the top right corner
                                    iTopPos[quadDex] = iiy;
                                if (iiy > iBotPos[quadDex]) //if a value is "lower" than the current value then update the value
                                    iBotPos[quadDex] = iiy;
                                if (depth[depthIndex] < iDepthMin[quadDex]) //if a value is "further forward" than the current value then update the value
                                    iDepthMin[quadDex] = depth[depthIndex];
                                if (depth[depthIndex] > iDepthMax[quadDex])//if a value is "further back" than the current value then update the value
                                    iDepthMax[quadDex] = depth[depthIndex];  
                            }
                        }
                    }
                }
            }

            // initializer when no previous depth value exists
            if (iLeftPosOld == null) 
            {
                iLeftPosOld = iLeftPos;
                iRightPosOld = iRightPos;
                iTopPosOld = iTopPos;
                iBotPosOld = iBotPos;
                iDepthMinOld = iDepthMin;
                iDepthMaxOld = iDepthMax;
            }

            //calculate the difference between the old and new frame values for each quadrant and add them together
            int iMovementValue = 0;
            for (int quadY = 0; quadY < quadrantDiv; quadY++)
            {
                for (int quadX = 0; quadX < quadrantDiv; quadX++)
                {
                    int quadDex = quadX + quadY * quadrantDiv;
                    int leftDiff = 0;
                    int rightDiff = 0;
                    int topDiff = 0;
                    int botDiff = 0;
                    int depthMinDiff = 0;
                    int depthMaxDiff = 0;

                    //dont want to subtract zero values from old or new since zero value means invalid calculation (above noise threshold)
                    if (iLeftPos[quadDex] != 0 && iLeftPosOld[quadDex] != 0)
                        leftDiff = Math.Abs(iLeftPos[quadDex] - iLeftPosOld[quadDex]);

                    if (iRightPos[quadDex] != 0 && iRightPosOld[quadDex] != 0)
                        rightDiff = Math.Abs(iRightPos[quadDex] - iRightPosOld[quadDex]);

                    if (iTopPos[quadDex] != 0 && iTopPosOld[quadDex] != 0)
                        topDiff = Math.Abs(iTopPos[quadDex] - iTopPosOld[quadDex]);

                    if (iBotPos[quadDex] != 0 && iBotPosOld[quadDex] != 0)
                        botDiff = Math.Abs(iBotPos[quadDex] - iBotPosOld[quadDex]);

                    if (iDepthMin[quadDex] != 0 && iDepthMinOld[quadDex] != 0)
                        depthMinDiff = Math.Abs(iDepthMin[quadDex] - iDepthMinOld[quadDex]);

                    if (iDepthMax[quadDex] != 0 && iDepthMaxOld[quadDex] != 0)
                        depthMaxDiff = Math.Abs(iDepthMax[quadDex] - iDepthMaxOld[quadDex]);

                    if (leftDiff < noiseCutOff && rightDiff < noiseCutOff && topDiff < noiseCutOff && botDiff < noiseCutOff && depthMinDiff < noiseCutOff && depthMaxDiff < noiseCutOff)
                        allMovementValue[quadDex] = leftDiff + rightDiff + topDiff + depthMinDiff;

                    iMovementValue += allMovementValue[quadDex]; //calculate final movement value
                }
            }

            //display GMV on UI
            this.DepthDiff.Dispatcher.BeginInvoke(new Action(() =>
            {
                DepthDiff.Text = string.Format("GMV: {0}", iMovementValue);
            }));

            
 
            //assign old values for next frame comparison
            iLeftPosOld = iLeftPos;
            iRightPosOld = iRightPos;
            iTopPosOld = iTopPos;
            iBotPosOld = iBotPos;
            iDepthMinOld = iDepthMin;
            iDepthMaxOld = iDepthMax;

            #endregion

            //update UI display with elapsed time information
            TimeSpan elapsed = DateTime.Now.Subtract(timeStart);
            this.TimeElapsed.Dispatcher.BeginInvoke(new Action(() =>
            {
                int s = (int)elapsed.TotalSeconds;
                int ds = (int)(elapsed.TotalSeconds*10 % 10);
                TimeElapsed.Text = "elapsed: " + s.ToString() + "."+ds.ToString() +"s";
            }));

            //send flag to email handler to send email (will only send if enough time has passed since last email)
            double currentTimeElapsed = Math.Floor(elapsed.TotalSeconds);
            emailHandler.CheckEmailSend(currentTimeElapsed,counter);

            //save the movement data to file
            fileHandler.SaveMovementData(elapsed.TotalMilliseconds, allMovementValue);

            //check to see if there was a control hub trigger send to the portHandler, if so then increment the event counter and flip the saving video flag
            if (!savingVideo && saveVideoOnEvent) //only can occur if not already saving video
            {
                bool KinectFeederTrigger = portHandler.checkSerialInput(DateTime.Now.Subtract(timeStart).TotalSeconds); 
                if (KinectFeederTrigger) //
                {
                    counter++;
                    fileHandler.SaveEventData(elapsed.TotalMilliseconds, "TIEvent");
                    this.TimeElapsed.Dispatcher.BeginInvoke(new Action(() =>
                        {Counter.Text = string.Format("{0} Events", counter);}));
                    savingVideo = true;
                }
            }

            // This is also the place to modify the system to set up a behavior contingency on the feeder. 
            //For example -> if GMV> threshold then could trigger the feeder, or could "turn on" the control hub such that other events can allow for the feeder to trigger
            //  this could allow for experiments where movement and a neural event must both be above a threshold to train complex behaviors or movement/activity dissociation 

        }

        //Save video function called when t>trigger+T/2 OR from UI Test button, Saves ImageCollection buffer to video
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

        #region UI functions
        //UI functions for slider value changes and Buttons
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
      
        //Save settings to file (could move to valChange events for ease of use, but would cause slight slowdown)
        public void SaveSettingsButton(object sender, EventArgs a)
        {
            int[] settings = new int[]{quadMarginXL, quadMarginXR, quadMarginYT, quadMarginYB, loDepthThreshold, hiDepthThreshold};
            fileHandler.saveSettings(settings);
        }

        //Button to send a signal to the control hub to test the feeder functionality (ensures port connection is valid, AND that feeder is working)
        private void FreeTreatFeederTest(object sender, EventArgs a)
        {
            portHandler.sendSerialTreat();
        }

        //Button for saving video in a test environment
        private void saveVideoTest(object sender, EventArgs a)
        {
            SaveVideo(true);
        }

        #endregion
    }
}
