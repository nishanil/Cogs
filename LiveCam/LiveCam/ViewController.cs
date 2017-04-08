using System;
using System.Linq;

using UIKit;
using AVFoundation;
using CoreAnimation;
using Foundation;
using CoreMedia;
using CoreVideo;
using CoreFoundation;
using CoreImage;
using CoreGraphics;
using ServiceHelpers;
using System.Threading.Tasks;
using System.IO;
using Microsoft.ProjectOxford.Face;

namespace LiveCam
{
	public partial class ViewController : UIViewController
	{
		readonly DispatchQueue sessionQueue = new DispatchQueue("com.nnish.livecamqueue");
		static AVCaptureSession captureSession = new AVCaptureSession();
		CALayer previewLayer;
		AVCaptureDevice captureDevice;

		static bool isFaceRegistered = false;
		public ViewController(IntPtr handle) : base(handle)
		{
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();



			// Perform any additional setup after loading the view, typically from a nib.
		}
		static string personGroupId;
		async Task RegisterFaces()
		{
			var faceServiceClient = new FaceServiceClient("b1843365b41247538cffb304d36609b3");

			// Step 1 - Create Face List
			personGroupId = Guid.NewGuid().ToString();
			await faceServiceClient.CreatePersonGroupAsync(personGroupId, "Xamarin");


			var p = await faceServiceClient.CreatePersonAsync(personGroupId, "Nish Anil");
			await faceServiceClient.AddPersonFaceAsync
								   (personGroupId, p.PersonId, "https://raw.githubusercontent.com/nishanil/Mods2016/master/Slides/nish-test.jpg");


			// Step 3 - Train face group
			await faceServiceClient.TrainPersonGroupAsync(personGroupId);
			isFaceRegistered = true;


			//await DetectFace(
			//	new UIImage(
			//		NSData.FromUrl(
			//			new NSUrl("https://raw.githubusercontent.com/nishanil/Mods2016/master/Slides/nish-test.jpg"))).ResizeImageWithAspectRatio(300,400));
		}


		public async override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);
			await RegisterFaces();
			
			PrepareCamera();
		}
		public override void ViewDidDisappear(bool animated)
		{
			base.ViewDidDisappear(animated);

			EndSession();
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
			NSError error=null;
			var deviceInput = new AVCaptureDeviceInput(captureDevice, out error);
			if (error == null && captureSession.CanAddInput(deviceInput))
				captureSession.AddInput(deviceInput);
			previewLayer = new AVCaptureVideoPreviewLayer(captureSession)
			{
				VideoGravity = AVLayerVideoGravity.ResizeAspect
			};
			//this.HomeView.BackgroundColor = UIColor.Black;
			previewLayer.Frame = this.HomeView.Layer.Frame;

			this.HomeView.Layer.AddSublayer(previewLayer);



			captureDevice.LockForConfiguration(out error);
			if (error != null)
			{
				Console.WriteLine(error);
				captureDevice.UnlockForConfiguration();
				return;
			}

			if (UIDevice.CurrentDevice.CheckSystemVersion(7, 0))
				captureDevice.ActiveVideoMinFrameDuration = new CMTime(1, 15);
			captureDevice.UnlockForConfiguration();

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

			var OutputSampleDelegate = new OutputSampleDelegate { Navigation = NavigationController };
			videoOut.SetSampleBufferDelegateQueue(OutputSampleDelegate, sessionQueue);
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



		static async Task DetectFace(UIImage image)
		{
			FaceServiceClient faceServiceClient = new FaceServiceClient("b1843365b41247538cffb304d36609b3");

			var faces = await faceServiceClient.DetectAsync(image.AsPNG().AsStream());
			if (faces.Any())
			{
				var faceIds = faces.Select(face => face.FaceId).ToArray();

				var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);

				if (results.Any())
				{
					var result = results[0].Candidates[0].PersonId;

					var person = await faceServiceClient.GetPersonAsync(personGroupId, result);

					Console.Write(person.Name);
				}
			}
		}




		public class OutputSampleDelegate : AVCaptureVideoDataOutputSampleBufferDelegate
		{

			public UINavigationController Navigation
			{
				get;
				set;
			}
			bool isProcessing = false;


			public async override void DidOutputSampleBuffer(AVCaptureOutput captureOutput,
													   CMSampleBuffer sampleBuffer,
													   AVCaptureConnection connection)
			{

				if (!isFaceRegistered)
					return;

				if (isProcessing)
					return;
				connection.VideoOrientation = AVCaptureVideoOrientation.Portrait;
				var image = GetImageFromSampleBuffer(sampleBuffer);

				//DispatchQueue.MainQueue.DispatchAsync(() =>
				//{

				//	var SecondViewController = Navigation?.Storyboard.InstantiateViewController("SecondViewController") as SecondViewController;

				//	SecondViewController.Image = image;
				//	Navigation?.PushViewController(SecondViewController, true);

				//	captureSession.StopRunning();

				//});
				await Task.Run(async () =>
				{
					try
					{
						isProcessing = true;
						await DetectFace(image);
					}
					catch (Exception ex)
					{
						Console.Write(ex);
					}
					finally
					{
						isProcessing = false;

					}

				});

				//ImageAnalyzer imageWithFace = new ImageAnalyzer(() => Task.FromResult<Stream>(
				//														 image.AsPNG().AsStream()));


				//Task.Run(async () => await ProcessFaceDetectionCapture(imageWithFace))
				//    .ContinueWith((x) => isProcessing = false);


			}



			//private async Task ProcessFaceDetectionCapture(ImageAnalyzer e)
			//{

			//	DateTime start = DateTime.Now;

			//	await e.DetectFacesAsync();

			//	TimeSpan latency = DateTime.Now - start;



			//}


			//private UIImage GetImageFromSampleBuffer(CMSampleBuffer sampleBuffer)
			//{
			//	using(var buffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer)
			//	{
			//		var ciImage = new CIImage(buffer);
			//		var ciContext = CIContext.FromOptions(null);
			//		var imageRect = new CGRect(0, 0, buffer.Width, buffer.Height);
			//		var cgImage = ciContext.CreateCGImage(ciImage, imageRect);
			//		return UIImage.FromImage (cgImage);

			//	}
			//	return null;
			//}

			UIImage GetImageFromSampleBuffer(CMSampleBuffer sampleBuffer)
			{
				// Get the CoreVideo image
				using (var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer)
				{
					// Lock the base address
					pixelBuffer.Lock(CVPixelBufferLock.None);
					// Get the number of bytes per row for the pixel buffer
					var baseAddress = pixelBuffer.BaseAddress;
					var bytesPerRow = (int)pixelBuffer.BytesPerRow;
					var width = (int)pixelBuffer.Width;

					var height = (int)pixelBuffer.Height;
					var flags = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;
					// Create a CGImage on the RGB colorspace from the configured parameter above
					using (var cs = CGColorSpace.CreateDeviceRGB())
					{
						using (var context = new CGBitmapContext(baseAddress, width, height, 8, bytesPerRow, cs, (CGImageAlphaInfo)flags))
						{
							using (CGImage cgImage = context.ToImage())
							{
								pixelBuffer.Unlock(CVPixelBufferLock.None);

								return UIImage.FromImage(cgImage).ResizeImageWithAspectRatio(300,400);
							}
						}
					}
				}
			}
		}

	}
}