// WARNING
//
// This file has been generated automatically by Xamarin Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using Foundation;
using System;
using System.CodeDom.Compiler;

namespace LiveCam
{
    [Register ("ViewController")]
    partial class ViewController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel GreetingsLabel { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIView HomeView { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (GreetingsLabel != null) {
                GreetingsLabel.Dispose ();
                GreetingsLabel = null;
            }

            if (HomeView != null) {
                HomeView.Dispose ();
                HomeView = null;
            }
        }
    }
}