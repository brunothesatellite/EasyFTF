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

namespace EasyFTF
{
	// Simple class that defines a cache type
    public class TypeCache
    {
		// Cache type (Earthcache, Traditional Cache, etc...)
        public String Type = "";
		
		// Indicates if cache type is checked on list
        public bool Checked = false;

		// Associated image resource id
        public int ResourceId = 0;
		
		// constructor
		public TypeCache(String t, int res)
		{
			Type = t;
			ResourceId = res;
		}
    }
}