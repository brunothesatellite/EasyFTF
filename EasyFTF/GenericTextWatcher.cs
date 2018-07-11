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
using Android.Text;
using Java.Lang;

namespace EasyFTF
{
    class GenericTextWatcher : Android.Text.ITextWatcher
    {
        EditText et_;
        Activity ac_;

        public GenericTextWatcher(EditText et, Activity ac)
        {
            et_ = et;
            ac_ = ac;
        }


        public IntPtr Handle
        {
            get
            {
                return IntPtr.Zero; // throw new NotImplementedException();
            }
        }

        public void AfterTextChanged(IEditable s)
        {
            // update all the 3 coords formats
            et_.Text = GCStuffs.ConvertCoordinates(et_.Text);
        }

        public void BeforeTextChanged(ICharSequence s, int start, int count, int after)
        {
            //throw new NotImplementedException();
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public void OnTextChanged(ICharSequence s, int start, int before, int count)
        {
            //throw new NotImplementedException();
        }
    }
}