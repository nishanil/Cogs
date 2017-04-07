using System;
using System.Linq;

using UIKit;
using AVFoundation;
using CoreAnimation;
using Foundation;
using CoreMedia;
using CoreVideo;
using CoreFoundation;

namespace LiveCam
{
    public partial class ViewController : UIViewController
    {
        readonly DispatchQueue sessionQueue = new DispatchQueue("com.nnish.livecamqueue");
        readonly AVCaptureSession captureSession = new AVCaptureSession();
        CALayer previewLayer;
        AVCaptureDevice captureDevice;

        public ViewController(IntPtr handle) : base(handle)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();



            // Perform any additional setup after loading the view, typically from a nib.
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);

            PrepareCamera();
        }
        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);

            // EndSession();
        }
        private void PrepareCamera()
        {
            captureSession.SessionPreset = AVCaptureSession.PresetMedium;
            captureDevice = AVCaptureDevice.DevicesWithMediaType(AVMediaType.Video)
                            .Where(d => d.Position == AVCaptureDevicePosition.Front)
                            .FirstOrDefault() ?? AVCaptureDevice.GetDefaultDevice(AVMediaType.Video);

            

            BeginSession();
        }


        private void BeginSession()
        {
            var deviceInput = new AVCaptureDeviceInput(captureDevice, out NSError error);
            if (error == null && captureSession.CanAddInput(deviceInput))
                captureSession.AddInput(deviceInput);
            previewLayer = new AVCaptureVideoPreviewLayer(captureSession)
            {
                VideoGravity = AVLayerVideoGravity.ResizeAspect
            };
            this.HomeView.BackgroundColor = UIColor.Black;
            previewLayer.Frame = this.HomeView.Layer.Frame;

            this.HomeView.Layer.AddSublayer(previewLayer);

            captureSession.StartRunning();

            // create a VideoDataOutput and add it to the sesion
            var videoOut = new AVCaptureVideoDataOutput()
            {
                AlwaysDiscardsLateVideoFrames = true,
                WeakVideoSettings = new CVPixelBufferAttributes()
                {

                    PixelFormatType = CVPixelFormatType.CV32BGRA
                }.Dictionary
            };
            if (captureSession.CanAddOutput(videoOut))
                captureSession.AddOutput(videoOut);

            captureSession.CommitConfiguration();

            videoOut.SetSampleBufferDelegateQueue(new OutputSampleDelegate(), sessionQueue);
        }
        private void EndSession()
        {
            captureSession.StopRunning();

            foreach (var i in captureSession.Inputs)
                captureSession.RemoveInput(i);

        }

        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();
            // Release any cached data, images, etc that aren't in use.
        }

        public override bool ShouldAutorotateToInterfaceOrientation(UIInterfaceOrientation toInterfaceOrientation)
        {
            return toInterfaceOrientation == UIInterfaceOrientation.Portrait;
        }


    }

    public class OutputSampleDelegate : AVCaptureVideoDataOutputSampleBufferDelegate

    {
        public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
        {

        }
    }
}