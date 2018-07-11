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
	// Simple class that defines a notification
    public class Notif
    {
        // Number in initial list
        public int Number = 0;

		// Notification name (user defined)
        public String Name = "";
		
		// Cache type (Earthcache, Traditional Cache, etc...)
        public String Type = "";
		
		// Notification Id, used to create requests on GC.com
        public String Id = "";
		
		// Notification type : cache notifications handled (Published listing, etc...)
        public String NotifType = "";
		
		// Notification is enabled (active) or not
		public bool Enabled = false;
		
		// A user readable information on notification (Type & NotifType)
        public String Info = "";
		
		// Indicates if notification is checked on list
        public bool Checked = false;

		// Assicated image resource id
        public int ResourceId = 0;
    }
}