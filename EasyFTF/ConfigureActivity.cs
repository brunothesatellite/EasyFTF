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

namespace EasyFTF
{
	// activity used to configure login & password
    [Activity(Label = "@string/Configure", ScreenOrientation = ScreenOrientation.Portrait, Icon = "@drawable/configure")]
    public class ConfigureActivity : Activity
    {
		// Activity creation
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create application here
            SetMainView();
        }

		// Save button clicked
        private void Save_Click(object sender, EventArgs e)
        {
			// Check if internet access is available
			if (!GCStuffs.CheckNetworkAccess(this))
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoInternet),ToastLength.Short).Show();
				return;
			}

            ProgressDialog progressDialog = new ProgressDialog(this);
            progressDialog.SetProgressStyle(ProgressDialogStyle.Spinner);
            progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblAccountVerif));
            progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
            progressDialog.Indeterminate = true;
            progressDialog.SetCancelable(false);
            progressDialog.Show();

            ThreadPool.QueueUserWorkItem(o => PerformAuthentication(progressDialog));
        }
        
		// Perform authentication and quit to mainactivity if ok
		private void PerformAuthentication(ProgressDialog progressDialog)
		{
            // Check that filled values are valid
            GCStuffs gc = new GCStuffs();

            // Retrieve edittext for password & login
            EditText txtLogin = FindViewById<EditText>(Resource.Id.editLogin);
            EditText txtPassword = FindViewById<EditText>(Resource.Id.editPassword);

            if (gc.CheckGCAccount(txtLogin.Text, txtPassword.Text, true, this))
            {
                Thread.Sleep(1000);

                // We have valid information
                // We save the configuration
                GCStuffs.ExportData(txtLogin.Text, txtPassword.Text);

                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // All right!
                RunOnUiThread(() => Toast.MakeText(this,this.Resources.GetString(Resource.String.AccountGood) + " " + txtLogin.Text,ToastLength.Short).Show());
				
				// Go back in MainActivity and pass the valid login & password
                var intent = new Intent(this, typeof(MainActivity));
                SetResult(Result.Ok, intent);
				Finish();
            }
            else
            {
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Invalid account information, throw a message
                RunOnUiThread(() => Toast.MakeText(this,this.Resources.GetString(Resource.String.AccountError),ToastLength.Short).Show());
            }
		}
		
		
		// Cancel configuration
		private void Cancel()
		{
			// We do not change the current account information
			// Start MainActivity
            var intent = new Intent(this, typeof(MainActivity));
            SetResult(Result.Canceled, intent);
			Finish();
		}
		
		// Back button pressed
		public override void OnBackPressed()
        {
			// Execute cancel operation
			Cancel();
            base.OnBackPressed();
        }
		
		// Cancel button clicked
        private void Cancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

		// Define the view
        private void SetMainView()
        {
            // Set our view from the "Configuration" layout resource
            SetContentView(Resource.Layout.Configuration);

            // Hide the bloody keyboard by default ?
            Window.SetSoftInputMode(SoftInput.StateAlwaysHidden);

            // Retrieve edittext for password & login
            EditText txtLogin = FindViewById<EditText>(Resource.Id.editLogin);
            EditText txtPassword = FindViewById<EditText>(Resource.Id.editPassword);
		            
			// We read configuration from exportdata
            List<String> conf = GCStuffs.LoadDataString();
            if ((conf != null) &&(conf.Count >= 2))
            {
				txtLogin.Text = conf[0];
				txtPassword.Text = conf[1];
            }
			
            // Get our button from the layout resource,
            // and attach an event to it
            Button button = FindViewById<Button>(Resource.Id.Save);
            button.Click += Save_Click;
            button = FindViewById<Button>(Resource.Id.Cancel);
            button.Click += Cancel_Click;
        }
    }
}