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
	// Simple adapter to display cache types on ListView
    class TypeCacheAdapter : BaseAdapter<TypeCache>
    {
		// List of cache types, kept internally of this adapter in parallel of CreationActivity
        List<TypeCache> typecaches;
		
		// Associated activity context
        Activity context;

		// Get/Set for type caches
        public List<TypeCache> DisplayedTypeCaches
        {
            get
            {
                return typecaches;
            }
            set
            {
                typecaches = value;
            }
        }

		// Constructor, does nothing really interesting
        public TypeCacheAdapter(Activity context, List<TypeCache> typecaches) : base()
        {
            this.context = context;
            this.typecaches = typecaches;

        }

		// Overriden, nothing fancy
        public override long GetItemId(int position)
        {
            return position;
        }
		
		// Overriden, returns cache type at given position
        public override TypeCache this[int position]
        {
            get { return typecaches[position]; }
        }
		
		// Overriden, returns cache type count
        public override int Count
        {
            get { return typecaches.Count; }
        }

		// Create view for an item (cache type)
        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            
            ImageView view = null;
            if (convertView == null)
            {  // if it's not recycled, initialize some attributes
                view = new ImageView(context);
                //view.LayoutParameters = new GridView.LayoutParams(85, 85); //85, 85);
                //view.SetScaleType(ImageView.ScaleType.FitCenter); //ImageView.ScaleType.CenterCrop);
                view.SetPadding(10, 10, 10, 10); //8, 8, 8, 8);
            }
            else
            {
                view = (ImageView)convertView;
            }

            // Get type cache at given position
            TypeCache f = typecaches[position];

            // Fill image
            view.SetImageResource(f.ResourceId);

            // Color depending on check status
            if (f.Checked)
            {
                view.SetBackgroundColor(Color.RoyalBlue);

            }
            else
            {
                view.SetBackgroundColor(Color.Transparent);
            }

            return view;
            
            /*
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) // otherwise create a new one
            {
				// We use a custom layout for this view
                view = context.LayoutInflater.Inflate(Resource.Layout.list_item_layout2, null);
            }

			// Get type cache at given position
            TypeCache f = typecaches[position];

            // Fill name
            TextView txtName = view.FindViewById<TextView>(Resource.Id.textView_Name);
            txtName.Text = (1 + position).ToString() + "- " + f.Type;

			// Fill image
            view.FindViewById<ImageView>(Resource.Id.imageView_icon).SetImageResource(f.ResourceId);


            // Color depending on check status
            if (f.Checked)
            {
                view.SetBackgroundColor(Color.RoyalBlue);

            }
            else
            {
                view.SetBackgroundColor(Color.Transparent);
            }
            
            // Returns the view. All done
            return view;
            */
        }
    }
}