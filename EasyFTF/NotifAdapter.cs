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
using Android.Graphics;

namespace EasyFTF
{
	// Simple adapter to display notifications on ListView
    class NotifAdapter : BaseAdapter<Notif>
    {
		// List of notifications, kept internally of this adapter in parallel of MainActivity
        List<Notif> notifs;
		
		// Associated activity context
        Activity context;

		// Get/Set for notifications
        public List<Notif> DisplayedNotifs
        {
            get
            {
                return notifs;
            }
            set
            {
                notifs = value;
            }
        }

		// Constructor, does nothing really interesting
        public NotifAdapter(Activity context, List<Notif> notifs) : base()
        {
            this.context = context;
            this.notifs = notifs;

        }

		// Overriden, nothing fancy
        public override long GetItemId(int position)
        {
            return position;
        }
		
		// Overriden, returns notification at given position
        public override Notif this[int position]
        {
            get { return notifs[position]; }
        }
		
		// Overriden, returns notification count
        public override int Count
        {
            get { return notifs.Count; }
        }

		// Create view for an item (notification
        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) // otherwise create a new one
            {
				// We use a custom layout for this view
                view = context.LayoutInflater.Inflate(Resource.Layout.list_item_layout, null);
            }

			// Get notification at given position
            Notif f = notifs[position];

            // Fill name
            TextView txtName = view.FindViewById<TextView>(Resource.Id.textView_Name);
            txtName.Text = f.Number.ToString("00") + "- " + f.Name;
			
			// Fill information
            view.FindViewById<TextView>(Resource.Id.textView_Info).Text = f.Info;
			
			// Fill image
            ImageView img = view.FindViewById<ImageView>(Resource.Id.imageView_icon);
			img.SetImageResource(f.ResourceId);
			
            // Checbox depending on check status
			if (f.Checked)
            {
                //txtName.SetCompoundDrawablesWithIntrinsicBounds(Resource.Drawable.@checked, 0, 0, 0);
                view.SetBackgroundColor(Color.RoyalBlue);
            }
            else
            {
                //txtName.SetCompoundDrawablesWithIntrinsicBounds(0, 0, 0, 0);
                view.SetBackgroundColor(Color.Transparent);
            }

            // Color depending on enabled status
            ImageView img2 = view.FindViewById<ImageView>(Resource.Id.imageView_prefix);
            if (f.Enabled)
            {
                //txtName.SetTextColor(Android.Graphics.Color.Green);
				img2.SetImageResource(Resource.Drawable.@bulletgreen);
            }
            else
            {
                //txtName.SetTextColor(Android.Graphics.Color.Red);
				img2.SetImageResource(Resource.Drawable.bulletred);
            }
			
			// Returns the view. All done
            return view;
        }
    }
}