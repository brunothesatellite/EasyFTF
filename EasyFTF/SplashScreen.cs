using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using System;
using System.Threading;

namespace EasyFTF  
{  
    //Set MainLauncher = true makes this Activity Shown First on Running this Application  
    //Theme property set the Custom Theme for this Activity  
    //No History= true removes the Activity from BackStack when user navigates away from the Activity  
    [Activity(Label = "EasyFTF", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait, Theme = "@style/Theme.Splash", NoHistory = true, Icon = "@drawable/icon")]  
    public class SplashScreen : Activity
    {
        // *************************************************************
        // *** TO MODIFY AND CHECK PRIOR TO EACH PUBLICATION         ***
        // *** 1) check expiration date and expiration NEED
        // *** 2) update version string in :
        //        - Manifest :
        //            > Package name !
        //            > VersionCode : incremental on build
        //            > VersionName : version of EzFTF
        // *************************************************************
        public static bool GetExpirationDate(ref DateTime expirationDate)
        {
            expirationDate = new DateTime(2026, 09, 30); // CHECK THE DATE
            return false;                                 // CHECK IF IT IS STILL LIMITED
        }
        // *************************************************************
        // *************************************************************

        protected override void OnCreate(Bundle bundle)  
        {
            base.OnCreate(bundle);

            DateTime timeLimit = DateTime.Now;
            bool bExpired = false;
            // Is expiration mechanism active?
            if (GetExpirationDate(ref timeLimit))
            {
                // are we to late to use EzFTF?
                bExpired = (DateTime.Now > timeLimit);
            }

            if (bExpired)
            {
                // Message to inform that it is expired
                Toast.MakeText(this, this.Resources.GetString(Resource.String.ExpiredVersion), ToastLength.Long).Show();
                ThreadPool.QueueUserWorkItem(o => DoFinish());
            }
            else if (!GCStuffs.CheckNetworkAccess(this))
            {
                Toast.MakeText(this, this.Resources.GetString(Resource.String.NoInternet), ToastLength.Long).Show();
                ThreadPool.QueueUserWorkItem(o => DoFinish());
            }
            else
            {
                //Start MainActivity Activity  
                StartActivity(typeof(MainActivity));
            }
        }

        // We wait and die
        private void DoFinish()
        {
            Thread.Sleep(3400);
            Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
        }

    }
} 