using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Gms.Maps;
using Android.Support.V4.Content;
using Android.Gms.Maps.Model;
using Android;
using Android.Content.PM;
using Android.Graphics;
using System.Threading;

namespace EasyFTF
{
    [Activity(Label = "@string/ShowMap", ScreenOrientation = ScreenOrientation.Portrait, Icon = "@drawable/map")]
    public class ShowOnMapActivity : Activity, IOnMapReadyCallback
    {
        // For progress cancel
        bool _Canceled = false;

        // Hold all GC stuffs, including cookiejar
        GCStuffs _gcstuffs = null;
		
		// map object
        GoogleMap _map = null;
		
		// list of selected ids to consider
        IList<String> _ids = null;
		
		// Leave activity
		private void GoodBye()
		{
			var intent = new Intent(this, typeof(MainActivity));
			SetResult(Result.Canceled, intent);
			Finish();
		}
		
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "ShowOnMapLayout" layout resource
            SetContentView(Resource.Layout.ShowOnMapLayout);

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
                GoodBye();
			}
            			
            // Create your application here
            var frag = FragmentManager.FindFragmentById<MapFragment>(Resource.Id.map);
            frag.GetMapAsync(this);

            Button button = FindViewById<Button>(Resource.Id.BtnQuit);
            button.Click += Cancel_Click;
        }

        public void OnMapReady(GoogleMap googleMap)
        {
            if (googleMap != null)
            {
                //Map is ready for use
                _map = googleMap;

                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == Permission.Granted)
                {
                    _map.MyLocationEnabled = true;
                    _map.UiSettings.CompassEnabled = true;
                    _map.UiSettings.ZoomControlsEnabled = true;

					// Default map position in Marseille
                    CameraPosition.Builder builder = CameraPosition.InvokeBuilder();
                    LatLng location = new LatLng(43.3, 5.4);
                    builder.Target(location);
                    builder.Zoom(13);
                    CameraPosition cameraPosition = builder.Build();
                    CameraUpdate cameraUpdate = CameraUpdateFactory.NewCameraPosition(cameraPosition);
                    _map.MoveCamera(cameraUpdate);

                    // Custom marker popup
                    _map.SetInfoWindowAdapter(new CustomMarkerPopupAdapter(LayoutInflater));

                    // Now we launch the async request to display notifications on map
                    // Launch application inside a progress bar
                    ProgressDialog progressDialog = new ProgressDialog(this);
                    progressDialog.SetProgressStyle(ProgressDialogStyle.Horizontal); 
                    progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblDisplayInProgress));
                    progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
                    progressDialog.Progress = 0;
                    progressDialog.Max = _ids.Count;
                    progressDialog.SetCancelable(false);
                    _Canceled = false;
                    progressDialog.SetButton((int)(DialogButtonType.Negative),
                        this.Resources.GetString(Resource.String.Cancel),
                        (s, ev) =>
                        {
                            // Tell the system about cancellation
                            _Canceled = true;
                        });

                    progressDialog.Show();
                    
                    ThreadPool.QueueUserWorkItem(o => ShowOnMap(progressDialog));
                }
                else
                {
                    // Show rationale and request permission.
                    Toast.MakeText(this, this.Resources.GetString(Resource.String.ErrorLocation), ToastLength.Short).Show();
                }
                
            }

        }

		private void ShowOnMap(ProgressDialog progressDialog)
		{
			try
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
                    // Need to configure :-(
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.AccountConfigure), ToastLength.Short).Show());
                    GoodBye();
                }

                // we get information
                // Now iterate
                // dictionary with data
                Dictionary<String, List<GCNotification>> diconotifs = new Dictionary<string, List<GCNotification>>();
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
						// We stacks notifs with same coordinates
						// key is lat+lon
						String key = gcn.dlat.ToString() + gcn.dlon.ToString() + gcn.distance.ToString();
						if (diconotifs.ContainsKey(key))
						{
							// update existing
							diconotifs[key].Add(gcn);
						}
						else
						{
							// new one
							diconotifs.Add(key, new List<GCNotification>(new GCNotification[] { gcn }));
						}
					}

                    nb++;
				}

                // Store all marker locations
                List<LatLng> markerslocations = new List<LatLng>();

                // iterate on notifications
                foreach (KeyValuePair<String, List<GCNotification>> pair in diconotifs)
                {
					// We create the marker
					// Get color
					Color c = Color.Pink;
					float b = BitmapDescriptorFactory.HueRose;
					GCNotification gcn = pair.Value[0];
                    List<GCNotification> gcns = pair.Value;
                    if (pair.Value.Count == 1)
                	{
						// get color of this single notif
                		gcn.GetIcon(ref b, ref c);
                	}
										
					// Create marker
					LatLng location = new LatLng(gcn.dlat, gcn.dlon);
					// Not necessary since we do it for the circles right below
                    //markerslocations.Add(location);
					
					// Create markeroptions
                    MarkerOptions mk = new MarkerOptions();
                    mk.SetPosition(location);

                    // And the icon color
                    mk.SetIcon(BitmapDescriptorFactory.DefaultMarker(b));

                    // And a title and snippet
                    String title = "";
                    String snippet = "";

                    // Title and snippet depending on number of gnc
                    if (gcns.Count == 1)
                    {
                        // Single notification
                        title = gcn.name + " (" + gcn.distance.ToString() + " Km)";
                        snippet = gcn.GetTypeKeyInEnglish();
                    }
                    else
                    {
                        // Merged markers
                        // Create tooltip (may be to long, anyway...)
                        // Everyone is colocated
                        // Try to regroup by gcn names
                        Dictionary<String, List<GCNotification>> dicoNameGCN = new Dictionary<String, List<GCNotification>>();
                        foreach (GCNotification gn in gcns)
                        {
                            // Regroup by name
                            if (dicoNameGCN.ContainsKey(gn.name))
                                dicoNameGCN[gn.name].Add(gn);
                            else
                                dicoNameGCN.Add(gn.name, new List<GCNotification>(new GCNotification[] { gn }));
                        }

                        // Now create the tip
                        String tip = "";
                        foreach (KeyValuePair<String, List<GCNotification>> pair2 in dicoNameGCN)
                        {
                            // this is the gcn name
                            tip += pair2.Key + "\n";

                            // Now list all type / kind of notification
                            foreach (GCNotification g in pair2.Value)
                            {
                                // Type (tradi, etc...)
                                tip += "    " + g.GetTypeKeyInEnglish();// + ": ";

                                // And now the kind of notif (publish, etc...)
                                // NO! THIS IS THE POST VALUE NOT READABLE
                                /*
                                foreach(String kn in g.data.Item3)
                                {
                                    tip += kn + " ";
                                }
                                */
                                // new line
                                tip += "\n";
                            }
                        }

                        // Assign values
                        title = Resources.GetString(Resource.String.MergedMarkers) + " (" + gcns[0].distance.ToString() + " Km)";
                        snippet = tip;
                    }

                    // Assign real values
                    mk.SetTitle(title);
                    mk.SetSnippet(snippet);
                    
                    // Add marker
                    RunOnUiThread(() => _map.AddMarker(mk));
					
					// We create the circle marker
					CircleOptions circleOptions = new CircleOptions ();
					circleOptions.InvokeCenter(location);
					circleOptions.InvokeRadius(gcn.distance * 1000);
					circleOptions.InvokeFillColor(Color.Argb(60, c.R, c.G, c.B));
                    //circleOptions.InvokeStrokeColor(Color.Argb(60, c.R, c.G, c.B));
                    circleOptions.InvokeStrokeWidth(2.0f);
					
					// And we update the markerslocations with the bounding box of the circle
					BoundingBox bb = GCStuffs.GetBoundingBox(new MapPoint { Latitude = location.Latitude, Longitude = location.Longitude}, gcn.distance);
					markerslocations.Add(new LatLng(bb.MinPoint.Latitude, bb.MinPoint.Longitude));
					markerslocations.Add(new LatLng(bb.MaxPoint.Latitude, bb.MaxPoint.Longitude));
					
					// Create on map
                    RunOnUiThread(() => _map.AddCircle(circleOptions).Visible = true);
				}

                // Zoom map to fit
                if (markerslocations.Count != 0)
                    RunOnUiThread(() => FitAllMarkers(markerslocations));

                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // All right!
                if (_Canceled)
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Canceled), ToastLength.Short).Show());
                else
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Success), ToastLength.Short).Show());
			}
			catch(Exception)
			{
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Crap
                RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Error), ToastLength.Short).Show());
			}
		}

        private void FitAllMarkers(List<LatLng> markerslocations)
        {
            LatLngBounds.Builder builder = new LatLngBounds.Builder();
            foreach (LatLng item in markerslocations)
            {
                builder.Include(item);
            }

            LatLngBounds bounds = builder.Build();
            CameraUpdate cu = CameraUpdateFactory.NewLatLngBounds(bounds, 100);
            _map.AnimateCamera(cu);
        }

        // Back button pressed
        public override void OnBackPressed()
        {
            var builder = new AlertDialog.Builder(this);
            builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmGoBack));
            builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) =>
            {
                // Execute cancel operation
                GoodBye();
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
            builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmGoBack));
            builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) =>
            {
                // Execute cancel operation
                GoodBye();
            });
            builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) =>
            {
                // do something on Cancel click
            });
            builder.Create().Show();
        }
    }
}