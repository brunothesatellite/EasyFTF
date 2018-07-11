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

namespace EasyFTF
{
    [Activity(Label = "@string/PickMap", ScreenOrientation = ScreenOrientation.Portrait, Icon = "@drawable/map")]
    public class PickActivity : Activity, IOnMapReadyCallback
    {
        String lat = "";
        String lon = "";
        int radius = 0;
        Circle _circle = null;
        GoogleMap _map = null;
        MarkerOptions _marker = null;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "MapLayout" layout resource
            SetContentView(Resource.Layout.MapLayout);

            // Get params
            IList<String> p = null;
            if (Intent.Extras != null)
                p = Intent.Extras.GetStringArrayList("coordinates") ?? new string[0];

            // Check validity of parameter
            if (p != null)
            {
                if (p.Count >= 2)
                {
                    // List is valid, populate login & password
                    lat = p[0];
                    lon = p[1];
                }
                if (p.Count >= 3)
                {
                    if (!Int32.TryParse(p[2], out radius))
                        radius = 0;
                }

            }

            if ((lat != "") && (lon != ""))
            {
                // Textview to diplay coordinates
                TextView lblCoord = FindViewById<TextView>(Resource.Id.lblCoord);
                String sLat2 = GCStuffs.ConvertDegreesToDDMM(GCStuffs.ConvertToDouble(lat), true);
                String sLon2 = GCStuffs.ConvertDegreesToDDMM(GCStuffs.ConvertToDouble(lon), false);
                lblCoord.Text = /*"DD° MM.MMM: " + */sLat2 + " " + sLon2;
            }

            // Create your application here
            var frag = FragmentManager.FindFragmentById<MapFragment>(Resource.Id.map);
            frag.GetMapAsync(this);

            Button button = FindViewById<Button>(Resource.Id.Save);
            button.Click += Save_Click;
            button = FindViewById<Button>(Resource.Id.BtnQuit);
            button.Click += Cancel_Click;
        }

        public String NiceCoordToString(LatLng location)
        {
            String sLat2 = GCStuffs.ConvertDegreesToDDMM(location.Latitude, true);
            String sLon2 = GCStuffs.ConvertDegreesToDDMM(location.Longitude, false);
            return /*"DD° MM.MMM: " + */sLat2 + " " + sLon2;
        }

        public void myMarkerDragEnd(object sender, GoogleMap.MarkerDragEndEventArgs e)
        {
            double dlat = e.Marker.Position.Latitude;
            double dlon = e.Marker.Position.Longitude;
            lat = dlat.ToString().Replace(',', '.');
            lon = dlon.ToString().Replace(',', '.');
            String coords = NiceCoordToString(e.Marker.Position);
            _marker.SetTitle(coords);

            CreateCircle(e.Marker.Position);

            TextView lblCoord = FindViewById<TextView>(Resource.Id.lblCoord);
            lblCoord.Text = coords;
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

                    _map.MarkerDragEnd += myMarkerDragEnd;


                    CameraPosition.Builder builder = CameraPosition.InvokeBuilder();
                    LatLng location = null;
                    if ((lat != "") && (lon != ""))
                    {
                        location = new LatLng(GCStuffs.ConvertToDouble(lat), GCStuffs.ConvertToDouble(lon));
                    }
                    else
                    {
                        // Don't forget to assign a value to lat and lon
                        // If omeone click on save coord without moving the marker, these values will be used
                        lat = "43.3";
                        lon = "5.4";
                        location = new LatLng(43.3, 5.4);
                    }
                    builder.Target(location);
                    builder.Zoom(10);// 13);
                    CameraPosition cameraPosition = builder.Build();
                    CameraUpdate cameraUpdate = CameraUpdateFactory.NewCameraPosition(cameraPosition);
                    _map.MoveCamera(cameraUpdate);

                    // Create marker
                    _marker = new MarkerOptions();
                    _marker.SetPosition(location);
                    TextView lblCoord = FindViewById<TextView>(Resource.Id.lblCoord);
                    lblCoord.Text = /*"DD° MM.MMM: " + */NiceCoordToString(location);
                    _marker.Draggable(true);
                    _marker.SetTitle(NiceCoordToString(location));
                    //_marker.SetIcon(BitmapDescriptorFactory.FromResource(geo.GetIconResourceId()));
                    _map.AddMarker(_marker);

                    // Create circle if possible
                    CreateCircle(location);

                }
                else
                {
                    // Show rationale and request permission.
                    Toast.MakeText(this, this.Resources.GetString(Resource.String.ErrorLocation), ToastLength.Short).Show();
                }
                
            }

        }

        private void CreateCircle(LatLng location)
        {
            if (_circle != null)
            {
                _circle.Remove();
                _circle = null;
            }
            if (radius > 0)
            {
                // We create the circle marker
                CircleOptions circleOptions = new CircleOptions();
                circleOptions.InvokeCenter(location);
                circleOptions.InvokeRadius(radius * 1000);
                circleOptions.InvokeFillColor(Color.Argb(60, 0, 255, 0));
                circleOptions.InvokeStrokeWidth(2.0f);
                _circle = _map.AddCircle(circleOptions);
            }
        }

        // Save button clicked
        private void Save_Click(object sender, EventArgs e)
        {
            var builder = new AlertDialog.Builder(this);
            builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmPick));
            builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) =>
            {
                // Execute confirm operation
                var intent = new Intent(this, typeof(MainActivity));
                List<string> parameters = new List<string>();
                parameters.Add(lat);
                parameters.Add(lon);
                intent.PutStringArrayListExtra("coordinates", parameters);
                SetResult(Result.Ok, intent);
                Finish();

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
            builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmCancelPick));
            builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) =>
            {
                // Execute cancel operation
                var intent = new Intent(this, typeof(MainActivity));
                List<string> parameters = new List<string>();
                parameters.Add("");
                parameters.Add("");
                intent.PutStringArrayListExtra("coordinates", parameters);
                SetResult(Result.Canceled, intent);
                Finish();
            });
            builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) =>
            {
                // do something on Cancel click
            });
            builder.Create().Show();
        }
    }
}