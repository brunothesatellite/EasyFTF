using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content.PM;
using Android.Gms.Common;
using Android.Text.Method;
using Android.Text;

namespace EasyFTF
{
	// activity used to display about box
    [Activity(Label = "@string/About", ScreenOrientation = ScreenOrientation.Portrait, Icon = "@drawable/icon")]
    public class AboutActivity : Activity
    {
		// list of parameters
        IList<String> parameters = null;
		
		// Activity creation
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create application here
            // Set our view from the "About" layout resource
            SetContentView(Resource.Layout.About);

            // Online help
            TextView lblHelp = FindViewById<TextView>(Resource.Id.lblHelp);
            lblHelp.TextFormatted = Html.FromHtml("<a href=http://mgmgeo.free.fr/ezftf>" + Resources.GetString(Resource.String.ShowHelp) + "</a>\n");
            lblHelp.MovementMethod = LinkMovementMethod.Instance;

            // Email
            var lblAboutMail = FindViewById<TextView>(Resource.Id.lblAboutMail);
            lblAboutMail.TextFormatted = Html.FromHtml("<a href=\"mailto:spaceeye@free.fr\">" + Resources.GetString(Resource.String.Mail)  + "</a>\n");
            lblAboutMail.MovementMethod = LinkMovementMethod.Instance;

            // License
            TextView lblLicense = FindViewById<TextView>(Resource.Id.lblLicense);
            lblLicense.TextFormatted = Html.FromHtml("<a href=https://profile.flaticon.com/license/fi/SwkgUxJEL3JglsTgaFIv-7Lfhb7w7SVQlh01IZZJdyELjtiUMM63BVFGqkz7cy6TK3lXOaohEGeNtgmMGtaWwqgahlMXdt6wCBMs7ivRwujY5QyXbkyNTDkL7dizB2in957Q1YVpoFI4_2zs3HI3afdDN3MyTB7RzbylwcsZt7uWt9l29PxgTUa6eOC5WasoMxSwd-yXdHO8JYCrIjNsMDcb0Hcwr7pxnZn27kLBqckDV2XpTjHwjBpwFCB-90D-PoMth6pQLb_seJIfNVSgjlr01ShRH-Odylg4FpazgfhMjb7bxT9r-BJq49M2m5_rfRs6jIru1EqUC3tfoKdBo5gta2kB91BVXeLpXfhePIqc0SpqbMZ1YR4hsYWLuFS4>" + Resources.GetString(Resource.String.License) + "</a>\n");
            lblLicense.MovementMethod = LinkMovementMethod.Instance;

            // Google Maps freaking license
            var lblAboutMaps = FindViewById<TextView>(Resource.Id.lblMaps);
            lblAboutMaps.Text = "";
            ThreadPool.QueueUserWorkItem(o => GetGoogleLicense());

            // The text
            TextView lblAbout = FindViewById<TextView>(Resource.Id.lblAbout);
			
			// Get params
            if (Intent.Extras != null)
                parameters = Intent.Extras.GetStringArrayList("about") ?? new string[0];

            // Check validity of parameter
            if ((parameters != null) && (parameters.Count != 0))
            {
                // List is valid, we continue
				lblAbout.Text = "Version " + parameters[0] + " " + parameters[1] + "\n";

                DateTime timeLimit = DateTime.Now;
                // Is expiration mechanism active?
                if (SplashScreen.GetExpirationDate(ref timeLimit))
                {
                    // are we to late to use EzFTF?
                    lblAbout.Text += Resources.GetString(Resource.String.ExpireDate) + " " + timeLimit.ToLongDateString() + "\r\n";
                }
            }
        }

        public void GetGoogleLicense()
        {
            String txt;
            var lblAboutMaps = FindViewById<TextView>(Resource.Id.lblMaps);
            try
            {
                if (GooglePlayServicesUtil.IsGooglePlayServicesAvailable(this) == ConnectionResult.Success)
                {
                    txt = GooglePlayServicesUtil.GetOpenSourceSoftwareLicenseInfo(this);
                    if (txt != null)
                    {
                        RunOnUiThread(() => lblAboutMaps.Text = txt);
                    }
                    else
                    {
                        RunOnUiThread(() => lblAboutMaps.Text = "");
                    }
                }
            }
            catch (Exception ex)
            {
                RunOnUiThread(() => lblAboutMaps.Text = ex.Message);
            }
        }

        // Back button pressed
        public override void OnBackPressed()
        {
			// Execute cancel operation
			var intent = new Intent(this, typeof(MainActivity));
            intent.PutStringArrayListExtra("parameters", parameters);
            SetResult(Result.Canceled, intent);
			Finish();
            base.OnBackPressed();
        }
    }
}