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
	// activity used to create a notification
	// don't forget to suscribe to location updates
    [Activity(Label = "@string/Add", ScreenOrientation = ScreenOrientation.Portrait, Icon = "@drawable/create")]
    public class CreationActivity : Activity, ILocationListener
    {
		// Nb of notifications already created
		int nbNotifs = 0;
		
        // To prevent double email checking
        bool _CheckingEmails = false;

        // List of emails passed through intent
        IList<String> _emails = null;

        // For progress cancel
        bool _Canceled = false;

        // Hold all GC stuffs, including cookiejar
        GCStuffs _gcstuffs = null;
		
		// Internal object that holds all the supported caches
        List<TypeCache> _typecaches = new List<TypeCache>();
		
		// Adapter used to display type caches
        TypeCacheAdapter _fa;
		
		// Allowed type cache for creation
		List<Tuple<int, string, List<string>, int>> _allowedtypes = null;
		
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

            // Get params
            if (Intent.Extras != null)
			{
                _emails = Intent.Extras.GetStringArrayList("emails") ?? new string[0];
            
				// Try to retrieve number of notifications
                var n = Intent.Extras.GetStringArrayList("nbNotifs") ?? new string[0];
				if ((n != null)&&(n.Count != 0))
				{
					Int32.TryParse(n[0], out nbNotifs);
				}
			}
            
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
		/*
			string Provider = LocationManager.GpsProvider;
			if(locMgr.IsProviderEnabled(Provider))
			{
			  // requests location updates every 2000 milliseconds, and only when the location changes more than 50 metre:
			  locMgr.RequestLocationUpdates(Provider, 2000, 50, this);
			 }*/
			 
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

		private void CreateNotifications()
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
				
				// Get radius
				String radius = FindViewById<EditText>(Resource.Id.editRadius).Text;
                int distance = 0;
                if (!Int32.TryParse(radius, out distance) || (distance == 0))
                {
                    // Cancel creation wrong radius
                    Toast.MakeText(this, this.Resources.GetString(Resource.String.BadDistance), ToastLength.Short).Show();
                    return;
                }

				// Get Name
				String name = FindViewById<EditText>(Resource.Id.editName).Text;
				if (name == "")
				{
					// Cancel creation since empty name
					Toast.MakeText(this,this.Resources.GetString(Resource.String.BadName),ToastLength.Short).Show();
					return;
				}
				
				// Email
				Spinner mySpinner = FindViewById<Spinner>(Resource.Id.spinnerEmail);
                String email = "";
                if (mySpinner.Visibility != ViewStates.Invisible)
                {
                    if (mySpinner.SelectedItem != null)
                    {
                        email = mySpinner.SelectedItem.ToString();
                    }
                    /*
                    if (email == "")
                    {
                        // Cancel creation since empty name
                        Toast.MakeText(this, this.Resources.GetString(Resource.String.BadEmail), ToastLength.Short).Show();
                        return;
                    }*/
                }
				// Types : check that at least one type is checked
				List<Tuple<int, string, List<string>, int>> selectedTypes = new List<Tuple<int, string, List<string>, int>>();
				foreach(var tc in _typecaches)
				{
					// Is it checked ?
					if (tc.Checked)
					{
						// Yes, so we find the correspoding tuple
						foreach(var tpl in _allowedtypes)
						{
							if (tpl.Item2 == tc.Type)
							{
								// Found it !
								selectedTypes.Add(tpl);
							}
						}
					}
				}
				
				// At least one type selected ?
				int nbsel = selectedTypes.Count;
				if (nbsel == 0)
				{
					// Cancel creation since no type
					Toast.MakeText(this,this.Resources.GetString(Resource.String.NoType),ToastLength.Short).Show();
					return;
				}
				else
				{
					// Check if we go higher that 40 notifications
					if ((nbNotifs + nbsel) > 40)
					{
						int maxnb = 40 - nbNotifs;
						String msg = String.Format(this.Resources.GetString(Resource.String.MaxNotifWillReached), nbNotifs, maxnb);
						Toast.MakeText(this,msg,ToastLength.Long).Show();
						return;
					}
				}

                // Ask if we are ready
                var builder = new AlertDialog.Builder(this);
                builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmCreate));
                builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) =>
                {
                    // Launch application inside a progress bar
                    ProgressDialog progressDialog = new ProgressDialog(this);
                    progressDialog.SetProgressStyle(ProgressDialogStyle.Horizontal);
                    progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblCreateInProgress));
                    progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
                    progressDialog.Progress = 0;
                    progressDialog.Max = selectedTypes.Count;
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

                    ThreadPool.QueueUserWorkItem(o => CreateNotificationsImpl(progressDialog, dlat, dlon, distance, name, selectedTypes, email));
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
		
		// do the creation job
		private void CreateNotificationsImpl(ProgressDialog progressDialog, double dlat, double dlon, int distance, String name, List<Tuple<int, string, List<string>, int>> selectedTypes, String email)
		{
			try
			{
				// we create notifications
				// No iterate on selected type and create !
				String url = "https://www.geocaching.com/notify/edit.aspx";
				String post_response = "";
				String post_string = "";
                int nb = 1;
				bool error = false;
				String warning = "";
				foreach (var tpl in selectedTypes)
				{
                    // Is it canceled ?
                    if (_Canceled)
                        break; // Yes

                    // Update progress
                    progressDialog.Progress = nb;

                    // Progress message
                    RunOnUiThread(() => progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblCreateInProgress) + " - " + tpl.Item2));

                    // On demande la page par défaut pour initialiser une nouvelle demande
                    HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
					objRequest.CookieContainer = _gcstuffs._cookieJar; // surtout récupérer le container de cookie qui est maintenant renseigné avec le cookie d'authentification
					HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
					using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
					{
						post_response = responseStream.ReadToEnd();
						responseStream.Close();
					}
					// On regarde si on a claqué le nombre max
					warning = GCStuffs.CheckWarningMessage(post_response);
					if (warning != "")
					{
						error = true;
						break;
					}
					
					// Une mise à jour pour définir le type de cache
					post_string = GCStuffs.GeneratePostString(post_response, dlat, dlon, distance, name, tpl, email, true);
					post_response = GCStuffs.GeneratePostRequets(url, post_string, _gcstuffs._cookieJar);

					// Une mise à jour pour définir le type de notif
					post_string = GCStuffs.GeneratePostString(post_response, dlat, dlon, distance, name, tpl, email, true);
					post_response = GCStuffs.GeneratePostRequets(url, post_string, _gcstuffs._cookieJar);

					// Vérification de la création correcte !
					warning = GCStuffs.CheckValidationMessage(post_response);
					if (warning != "")
					{
						error = true;
						break;
					}
					
					// On décoche le type que l'on vient de poster
					foreach(var tc in _typecaches)
					{
						
						if (tc.Type == tpl.Item2)
						{
							// Found it !
							// On le décoche
							RunOnUiThread(() => tc.Checked = false);
						}
					}
					
                    nb++;
				}

                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // All right!
				if (error)
				{
					RunOnUiThread(() => Toast.MakeText(this, warning, ToastLength.Long).Show());
                    // Don't go to main activity !!!
				}
				else if (_Canceled)
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
			catch(Exception ex)
			{
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Crap
                RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Error) + "\n" + ex.Message, ToastLength.Long).Show());
			}
		}
		
		// Save button clicked
        private void Save_Click(object sender, EventArgs e)
        {
		    CreateNotifications();
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
			builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmCancelCreate));
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
			builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmCancelCreate));
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

            // Create coordinates to pass
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

            String radius = FindViewById<EditText>(Resource.Id.editRadius).Text;
            if (!String.IsNullOrEmpty(radius))
                parameters.Add(radius);

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

        // To manually update emails
        private void UpdateEmails_Click(object sender, EventArgs e)
        {
            // Avoid multiple calls
            if (!_CheckingEmails)
            {
                ProgressDialog progressDialog = new ProgressDialog(this);
                progressDialog.SetProgressStyle(ProgressDialogStyle.Spinner);
                progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblEmailInProgress));
                progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
                progressDialog.Indeterminate = true;
                progressDialog.SetCancelable(false);
                progressDialog.Show();

                // Now we perform time consumming activities 
                // chec GC account
                // retrieve emails from GC.com
                ThreadPool.QueueUserWorkItem(o => PerformGCActions(progressDialog));
            }
        }

        // Define the view
        private void SetMainView()
        {
            // Set our view from the "Creation" layout resource
            SetContentView(Resource.Layout.Creation);

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

            // This button is not used anymore
            ImageButton btnimg = FindViewById<ImageButton>(Resource.Id.UpdateEmails);
            btnimg.Visibility = ViewStates.Invisible;
			// If we have no emails, hide the stuff immediately
			if ((_emails == null) || (_emails.Count == 0))
            {
                // Hide the spinner and associated label
                Spinner spinner = FindViewById<Spinner>(Resource.Id.spinnerEmail);
                spinner.Visibility = ViewStates.Invisible;
                TextView txt = FindViewById<TextView>(Resource.Id.lblEmail);
                txt.Visibility = ViewStates.Invisible;
                ImageView img = FindViewById<ImageView>(Resource.Id.imageEmail);
                img.Visibility = ViewStates.Invisible;
            }
            else
            {
                PopulateSpinnerWithEmails(_emails.ToList());
            }

            // Adapter
            _fa = new TypeCacheAdapter(this, new List<TypeCache>());
            var gridview = FindViewById<GridView>(Resource.Id.gridview);
            gridview.Adapter =_fa;
            gridview.ItemClick += dataview_ItemClick;

            // Create initial list of supported caches
            _allowedtypes = new List<Tuple<int, string, List<string>, int>>
            {
#if (FULL)
                Tuple.Create(2, "Traditional Cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$6"}), Resource.Drawable.Tradi),
#endif
                Tuple.Create(3, "Multi-cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$6"}), Resource.Drawable.Multi),

                Tuple.Create(8, "Unknown Cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$6"}), Resource.Drawable.Unknown),
                Tuple.Create(137, "Earthcache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$6"}), Resource.Drawable.Earth),
                Tuple.Create(5, "Letterbox Hybrid", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$6"}), Resource.Drawable.Letterbox),
                Tuple.Create(1858, "Wherigo Cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$6"}), Resource.Drawable.Wherigo),
                Tuple.Create(4, "Virtual Cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$6"}), Resource.Drawable.Virtual),

                Tuple.Create(13, "Cache In Trash Out Event", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$7"}), Resource.Drawable.CITO),
                Tuple.Create(6, "Event Cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$7"}), Resource.Drawable.Event),
                Tuple.Create(453, "Mega-Event Cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$7"}), Resource.Drawable.Mega),
				Tuple.Create(7005, "Giga-Event Cache", new List<string>(new string[] { "ctl00$ContentBody$LogNotify$cblLogTypeList$7"}), Resource.Drawable.Giga)
                
            };

#if (LITE)
            Toast.MakeText(this, this.Resources.GetString(Resource.String.OnlyFullTypes), ToastLength.Long).Show();
#endif
            foreach (var tpl in _allowedtypes) 
            {
                _typecaches.Add(new TypeCache(tpl.Item2, tpl.Item4));
            }

            // Pass these notifications to the adapter
            _fa.DisplayedTypeCaches = _typecaches;

            // Notify adapter that notifications changed to trigger refresh
            _fa.NotifyDataSetChanged();

            // Store edits for coordinates
            txtCoord = FindViewById<EditText>(Resource.Id.editCoord);
            lblCoordAllValues = FindViewById<TextView>(Resource.Id.lblCoordAllValues);
            txtCoord.TextChanged += (sender, e) =>
            {
                lblCoordAllValues.Text = GCStuffs.ConvertCoordinates(txtCoord.Text);
            };

            // Now we perform time consumming activities 
            // chec GC account
            // retrieve emails from GC.com
            ThreadPool.QueueUserWorkItem(o => PerformGCActions(null));
        }

        // Perform HMI stuff : check GC account and populate emails
        private void PerformGCActions(ProgressDialog progressDialog)
        {
            if (_CheckingEmails)
                return;
            _CheckingEmails = true;

            if (!GCStuffs.CheckNetworkAccess(this))
            {
                if (progressDialog != null)
                    RunOnUiThread(() => progressDialog.Hide());

                // Need to connect to internet :-(
                RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.NoInternet), ToastLength.Short).Show());
                _CheckingEmails = false;
                return;
            }

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
                _CheckingEmails = false;
                return;
            }
            
            // Kill progressdialog (we are in UI thread already, good)
            if (progressDialog != null)
                RunOnUiThread(() => progressDialog.Hide());
            _CheckingEmails = false;
        }

        // Populate spinner with emails
        private void PopulateSpinnerWithEmails(List<String> emails)
        {
            Spinner spinner = FindViewById<Spinner>(Resource.Id.spinnerEmail);
            var adapter = new ArrayAdapter(this, Android.Resource.Layout.SimpleSpinnerItem);
            foreach (String email in emails)
                adapter.Add(email);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            spinner.Adapter = adapter;
        }

        // Thrown when a listview/gridview item is clicked
        private void dataview_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
			// We toggle check status of the clicked item
            TypeCache sel = _typecaches[e.Position];
            sel.Checked = !sel.Checked;
			
			// Don't forget to notify the adapter to force refresh
            _fa.NotifyDataSetChanged();
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
 