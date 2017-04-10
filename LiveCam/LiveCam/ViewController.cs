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
using System.Threading;

namespace LiveCam
{
	public partial class ViewController : UIViewController
	{
		static DispatchQueue sessionQueue = new DispatchQueue("com.nnish.livecamqueue");
		static AVCaptureSession captureSession = new AVCaptureSession();

		static CIDetector faceDetector;

		public static bool IsFaceDetected = false;
		CALayer previewLayer;
		CALayer featureLayer = null;
		AVCaptureVideoDataOutput videoOut;

		AVCaptureDevice captureDevice;

		UIImage borderImage;

		public static bool isFaceRegistered = false;

		public bool IsUsingFrontFacingCamera
		{
			get { return captureDevice?.Position == AVCaptureDevicePosition.Front; }
		}

		public ViewController(IntPtr handle) : base(handle)
		{
		}

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();

			this.Title = "Intelligent Kiosk";
			faceDetector = CIDetector.CreateFaceDetector(CIContext.FromOptions(null), false);
			borderImage = UIImage.FromFile("square.png");

			UIDevice.CurrentDevice.BeginGeneratingDeviceOrientationNotifications();

			// Perform any additional setup after loading the view, typically from a nib.
		}


		public override bool ShouldAutorotateToInterfaceOrientation(UIInterfaceOrientation toInterfaceOrientation)
		{
			return toInterfaceOrientation == UIInterfaceOrientation.Portrait;
		}
		//static string personGroupId;
		async Task RegisterFaces()
		{

			try
			{
				var persongroupId = Guid.NewGuid().ToString();
				await FaceServiceHelper.CreatePersonGroupAsync(persongroupId,
														"Xamarin",
													 AppDelegate.WorkspaceKey);
				await FaceServiceHelper.CreatePersonAsync(persongroupId, "NISH ANIL");

				var personsInGroup = await FaceServiceHelper.GetPersonsAsync(persongroupId);

				await FaceServiceHelper.AddPersonFaceAsync(persongroupId, personsInGroup[0].PersonId,
														   "https://raw.githubusercontent.com/nishanil/Mods2016/master/Slides/nish-test.jpg", null, null);

				await FaceServiceHelper.TrainPersonGroupAsync(persongroupId);


				isFaceRegistered = true;


			}
			catch (FaceAPIException ex)

			{
				Console.WriteLine(ex.Message);
				isFaceRegistered = false;

			}

		}


		public async override void ViewWillAppear(bool animated)
		{
			base.ViewWillAppear(animated);

			//TODO: Just for POC
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
			NSError error = null;
			var deviceInput = new AVCaptureDeviceInput(captureDevice, out error);
			if (error == null && captureSession.CanAddInput(deviceInput))
				captureSession.AddInput(deviceInput);
			previewLayer = new AVCaptureVideoPreviewLayer(captureSession)
			{
				VideoGravity = AVLayerVideoGravity.ResizeAspect
			};
			//this.HomeView.BackgroundColor = UIColor.Black;
			previewLayer.Frame = this.HomeView.Layer.Bounds;

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
			videoOut = new AVCaptureVideoDataOutput()
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

			var OutputSampleDelegate = new OutputSampleDelegate(
				(s) =>
				{
					GreetingsLabel.Text = s;
				}, new Action<CIImage, CGRect>(DrawFaces));

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

		#region Draw Faces

		private void DrawFaces(CIImage image, CGRect cleanAperture)
		{
			if (image == null)
				return;

			var features = faceDetector.FeaturesInImage(image);

			if (features.Count() > 0)
				IsFaceDetected = true;

			DrawFaces(features, cleanAperture, UIDeviceOrientation.Portrait);

		}

		private CIImageOrientation GetExifOrientation(UIDeviceOrientation orientation)
		{
			CIImageOrientation exifOrientation;

			switch (orientation)
			{
				case UIDeviceOrientation.PortraitUpsideDown:
					exifOrientation = CIImageOrientation.LeftBottom;
					break;
				case UIDeviceOrientation.LandscapeLeft:
					if (IsUsingFrontFacingCamera)
						exifOrientation = CIImageOrientation.BottomRight;
					else
						exifOrientation = CIImageOrientation.TopLeft;
					break;
				case UIDeviceOrientation.LandscapeRight:
					if (IsUsingFrontFacingCamera)
						exifOrientation = CIImageOrientation.TopLeft;
					else
						exifOrientation = CIImageOrientation.BottomRight;

					break;
				case UIDeviceOrientation.Portrait:
				default:
					exifOrientation = CIImageOrientation.RightTop;
					break;

			}

			return exifOrientation;
		}

		private void DrawFaces(CIFeature[] features,
					   CGRect clearAperture,
					   UIDeviceOrientation deviceOrientation)
		{

			var pLayer = this.previewLayer as AVCaptureVideoPreviewLayer;
			var subLayers = pLayer.Sublayers;
			var subLayersCount = subLayers.Count();

			var featureCount = features.Count();

			nint currentSubLayer = 0, currentFeature = 0;
			CATransaction.Begin();
			CATransaction.DisableActions = true;
			foreach (var layer in subLayers)
			{
				if (layer.Name == "FaceLayer")
					layer.Hidden = true;

			}

			Console.WriteLine("Feature: " + featureCount);
			if (featureCount == 0)
			{
				CATransaction.Commit();
				return;
			}

			var parentFameSize = this.HomeView.Frame.Size;
			var gravity = pLayer.VideoGravity;
			var isMirrored = pLayer.Connection.VideoMirrored;
			var previewBox = VideoPreviewBoxForGravity(gravity, parentFameSize, clearAperture.Size);


			foreach (var feature in features)
			{

				// find the correct position for the square layer within the previewLayer
				// the feature box originates in the bottom left of the video frame.
				// (Bottom right if mirroring is turned on)
				CGRect faceRect = feature.Bounds;

				// flip preview width and height
				var tempCGSize = new CGSize(faceRect.Size.Height, faceRect.Size.Width);

				faceRect.Size = tempCGSize;
				faceRect.X = faceRect.Y;
				faceRect.Y = faceRect.X;

				//// scale coordinates so they fit in the preview box, which may be scaled
				var widthScaleBy = previewBox.Size.Width / clearAperture.Size.Height;
				var heightScaleBy = previewBox.Size.Height / clearAperture.Size.Width;
				var newWidth = faceRect.Size.Width * widthScaleBy;
				var newheight = faceRect.Size.Height * heightScaleBy;
				faceRect.Size = new CGSize(newWidth, newheight);
				faceRect.X *= widthScaleBy;
				faceRect.Y *= heightScaleBy;

				if (isMirrored)
					faceRect.Offset(previewBox.X + previewBox.Size.Width - faceRect.Size.Width - (faceRect.X * 2),
											 previewBox.Y);
				else
					faceRect.Offset(previewBox.X, previewBox.Y);


				while (featureLayer != null && currentSubLayer < subLayersCount)
				{
					CALayer currentLayer = subLayers[currentSubLayer++];
					if (currentLayer.Name == "FaceLayer")
					{
						featureLayer = currentLayer;
						currentLayer.Hidden = false;
					}
				}

				if (featureLayer == null)
				{
					featureLayer = new CALayer();
					featureLayer.Contents = borderImage.CGImage;
					featureLayer.Name = "FaceLayer";
					this.previewLayer.AddSublayer(featureLayer);

				}

				featureLayer.Frame = faceRect;


				switch (deviceOrientation)
				{
					case UIDeviceOrientation.Portrait:
						featureLayer.AffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(0));
						break;
					case UIDeviceOrientation.PortraitUpsideDown:
						featureLayer.AffineTransform = (CGAffineTransform.MakeRotation(DegreesToRadians(180)));
						break;
					case UIDeviceOrientation.LandscapeLeft:
						featureLayer.AffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(90));
						break;

					case UIDeviceOrientation.LandscapeRight:
						featureLayer.AffineTransform = CGAffineTransform.MakeRotation(DegreesToRadians(-90));

						break;
					case UIDeviceOrientation.FaceUp:
					case UIDeviceOrientation.FaceDown:
					default:
						break; // leave the layer in its last known orientation
				}
				currentFeature++;

			}

			CATransaction.Commit();


		}


		public nfloat DegreesToRadians(nfloat deg)
		{
			return (nfloat)(Math.PI * deg / 180.0);
		}

		private CGRect VideoPreviewBoxForGravity(AVLayerVideoGravity gravity, CGSize frameSize, CGSize apertureSize)
		{
			var apertureRatio = apertureSize.Height / apertureSize.Width;
			var viewRatio = frameSize.Width / frameSize.Height;

			CGSize size = CGSize.Empty;

			if (gravity == AVLayerVideoGravity.ResizeAspectFill)
			{
				if (viewRatio > apertureRatio)
				{
					size.Width = frameSize.Width;
					size.Height = apertureSize.Width * (frameSize.Width / apertureSize.Height);
				}
				else
				{
					size.Width = apertureSize.Height * (frameSize.Height / apertureSize.Width);
					size.Height = frameSize.Height;
				}
			}
			else if (gravity == AVLayerVideoGravity.ResizeAspect)
			{
				if (viewRatio > apertureRatio)
				{
					size.Width = apertureSize.Height * (frameSize.Height / apertureSize.Width);
					size.Height = frameSize.Height;
				}
				else
				{
					size.Width = frameSize.Width;
					size.Height = apertureSize.Width * (frameSize.Width / apertureSize.Height);
				}
			}
			else if (gravity == AVLayerVideoGravity.Resize)
			{
				size.Width = frameSize.Width;
				size.Height = frameSize.Height;
			}


			CGRect videoBox = CGRect.Empty;
			videoBox.Size = size;
			if (size.Width < frameSize.Width)
				videoBox.X = (frameSize.Width - size.Width) / 2;
			else
				videoBox.X = (size.Width - frameSize.Width) / 2;

			if (size.Height < frameSize.Height)
				videoBox.Y = (frameSize.Height - size.Height) / 2;
			else
				videoBox.Y = (size.Height - frameSize.Height) / 2;

			return videoBox;
		}

		#endregion

	}


	public class OutputSampleDelegate : AVCaptureVideoDataOutputSampleBufferDelegate
	{
		bool isProcessing = false;
		Action<string> greetingsCallback;
		Action<CIImage, CGRect> drawFacesCallback;

		CALayer previewLayer;

		public OutputSampleDelegate(Action<string> greetingsCallback, Action<CIImage, CGRect> drawFacesCallback)
		{
			this.greetingsCallback = greetingsCallback;
			this.drawFacesCallback = drawFacesCallback;
		}


		ImageAnalyzer imageAnalyzer = null;
		public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput,
												   CMSampleBuffer sampleBuffer,
												   AVCaptureConnection connection)
		{
			try
			{
				if (!ViewController.isFaceRegistered || isProcessing)

				{
					//		Console.WriteLine("OutputDelegate - Exit (isProcessing: " + DateTime.Now);
					sampleBuffer.Dispose();
					//Console.WriteLine("processing..");

					return;
				}


				//Console.WriteLine("IsProcessing: ");

				isProcessing = true;
				connection.VideoOrientation = AVCaptureVideoOrientation.Portrait;
				connection.VideoScaleAndCropFactor = 1.0f;

				var image = GetImageFromSampleBuffer(sampleBuffer);

				var ciImage = CIImage.FromCGImage(image.CGImage);

				var cleanAperture = sampleBuffer.GetVideoFormatDescription().GetCleanAperture(false);

				/*For Face Detection using iOS APIs*/
				DispatchQueue.MainQueue.DispatchAsync(() =>
													  drawFacesCallback(ciImage, cleanAperture));


				//Console.WriteLine(ciImage);
				Task.Run(async () =>
								{
									try
									{
										//if (ViewController.IsFaceDetected)
										//{
											Console.WriteLine("face detected: ");

											imageAnalyzer = new ImageAnalyzer(() => Task.FromResult<Stream>(image.ResizeImageWithAspectRatio(300, 400).AsPNG().AsStream()));
											await ProcessCameraCapture(imageAnalyzer);
										//}

									}

									finally
									{
										imageAnalyzer = null;
										isProcessing = false;
										Console.WriteLine("OUT ");

									}

								});
			}
			catch (Exception ex)
			{
				Console.Write(ex);
			}
			finally
			{
				sampleBuffer.Dispose();
			}

		}




		private async Task ProcessCameraCapture(ImageAnalyzer e)
		{

			DateTime start = DateTime.Now;

			await e.DetectFacesAsync();

			if (e.DetectedFaces.Any())
			{
				await e.IdentifyFacesAsync();
				string greetingsText = GetGreettingFromFaces(e);

				if (e.IdentifiedPersons.Any())
				{

					if (greetingsCallback != null)
					{
						DisplayMessage(greetingsText);
					}

					Console.WriteLine(greetingsText);
				}
				else
				{
					DisplayMessage("No Idea, who you're.. Register your face.");

					Console.WriteLine("No Idea");

				}
			}
			else
			{
				DisplayMessage("No face detected.");

				Console.WriteLine("No Face ");

				//this.UpdateUIForNoFacesDetected();

			}

			TimeSpan latency = DateTime.Now - start;
			var latencyString = string.Format("Face API latency: {0}ms", (int)latency.TotalMilliseconds);
			Console.WriteLine(latencyString);
			//this.isProcessingPhoto = false;
		}

		void DisplayMessage(string greetingsText)
		{
			DispatchQueue.MainQueue.DispatchAsync(() =>
												  greetingsCallback(greetingsText));
		}

		private string GetGreettingFromFaces(ImageAnalyzer img)
		{
			if (img.IdentifiedPersons.Any())
			{
				string names = img.IdentifiedPersons.Count() > 1 ? string.Join(", ", img.IdentifiedPersons.Select(p => p.Person.Name)) : img.IdentifiedPersons.First().Person.Name;

				if (img.DetectedFaces.Count() > img.IdentifiedPersons.Count())
				{
					return string.Format("Welcome back, {0} and company!", names);
				}
				else
				{
					return string.Format("Welcome back, {0}!", names);
				}
			}
			else
			{
				if (img.DetectedFaces.Count() > 1)
				{
					return "Hi everyone! If I knew any of you by name I would say it...";
				}
				else
				{
					return "Hi there! If I knew you by name I would say it...";
				}
			}
		}


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

							return UIImage.FromImage(cgImage);
						}
					}
				}
			}
		}



	}
}