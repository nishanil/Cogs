
using Foundation;
using System;
using UIKit;

namespace LiveCam
{
    public partial class SecondViewController : UIViewController
    {
		public UIImage Image
		{
			get;

			set;
		}
		partial void UIButton22_TouchUpInside(UIButton sender)
		{
			NavigationController.PopViewController(true);
		}

		public SecondViewController (IntPtr handle) : base (handle)
        {
        }

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();
			SampleImage.ContentMode = UIViewContentMode.ScaleAspectFit;
			SampleImage.Image = Image;
		}
    }
}