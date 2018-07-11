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
using Android.Text;
using Android.Content.PM;
using Android.Locations;
using System.Net;
using System.IO;
using Android.Views.InputMethods;

namespace EasyFTF
{
	// activity used to update coordinates
	// don't forget to suscribe to location updates
    [Activity(Label = "@string/UpdateCoord", ScreenOrientation = ScreenOrientation.Portrait, Icon = "@drawable/coordinate")]
    public class UpdateCoordActivity : Activity, ILocationListener
    {
        // For progress cancel
        bool _Canceled = false;

        // Hold all GC stuffs, including cookiejar
        GCStuffs _gcstuffs = null;
		
		// list of selected ids to consider
        IList<String> _ids = null;
		
		// EditText for coordinates
		EditText txtCoord = null;
		
		// TextView to display coordinates in 3 different formats
		TextView lblCoordAllValues = null;
		
		// Location manager
		LocationManager locMgr = null;
		
		// Last GPS coordinates from location manager		
		String lastGPSCoords = "";
		
		// Activity creation
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

			// Create application here
            SetMainView();
			
			// Create location manager
			locMgr = GetSystemService(Context.LocationService) as LocationManager;
        }

		// on resume, for GPS update
		protected override void OnResume ()
		{
			base.OnResume ();
			RequestLocationUpdates();
		}

		private void RequestLocationUpdates(String Provider)
		{
			if(locMgr.IsProviderEnabled(Provider))
			{
			  // requests location updates every 2000 milliseconds, and only when the location changes more than 50 metre:
			  locMgr.RequestLocationUpdates(Provider, 2000, 50, this);
			}
		}
		
		private void RequestLocationUpdates()
		{		 
			// Try to get the best provider
			Criteria locationCriteria = new Criteria();
			locationCriteria.Accuracy = Accuracy.Coarse;
			locationCriteria.PowerRequirement = Power.Medium;

			// Check if location is enabled
			var locationProvider = locMgr.GetBestProvider(locationCriteria, true);
			if(locationProvider != null)
			{
			  // requests location updates every 2000 milliseconds, and only when the location changes more than 50 metre:
			  locMgr.RequestLocationUpdates(locationProvider, 2000, 50, this);
			}
			else
			{
				// Error getting location
				Toast.MakeText(this,this.Resources.GetString(Resource.String.ErrorLocation),ToastLength.Short).Show();
			}
		}
		
	  // Complementary methods that notify the application when the user has enabled or disabled the provider (for example, a user may disable GPS to conserve battery).
	  public void OnProviderEnabled (string provider)
	  {
		// Remove updates from provider (just to be sure)
		locMgr.RemoveUpdates(this);
		// Now we request notitication updates
		RequestLocationUpdates(provider);
	  }
	  
	  // Complementary methods that notify the application when the user has enabled or disabled the provider (for example, a user may disable GPS to conserve battery).
	  public void OnProviderDisabled (string provider)
	  {
		// Remove updates from provider
		locMgr.RemoveUpdates (this);
	  }
	  
	  // Notifies the application when the provider's availability changes, and provides the accompanying status (for example, GPS availability may change when a user walks indoors).
	  public void OnStatusChanged (string provider, Availability status, Bundle extras)
	  {
		if (status == Availability.Available)
		{
			// Remove updates from provider (just to be sure)
			locMgr.RemoveUpdates(this);
			
			// Now we request notitication updates
			RequestLocationUpdates(provider);
		}
		else
		{
			// No provider anymore ?
			// Remove updates from provider (just to be sure)
			locMgr.RemoveUpdates(this);
		}
	  }
	  
	  // The System will call OnLocationChanged when the user's location changes enough to qualify as a location change according to the Criteria we set when requesting location updates.
	  public void OnLocationChanged (Android.Locations.Location location)
	  {
        //base.OnLocationChanged(location)
        // STORE VALUES and link it to GPS button !!!    
        String sLat2 = GCStuffs.ConvertDegreesToDDMM(location.Latitude, true);
        String sLon2 = GCStuffs.ConvertDegreesToDDMM(location.Longitude, false);
        lastGPSCoords = /*"DD° MM.MMM: " + */sLat2 + " " + sLon2;

		Toast.MakeText(this,this.Resources.GetString(Resource.String.GPSFix),ToastLength.Short).Show();

        // If no location filled, we put it, better than nothing
        if (txtCoord.Text == "")
            txtCoord.Text = lastGPSCoords;
      }
	  
	  // The RemoveUpdates method tells the system location Service to stop sending updates to our application. By calling this in OnPause, we are able to conserve power if an application doesn't need location updates while its Activity is not on the screen:
	  protected override void OnPause ()
		{
		  base.OnPause ();
		  locMgr.RemoveUpdates(this);
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
			
			try
			{
				// Get all the nested values
				// Get coordinates
				String coord = txtCoord.Text;
				String sLat = "";
				String sLon = "";
				if (!GCStuffs.TryToConvertCoordinates(coord, ref sLat, ref sLon))
				{
					// Cancel creation wrong coordinates
					Toast.MakeText(this,this.Resources.GetString(Resource.String.BadCoordinates),ToastLength.Short).Show();
					return;
				}
				// convert to double
				double dlat = GCStuffs.ConvertToDouble(sLat);
				double dlon = GCStuffs.ConvertToDouble(sLon);
				
                // Ask if we are ready
                var builder = new AlertDialog.Builder(this);
                builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmUpdateCoord));
                builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) =>
                {
                    // Launch application inside a progress bar
                    ProgressDialog progressDialog = new ProgressDialog(this);
                    progressDialog.SetProgressStyle(ProgressDialogStyle.Horizontal);
                    progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblUpdateInProgress));
                    progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
                    progressDialog.Progress = 0;
                    progressDialog.Max = _ids.Count;
                    progressDialog.SetCancelable(false);
                    _Canceled = false;
                    progressDialog.SetButton((int)(DialogButtonType.Negative),
                        this.Resources.GetString(Resource.String.Cancel),
                        (st, evt) =>
                        {
                            // Tell the system about cancellation
                            _Canceled = true;
                        });
                    progressDialog.Show();

                    ThreadPool.QueueUserWorkItem(o => UpdateCoord(progressDialog, dlat, dlon));
                });
                builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) =>
                {
                    // do something on Cancel click
                });
                builder.Create().Show();
			}
			catch(Exception)
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.Error),ToastLength.Short).Show();
			}
        }

		// do the update coord job
		private void UpdateCoord(ProgressDialog progressDialog, double dlat, double dlon)
		{
			try
			{
                // Iterate on selection
                int nb = 1;
				foreach(String id in _ids)
				{
                    // Is it canceled ?
                    if (_Canceled)
                        break; // Yes

                    // Update progress
                    progressDialog.Progress = nb;

                    // Get info
                    String post_response = ""; // Not used here
					GCNotification gcn = _gcstuffs.GetNotificationData(id, ref post_response);
					if (gcn != null)
					{
						// We update :-)
						// WE NEED TO USE the previous post_response to get valid VIEW_STATES
						// We build the post string
						String post_string = GCStuffs.GeneratePostString(post_response, dlat, dlon, gcn.distance, gcn.name, gcn.data, gcn.email, gcn.checknotif);
						String url = "https://www.geocaching.com/notify/edit.aspx?NID=" + id;
						
						// And we post
						post_response = GCStuffs.GeneratePostRequets(url, post_string, _gcstuffs._cookieJar);
					}

                    nb++;
				}

                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // All right!
                if (_Canceled)
                {
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Canceled), ToastLength.Short).Show());
                    // Don't go to main activity !!!
                }
                else
                {
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Success), ToastLength.Short).Show());

                    // Then go back to main activity
                    GoToMainActivity(true);
                }
			}
			catch(Exception)
			{
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Crap
                RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Error), ToastLength.Short).Show());
			}
		}
		
		// Cancel configuration
		private void Cancel()
		{
			GoToMainActivity(false);
		}
		
		// Back button pressed
		public override void OnBackPressed()
        {
			var builder = new AlertDialog.Builder(this);
			builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmCancelUpdateCoord));
			builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) => 
			{
				// Execute cancel operation
				Cancel();
				base.OnBackPressed();
			});
			builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) => 
			{ 
				// do something on Cancel click
			});
			builder.Create().Show();
			
			
        }
		
		// Cancel button clicked
        private void Cancel_Click(object sender, EventArgs e)
        {
            var builder = new AlertDialog.Builder(this);
			builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmCancelUpdateCoord));
			builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) => 
			{
				// Execute cancel operation
				Cancel();
			});
			builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) => 
			{ 
				// do something on Cancel click
			});
			builder.Create().Show();
        }

		// go back to MainActivity
		private void GoToMainActivity(bool created)
		{
			// Finish GPS updates
			locMgr.RemoveUpdates(this);
			
			// Back to MainActivity, without any parameter
            var intent = new Intent(this, typeof(MainActivity));
			if (created)
				SetResult (Result.Ok, intent);
			else
				SetResult (Result.Canceled, intent);
				
			Finish();
		}

        // PickMap_Click button clicked
        private void PickMap_Click(object sender, EventArgs e)
        {
//#if (FULL)
            // Create intent to launch configuration activity
            var intent = new Intent(this, typeof(PickActivity));

            // Create parameters to pass : login & password, even if they are empty
            List<string> parameters = new List<string>();
            String coord = txtCoord.Text;
            String sLat = "";
            String sLon = "";
            if (GCStuffs.TryToConvertCoordinates(coord, ref sLat, ref sLon))
            {
                parameters.Add(sLat);
                parameters.Add(sLon);
            }
            else
            {
                parameters.Add("");
                parameters.Add("");
            }
            intent.PutStringArrayListExtra("coordinates", parameters);

            // Start the activity waiting for a result
            StartActivityForResult(intent, 10); // 10 for map
            return;
//#endif
/*
#if (LITE)
            Toast.MakeText(this, this.Resources.GetString(Resource.String.OnlyFullMap), ToastLength.Long).Show();
            return;
#endif
*/
        }

        // btnCoord button clicked, get coordinates from current location
        private void btnCoord_Click(object sender, EventArgs e)
        {
            // If we have GPS values, we store them on editCoord
            if (lastGPSCoords != "")
            {
                txtCoord.Text = lastGPSCoords;
                Toast.MakeText(this, this.Resources.GetString(Resource.String.GPSFixSet), ToastLength.Short).Show();
            }
            else
            {
                // No coord yet
                Toast.MakeText(this, this.Resources.GetString(Resource.String.ErrorNoGPSFix), ToastLength.Short).Show();
            }
        }

        // Define the view
        private void SetMainView()
        {
            // Set our view from the "UpdateCoordLayout" layout resource
            SetContentView(Resource.Layout.UpdateCoordLayout);

			// Get params
            if (Intent.Extras != null)
                _ids = Intent.Extras.GetStringArrayList("selectionids") ?? new string[0];

            // Check validity of parameter
            if ((_ids != null) && (_ids.Count != 0))
            {
                // List is valid, we continue
            }
			else
			{
				// Nothing to do
				// Execute cancel operation
                var intent = new Intent(this, typeof(MainActivity));
				SetResult (Result.Canceled, intent);
				Finish();
			}
			
            // Hide the bloody keyboard by default ?
            Window.SetSoftInputMode(SoftInput.StateAlwaysHidden);

            // Get our button from the layout resource,
            // and attach an event to it
            Button button = FindViewById<Button>(Resource.Id.Save);
            button.Click += Save_Click;
            button = FindViewById<Button>(Resource.Id.BtnQuit);
            button.Click += Cancel_Click;
            button = FindViewById<Button>(Resource.Id.btnCoord);
            button.Click += btnCoord_Click;
            button = FindViewById<Button>(Resource.Id.PickMap);
            button.Click += PickMap_Click;

            // Store edits for coordinates
            txtCoord = FindViewById<EditText>(Resource.Id.editCoord);
            lblCoordAllValues = FindViewById<TextView>(Resource.Id.lblCoordAllValues);
            txtCoord.TextChanged += (sender, e) =>
            {
                lblCoordAllValues.Text = GCStuffs.ConvertCoordinates(txtCoord.Text);
            };
            
            // Now we perform time consumming activities 
            // chec GC account
            ProgressDialog progressDialog = new ProgressDialog(this);
            progressDialog.SetProgressStyle(ProgressDialogStyle.Spinner);
            progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblInitInProgress));
            progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
            progressDialog.Indeterminate = true;
            progressDialog.SetCancelable(false);
            progressDialog.Show();

            ThreadPool.QueueUserWorkItem(o => PerformInit(progressDialog));
        }

        // Perform HMI stuff : check GC account
        private void PerformInit(ProgressDialog progressDialog)
        {
            // Create a new _gcstuffs
            _gcstuffs = new GCStuffs();

            // We read configuration from exportdata
            bool needtoconf = false;
            List<String> conf = GCStuffs.LoadDataString();
            if ((conf != null) && (conf.Count >= 2))
            {
                // We have a configuration on exportdata
                // check if account is valid and populate cookiejar
                if (_gcstuffs.CheckGCAccount(conf[0], conf[1], true, this))
                {
                    // All right !
                }
                else
                {
                    needtoconf = true;
                }
            }
            else
            {
                needtoconf = true;
            }

            // Do we need to configure ? no reason to be there in that case
            if (needtoconf)
            {
                // Kill progressdialog (we are in UI thread already, good)
                if (progressDialog != null)
                    RunOnUiThread(() => progressDialog.Hide());

                // Need to configure :-(
                RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.AccountConfigure), ToastLength.Short).Show());
                return;
            }

            // Kill progressdialog (we are in UI thread already, good)
            if (progressDialog != null)
                RunOnUiThread(() => progressDialog.Hide());
        }

        // Result of an activity, back to work!
        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            if (resultCode == Result.Ok)
            {
                if (requestCode == 10) // Map
                {
                    IList<String> p = null;
                    if (data != null)
                        p = data.Extras.GetStringArrayList("coordinates") ?? new string[0];
                    
                    // Check validity of parameter
                    if ((p != null) && (p.Count == 2))
                    {
                        // List is valid, populate login & password
                        String sLat2 = GCStuffs.ConvertDegreesToDDMM(GCStuffs.ConvertToDouble(p[0]), true);
                        String sLon2 = GCStuffs.ConvertDegreesToDDMM(GCStuffs.ConvertToDouble(p[1]), false);
                        txtCoord.Text = /*"DD° MM.MMM: " + */sLat2 + " " + sLon2;
                        Toast.MakeText(this, this.Resources.GetString(Resource.String.PickDone), ToastLength.Short).Show();
                    }
                }
                
            }
        }
    }
}
 