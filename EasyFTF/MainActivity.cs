using System;
using System.Threading;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Content.PM;
using System.Linq;
using Android.Preferences;

namespace EasyFTF
{
    [Activity(Label = "@string/ApplicationName", ScreenOrientation = ScreenOrientation.Portrait, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
		// Activity code :
		// 1 : configure
		// 2 : create
		
        // For progress cancel
        public bool _Canceled = false;

        // Holds cookiejar in particular
        // We keep this as a persistant object to save time on GC.com request (keep the same cookie once valid)
        GCStuffs _gcstuffs = null;
		
		// Internal object that holds all the retrieven notifications
        List<Notif> _notifs = new List<Notif>();
		
		// Adapter used to display notifications
        NotifAdapter _fa;
		
		// Login on GC.com
		// Initially empty, can be manually configured or retrieven from exportdata
        String _login = "";
		
		// Password on GC.com, not encrypted
		// Initially empty, can be manually configured or retrieven from exportdata
        String _password = "";

        // List of emails, fetched only at startup and on configuration change
        List<String> _emails = null;

        // For sorting mechanism
        enum SortType
        {
            Age = 1,
            Name = 2,
            Type = 3,
            None = 4
        };

        // By default we get list by age (id) in decreasing order
        SortType _currentSort = SortType.Age;
        bool _sortDirectionIncrease = false;

        // Creation method
        protected override void OnCreate(Bundle bundle)
        {
            // Base creation
            base.OnCreate(bundle);

            // Modify title
            this.Title = this.Title + " " + PackageManager.GetPackageInfo(PackageName, 0).VersionName + " - ";
#if (FULL)
            this.Title += this.Resources.GetString(Resource.String.Full);
#endif
#if (LITE)
			this.Title += this.Resources.GetString(Resource.String.Lite);
#endif

            // Create GCStuffs instance (do nothing except creating object)
            _gcstuffs = new GCStuffs();

            // Get account data
            GetAccountInformation();

            // We retrieved sorting type from preferences
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
            _currentSort = (SortType)(prefs.GetInt("sorttype", (int)(SortType.Age)));
            _sortDirectionIncrease = prefs.GetBoolean("sortdirection", false);

            // We display the main view
            bool accountconfigured = SetMainView();

            // Launch configure if no configuration
            if (!accountconfigured)
            {
                Configure();
            }
        }
        
        private void GetAccountInformation()
		{
            // we read data from configuration if exists
            List<String> conf = GCStuffs.LoadDataString();
            if ((conf != null) &&(conf.Count >= 2))
            {
				// Configuration is valid, populate login & password
                _login = conf[0];
                _password = conf[1];
            }
		}
		
        private bool SetMainView()
        {
			// Check if internet access is available
			if (!GCStuffs.CheckNetworkAccess(this))
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoInternet),ToastLength.Short).Show();
			}
			
			// Right before displaying the view, check GC account stuff (time consuming)
			// And force cookiejar generation, and store cookiejar inside _gcstuffs
			bool accountconfigured = _gcstuffs.CheckGCAccount(_login, _password, true, this);
			
            // Set our view from the "Main" layout resource
            SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            ImageButton button1 = FindViewById<ImageButton>(Resource.Id.Update);
            button1.Click += Update_Click;
            ImageButton button2 = FindViewById<ImageButton>(Resource.Id.Add);
            button2.Click += Add_Click;
            ImageButton button3 = FindViewById<ImageButton>(Resource.Id.Configure);
            button3.Click += Configure_Click;
            ImageButton button4 = FindViewById<ImageButton>(Resource.Id.MenuSel);
            button4.Click += MenuSel_Click;
            ImageButton button5 = FindViewById<ImageButton>(Resource.Id.Quit);
            button5.Click += Quit_Click;
			ImageButton button6 = FindViewById<ImageButton>(Resource.Id.About);
            button6.Click += About_Click;
		

            // Create adapter to display listview items (notifications)
            ListView lvItems = FindViewById<ListView>(Resource.Id.lvItems);
            _fa = new NotifAdapter(this, new List<Notif>());
            lvItems.Adapter = _fa;
            lvItems.ItemClick += LvItems_ItemClick;

            // Update hmi and populate list
			UpdateHMIAndPopulate(accountconfigured);
			
			return accountconfigured;
        }

		// update buttons and populate list if possible
		private void UpdateHMIAndPopulate(bool accountconfigured)
		{
			// Textview to diplay good or bad account information
			TextView lblAccountInfo = FindViewById<TextView>(Resource.Id.lblAccountInfo);

            // Get buttons
            ImageButton button1 = FindViewById<ImageButton>(Resource.Id.Update);
            ImageButton button2 = FindViewById<ImageButton>(Resource.Id.Add);
            ImageButton button4 = FindViewById<ImageButton>(Resource.Id.MenuSel);
            
			// We quickly (re)check validity of GC account information
            if (!accountconfigured)
            {
				// No valid information,
				// Deactivate Update, Add, Delete. Keep Configure
                button1.Enabled = false;
                button2.Enabled = false;
                button4.Enabled = false;
				lblAccountInfo.Text = this.Resources.GetString(Resource.String.AccountConfigure);
            }
            else
            {
                // Valid information !
				// Activate Update, Add, Delete. Keep Configure
                button1.Enabled = true;
                button2.Enabled = true;
                button4.Enabled = true;
				lblAccountInfo.Text = this.Resources.GetString(Resource.String.AccountGood) + " " + _login;

                // Get list of emails first !
                _emails = _gcstuffs.GetListOfEmails();

                // Start an update...
                Update();
            }
		}
		
		// Thrown when a listview item is clicked
        private void LvItems_ItemClick(object sender, AdapterView.ItemClickEventArgs e)
        {
			// We toggle check status of the clicked item
            Notif sel = _notifs[e.Position];
            sel.Checked = !sel.Checked;
			
			// Don't forget to notify the adapter to force refresh
            _fa.NotifyDataSetChanged();
        }

		// Perform selected notifications deletion
		private void Delete()
		{
			// Check if internet access is available
			if (!GCStuffs.CheckNetworkAccess(this))
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoInternet),ToastLength.Short).Show();
				return;
			}
			
			// List of notifications ids to delete
			List<String> ids = GetSelectedIds();
			
			// At last one selection ?
			if (ids.Count == 0)
			{
				// Cancel deletion since no type
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoNotif),ToastLength.Short).Show();
				return;
			}

            // Launch application inside a progress bar
            ProgressDialog progressDialog = new ProgressDialog(this);
            progressDialog.SetProgressStyle(ProgressDialogStyle.Horizontal);
            progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblDeleteInProgress));
            progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
            progressDialog.Progress = 0;
            progressDialog.Max = ids.Count;
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

            ThreadPool.QueueUserWorkItem(o => DeleteNotificationImpl(progressDialog, ids));
		}
		
		// Perform selected notifications toggle
		private void Toggle()
		{
			// Check if internet access is available
			if (!GCStuffs.CheckNetworkAccess(this))
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoInternet),ToastLength.Short).Show();
				return;
			}
			
			// List of notifications ids to toggle
			// All that is selected will have its toggle activation status toggled
			List<String> ids = GetSelectedIds();
			
			// At last one selection ?
			if (ids.Count == 0)
			{
				// Cancel deletion since no type
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoNotif),ToastLength.Short).Show();
				return;
			}

            // Launch application inside a progress bar
            ProgressDialog progressDialog = new ProgressDialog(this);
            progressDialog.SetProgressStyle(ProgressDialogStyle.Horizontal);
            progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblToggleInProgress));
            progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
            progressDialog.Progress = 0;
            progressDialog.Max = ids.Count;
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

            ThreadPool.QueueUserWorkItem(o => ToggleNotificationImpl(progressDialog, ids));
		}
		
		// Do the job of deletion
		private void DeleteNotificationImpl(ProgressDialog progressDialog, List<String> ids)
		{
			// We delete the notifications
            if (_gcstuffs.DeleteNotifications(this, progressDialog, ids))
            {
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Success, so we rebuild the notification list
                // This one will also create a progressdialog
                RunOnUiThread(() => Update());

                // All right!
                if (_Canceled)
                {
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Canceled), ToastLength.Short).Show());
                }
                else
                {
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Success), ToastLength.Short).Show());
                }
            }
			else
			{
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Crap
                RunOnUiThread(() => Toast.MakeText(this,this.Resources.GetString(Resource.String.Error),ToastLength.Short).Show());
			}
		}
		
		// Do the job of toggling
		private void ToggleNotificationImpl(ProgressDialog progressDialog, List<String> ids)
		{
			// We toggle the notifications
            if (_gcstuffs.ToggleNotifications(this, progressDialog, ids))
            {
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Success, so we rebuild the notification list
                // This one will also create a progressdialog
                RunOnUiThread(() => Update());

                // All right!
                if (_Canceled)
                {
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Canceled), ToastLength.Short).Show());
                    // Don't go to main activity !!!
                }
                else
                {
                    RunOnUiThread(() => Toast.MakeText(this, this.Resources.GetString(Resource.String.Success), ToastLength.Short).Show());
                }
            }
			else
			{
                // Kill progressdialog (we are in UI thread already, good)
                RunOnUiThread(() => progressDialog.Hide());

                // Crap
                RunOnUiThread(() => Toast.MakeText(this,this.Resources.GetString(Resource.String.Error),ToastLength.Short).Show());
			}
		}
		
		// Delete button clicked, call Delete function
        private void DeleteSelection()
        {
			// Check that we have at least one thing selected
			if (!DoWeHaveSelection())
				return;
			
			var builder = new AlertDialog.Builder(this);
			builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmDelete));
			builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) => 
			{
				// we delete notifications
				Delete();
			});
			builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) => 
			{ 
				// do something on Cancel click
			});
			builder.Create().Show();
            
        }

		// get list of selected ids
		private List<String> GetSelectedIds()
		{
			// List of notifications ids
			// All that is selected
			List<String> ids = new List<string>();
			
			// Parse notifications and keep checked ones
            foreach(Notif n in _notifs)
            {
                if (n.Checked)
                {
                    ids.Add(n.Id);
                }
            }
			return ids;			
		}
		
		// Update coord button clicked
        private void UpdateCoordSelection()
        {
#if (LITE)
            Toast.MakeText(this, this.Resources.GetString(Resource.String.OnlyFullUpdateCoord), ToastLength.Long).Show();
            return;
#endif
#if (FULL)
			// Check if something is selected
			if (!DoWeHaveSelection())
				return;
			
			// Check if internet access is available
			if (!GCStuffs.CheckNetworkAccess(this))
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoInternet),ToastLength.Short).Show();
				return;
			}
			
			// List of notifications ids
			// All that is selected
			List<String> ids = GetSelectedIds();
			
			// Create intent to launche configuration activity
            var intent = new Intent(this, typeof(UpdateCoordActivity));
			
			// Call activity
			intent.PutStringArrayListExtra("selectionids", ids);
			
			// Start the activity waiting for a result
			StartActivityForResult (intent, 3); // 3 for update coords
#endif
		}

		// Show on map button clicked
        private void ShowOnMapSelection()
        {
#if (LITE)
            Toast.MakeText(this, this.Resources.GetString(Resource.String.OnlyFullShowOnMap), ToastLength.Long).Show();
            return;
#endif
#if (FULL)
			// Check if something is selected
			if (!DoWeHaveSelection())
				return;
				
			// Check if internet access is available
			if (!GCStuffs.CheckNetworkAccess(this))
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoInternet),ToastLength.Short).Show();
				return;
			}
			
			// List of notifications ids
			// All that is selected
			List<String> ids = GetSelectedIds();
			
			// Create intent to launch show on map activity
            var intent = new Intent(this, typeof(ShowOnMapActivity));
			
			// Call activity
			intent.PutStringArrayListExtra("selectionids", ids);
			
			// Start the activity waiting for a result
			StartActivityForResult (intent, 4); // 4 for show on map
#endif
		}
		
		// check if we have a selection
		private bool DoWeHaveSelection()
		{
			// Parse notifications and keep checked ones
            bool hasselection = false;
            foreach(Notif n in _notifs)
            {
                if (n.Checked)
                {
                    hasselection = true;
					break;
                }
            }

			// At last one selection ?
			if (!hasselection)
			{
				// Cancel deletion since no type
				Toast.MakeText(this,this.Resources.GetString(Resource.String.NoNotif),ToastLength.Short).Show();
				return false;
			}
			return true;
		}
		
		// Toggle button clicked, call Toggle function
        private void ToggleSelection()
        {
#if (LITE)
            Toast.MakeText(this, this.Resources.GetString(Resource.String.OnlyFullNotif), ToastLength.Long).Show();
            return;
#endif
#if (FULL)
            // Check that we have at least one thing selected
            if (!DoWeHaveSelection())
				return;
			
			var builder = new AlertDialog.Builder(this);
			builder.SetMessage(this.Resources.GetString(Resource.String.ConfirmToggle));
			builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) => 
			{
				// we toggle notifications
				Toggle();
			});
			builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) => 
			{ 
				// do something on Cancel click
			});
			builder.Create().Show();
#endif  
        }
		
		// Popup menu
		private void MenuSel_Click(object sender, EventArgs e)
        {
            ImageButton btnmenu = FindViewById<ImageButton>(Resource.Id.MenuSel);
			PopupMenu menu = new PopupMenu (this, btnmenu);
			menu.Inflate (Resource.Menu.top_menus);

			menu.MenuItemClick += (s1, arg1) => {
				if (arg1.Item.ItemId == Resource.Id.menu_toggle)
				{
					ToggleSelection();
				}
                else if (arg1.Item.ItemId == Resource.Id.menu_sort)
                {
                    SortMenu();
                }
                else if (arg1.Item.ItemId == Resource.Id.menu_update)
				{
					UpdateCoordSelection();
				}
				else if (arg1.Item.ItemId == Resource.Id.menu_showonmap)
				{
					ShowOnMapSelection();
				}
				else if (arg1.Item.ItemId == Resource.Id.menu_delete)
				{
					DeleteSelection();
				}
                else if (arg1.Item.ItemId == Resource.Id.menu_selectall)
                {
                    SelectAll(true);
                }
                else if (arg1.Item.ItemId == Resource.Id.menu_deselectall)
                {
                    SelectAll(false);
                }
            };

			menu.DismissEvent += (s2, arg2) => {
				// Nothing when dismissed
			};
			menu.Show ();
        }

        private void SortMenu()
        {
            ImageButton btnmenu = FindViewById<ImageButton>(Resource.Id.MenuSel);
            PopupMenu menu = new PopupMenu(this, btnmenu);
            menu.Inflate(Resource.Menu.sort_menu);
            switch (_currentSort)
            {
                case SortType.Age:
                    menu.Menu.FindItem(Resource.Id.menu_sort_age).SetChecked(true);
                    break;
                case SortType.Name:
                    menu.Menu.FindItem(Resource.Id.menu_sort_name).SetChecked(true);
                    break;
                case SortType.Type:
                    menu.Menu.FindItem(Resource.Id.menu_sort_type).SetChecked(true);
                    break;
                default:
                    break;
            }

            menu.MenuItemClick += (s1, arg1) => {
                if (arg1.Item.ItemId == Resource.Id.menu_sort_age)
                {
                    SortList(SortType.Age);
                    arg1.Item.SetChecked(true);
                }
                else if (arg1.Item.ItemId == Resource.Id.menu_sort_name)
                {
                    SortList(SortType.Name);
                    arg1.Item.SetChecked(true);
                }
                else if (arg1.Item.ItemId == Resource.Id.menu_sort_type)
                {
                    SortList(SortType.Type);
                    arg1.Item.SetChecked(true);
                }
            };

            menu.DismissEvent += (s2, arg2) => {
                // Nothing when dismissed
            };
            menu.Show();
        }

        // The real sort
        private void SortList(SortType type)
        {
            if (_currentSort == type)
            {
                // On inverse l'ordre
                _sortDirectionIncrease = !_sortDirectionIncrease;
            }
            else
            {
                // On remet dans l'ordre croissant
                _sortDirectionIncrease = true;
            }

            // On assigne le nouveau tri
            _currentSort = type;

            // On sauve les préférences
            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(this);
            ISharedPreferencesEditor editor = prefs.Edit();
            editor.PutBoolean("sortdirection", _sortDirectionIncrease);
            editor.PutInt("sorttype", (int)_currentSort);
            editor.Apply();        // applies changes asynchronously on newer APIs

            PerformSort();
        }

        private void PerformSort()
        {
            List<Notif> SortedList = null;
            switch (_currentSort)
            {
                case SortType.Age:
                    SortedList = _notifs.OrderBy(o => o.Id).ToList();
                    break;
                case SortType.Name:
                    SortedList = _notifs.OrderBy(o => o.Name).ToList();
                    break;
                case SortType.Type:
                    SortedList = _notifs.OrderBy(o => o.Type).ToList();
                    break;
                default:
                    break;
            }

            if (SortedList != null)
            {
                // On regarde le sens du tri
                if (!_sortDirectionIncrease)
                    SortedList.Reverse();
                // On assigne la bonne liste
                _notifs = SortedList;

                // Si oui, on met à jour l'adapter, et on trie
                _fa.DisplayedNotifs = _notifs;
                _fa.NotifyDataSetChanged();
                //InvalidateOptionsMenu();
            }
        }

        // select/deselectall
        private void SelectAll(bool select)
        {
            // Changed selection state
            foreach (var n in _notifs)
                n.Checked = select;

            // Don't forget to notify the adapter to force refresh
            _fa.NotifyDataSetChanged();
        }

		// Configure button clicked, call Configure function
        private void Configure_Click(object sender, EventArgs e)
        {
            Configure();
        }

		// Launch configuration activity
        private void Configure()
        {
			// Create intent to launche configuration activity
            var intent = new Intent(this, typeof(ConfigureActivity));
			
			// Start the activity waiting for a result
			StartActivityForResult (intent, 1); // 1 for configure
        }

		// Result of an activity, back to work!
		protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
		{
		  base.OnActivityResult(requestCode, resultCode, data);
		  if (resultCode == Result.Ok) 
		  {
			if (requestCode == 1) // Configure
			{
				// User successfully configured his account
				// Update login & password with data
				GetAccountInformation();
				
				// And force cookiejar generation, and store cookiejar inside _gcstuffs
				bool accountconfigured = _gcstuffs.CheckGCAccount(_login, _password, true, this);
			
				// Update hmi and populate list
				UpdateHMIAndPopulate(accountconfigured);
			}
			else if (requestCode == 2) // Creation
			{
				// Back from a creation
				// We created something so update
				// Start an update...
                Update();
			}
			// else we do nothing for other callbacks
		  }
		}
		
		// Launch the Add activity
        private void Add_Click(object sender, EventArgs e)
        {
			Add();
        }

		private void Add()
		{
			// Check if we do not exceed maximum number of notifications
			int nbNotifs = 0;
			if (_fa != null)
			{
				nbNotifs = _fa.DisplayedNotifs.Count;
			}
			if (nbNotifs == 40)
			{
				Toast.MakeText(this,this.Resources.GetString(Resource.String.MaxNotifReached),ToastLength.Long).Show();
				return;
			}
			
			// Create intent to launch creation activity
            var intent = new Intent(this, typeof(CreationActivity));

            // parameters will be list of emails if valid
            if (_emails.Count != 0)
                intent.PutStringArrayListExtra("emails", _emails);

            // And add list of notifications already created
            List<string> parameters = new List<string>();
            parameters.Add(nbNotifs.ToString());
            intent.PutStringArrayListExtra("nbNotifs", parameters);
			
            // Start the activity waiting for a result
            StartActivityForResult (intent, 2); // 2 for creation
		}
		
		// Launch the update process
        private void Update_Click(object sender, EventArgs e)
        {
            Update();
        }

		// Update the list of notifications
        private void Update()
        {
            // Launch application inside a progress bar
            ProgressDialog progressDialog = new ProgressDialog(this);
            progressDialog.SetProgressStyle(ProgressDialogStyle.Spinner);
            progressDialog.SetMessage(this.Resources.GetString(Resource.String.LblUpdateInProgress));
            progressDialog.SetTitle(this.Resources.GetString(Resource.String.OperationInProgress));
            progressDialog.Indeterminate = true;
            progressDialog.SetCancelable(false);
            progressDialog.Show();

            ThreadPool.QueueUserWorkItem(o => UpdateImpl(progressDialog));
        }

		private void UpdateImpl(ProgressDialog progressDialog)
		{
            // Check if we have a valid _gcstuffs
            if (_gcstuffs != null)
            {
				// Check if account is valid
                if (_gcstuffs.CheckGCAccount(_login, _password, false, this))
                {
					// Retrieve list of notifications from GC.com
                    _notifs = _gcstuffs.ListNotifications();

                    // Pass these notifications to the adapter
                    //_fa.DisplayedNotifs = _notifs;
                    // Notify adapter that notifications changed to trigger refresh
                    //RunOnUiThread(() => _fa.NotifyDataSetChanged());

                    // Now we sort if needed
                    RunOnUiThread(() => PerformSort());

                    // Kill progressdialog (we are in UI thread already, good)
                    RunOnUiThread(() => progressDialog.Hide());
                }
                else
                {
                    // Kill progressdialog (we are in UI thread already, good)
                    RunOnUiThread(() => progressDialog.Hide());

                    // Crap, no valid account information, we launch the configure activity
                    RunOnUiThread(() => Toast.MakeText(this,this.Resources.GetString(Resource.String.AccountConfigure),ToastLength.Short).Show());
                    Configure();
                }
            }
		}
		
		// Quit the application
		public void Quit()
        {
			var builder = new AlertDialog.Builder(this);
			builder.SetMessage(this.Resources.GetString(Resource.String.QuitMsg));
			builder.SetPositiveButton(this.Resources.GetString(Resource.String.BtnYes), (s, ev) => 
			{
				// Kill the process
				Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
				base.OnBackPressed();
			});
			builder.SetNegativeButton(this.Resources.GetString(Resource.String.BtnNo), (s, ev) => 
			{ 
				// do nothing
			});
			builder.Create().Show();
        }

		// We clicked on About button
        private void About_Click(object sender, EventArgs e)
        {
			// Create intent to launch about activity
            var intent = new Intent(this, typeof(AboutActivity));
			
			// Some parameters
			List<string> parameters = new List<string>();
            parameters.Add(PackageManager.GetPackageInfo(PackageName, 0).VersionName);
#if (FULL)
            parameters.Add(this.Resources.GetString(Resource.String.Full));
#endif
#if (LITE)
			parameters.Add(this.Resources.GetString(Resource.String.Lite));
#endif
            intent.PutStringArrayListExtra("about", parameters);
			
			// Start the activity waiting for a result
			StartActivityForResult (intent, 5); // 4 for about
        }

		
		// We clicked on Quit button
        private void Quit_Click(object sender, EventArgs e)
        {
            Quit();
        }
        
        // We quit application when backpressed
        public override void OnBackPressed()
        {
            Quit();
        }
    }
}

