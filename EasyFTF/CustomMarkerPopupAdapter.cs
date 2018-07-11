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
using Android.Gms.Maps.Model;
using Android.Graphics;

namespace EasyFTF
{
    public class CustomMarkerPopupAdapter : Java.Lang.Object, GoogleMap.IInfoWindowAdapter
    {
        private LayoutInflater _layoutInflater = null;

        public CustomMarkerPopupAdapter(LayoutInflater inflater)
        {
            _layoutInflater = inflater;
        }

        public View GetInfoWindow(Marker marker)
        {
            return null;
        }

        public View GetInfoContents(Marker marker)
        {
            var customPopup = _layoutInflater.Inflate(Resource.Layout.CustomMarkerPopup, null);

            var titleTextView = customPopup.FindViewById<TextView>(Resource.Id.custom_marker_popup_title);
            if (titleTextView != null)
            {
                titleTextView.Text = marker.Title;
                titleTextView.SetTextColor(Color.Black);
            }

            var snippetTextView = customPopup.FindViewById<TextView>(Resource.Id.custom_marker_popup_snippet);
            if (snippetTextView != null)
            {
                snippetTextView.Text = marker.Snippet;
                snippetTextView.SetTextColor(Color.Black);
            }

            return customPopup;
        }
    }
}