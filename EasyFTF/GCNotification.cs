using System;
using System.Collections.Generic;

using Android.Gms.Maps.Model;
using Android.Graphics;

namespace EasyFTF
{
    public class GCNotification
    {
        public double dlat = Double.MaxValue;
        public double dlon = Double.MaxValue;
        public int distance = int.MaxValue;
        public String name = "";
        public String email = "";
        public Tuple<int, String, List<String>, int> data = null;
        public bool checknotif = false;

        public override string ToString()
        {
            return string.Format("[GCNotification Dlat={0}, Dlon={1}, Distance={2}, Name={3}, Email={4}, Checknotif={5}, Data={6}]", dlat, dlon, distance, name, email, checknotif, data);
        }

		public static int GetResourceId(String type)
		{
			if (type == "Traditional Cache")
				return Resource.Drawable.Tradi;
			else if (type == "Cache In Trash Out Event")
				return Resource.Drawable.CITO;
			else if (type == "Earthcache")
				return Resource.Drawable.Earth;
			else if (type == "Event Cache")
				return Resource.Drawable.Event;
			else if (type == "Giga-Event Cache")
				return Resource.Drawable.Giga;
			else if (type == "Letterbox Hybrid")
				return Resource.Drawable.Letterbox;
			else if (type == "Mega-Event Cache")
				return Resource.Drawable.Mega;
			else if (type == "Multi-cache")
				return Resource.Drawable.Multi;
			else if (type == "Unknown Cache")
				return Resource.Drawable.Unknown;
			else if (type == "Wherigo Cache")
				return Resource.Drawable.Wherigo;
            else if (type == "Virtual Cache")
                return Resource.Drawable.Virtual;
            else
                return Resource.Drawable.Unsupported;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public String GetTypeKeyInEnglish()
        {
            if (data.Item1 == 137) return "Earthcache";
            if (data.Item1 == 6) return "Event Cache";
            if (data.Item1 == 13) return "Cache In Trash Out Event";
            if (data.Item1 == 7005) return "Giga-Event Cache";
            if (data.Item1 == 4738) return "Groundspeak Block Party";
            if (data.Item1 == 3774) return "Groundspeak Lost and Found Celebration";
            if (data.Item1 == 3653) return "Lost and Found Event Cache";
            if (data.Item1 == 453) return "Mega-Event Cache";
            if (data.Item1 == 1304) return "GPS Adventures Exhibit";
            if (data.Item1 == 5) return "Letterbox Hybrid";
            if (data.Item1 == 12) return "Locationless (Reverse) Cache";
            if (data.Item1 == 3) return "Multi-cache";
            if (data.Item1 == 9) return "Project APE Cache";
            if (data.Item1 == 2) return "Traditional Cache";
            if (data.Item1 == 8) return "Unknown Cache";
            if (data.Item1 == 3773) return "Groundspeak HQ";
            if (data.Item1 == 4) return "Virtual Cache";
            if (data.Item1 == 11) return "Webcam Cache";
            if (data.Item1 == 1858) return "Wherigo Cache";
            return "";
        }

        public void GetIcon(ref float b, ref Color c)
        {
            /*
float	HUE_AZURE	
float	HUE_BLUE	
float	HUE_CYAN	
float	HUE_GREEN	
float	HUE_MAGENTA	
float	HUE_ORANGE	
float	HUE_RED	
float	HUE_ROSE	
float	HUE_VIOLET	
float	HUE_YELLOW	
			*/

/*
< option value = "137" > Earthcache </ option >
< option value = "6" > Event Cache </ option >
< option value = "13" > Cache In Trash Out Event</ option >
< option value = "7005" > Giga - Event Cache </ option >
< option value = "4738" > Groundspeak Block Party</ option >
< option value = "3774" > Groundspeak Lost and Found Celebration</ option >
< option value = "3653" > Lost and Found Event Cache</ option >
< option value = "453" > Mega - Event Cache </ option >
< option value = "1304" > GPS Adventures Exhibit</ option >
< option value = "5" > Letterbox Hybrid </ option >
< option value = "12" > Locationless(Reverse) Cache </ option >
< option value = "3" > Multi - cache </ option >
< option value = "9" > Project APE Cache</ option >
< option value = "2" > Traditional Cache </ option >
< option value = "8" > Unknown Cache </ option >
< option value = "3773" > Groundspeak HQ </ option >
< option value = "4" > Virtual Cache </ option >
< option value = "11" > Webcam Cache </ option >
< option value = "1858" > Wherigo Cache </ option > 
*/
            if (data.Item1 == 137)
            {
                c = Color.Purple;
                b = BitmapDescriptorFactory.HueViolet;
            }
            else if (data.Item1 == 5)
            {
                c = Color.Green;
                b = BitmapDescriptorFactory.HueGreen;
            }
            else if (data.Item1 == 3)
            {
                c = Color.Yellow;
                b = BitmapDescriptorFactory.HueYellow;
            }
            else if (data.Item1 == 8)
            {
                c = Color.Blue;
                b = BitmapDescriptorFactory.HueBlue;
            }
            else if (data.Item1 == 2)
            {
                c = Color.Green;
                b = BitmapDescriptorFactory.HueGreen;
            }
            else if (data.Item1 == 4)
            {
                c = Color.Purple;
                b = BitmapDescriptorFactory.HueViolet;
            }
            else if (data.Item1 == 11)
            {
                c = Color.Purple;
                b = BitmapDescriptorFactory.HueViolet;
            }
            else if (data.Item1 == 1858)
            {
                c = Color.Blue;
                b = BitmapDescriptorFactory.HueBlue;
            }
            else
            {
                // Tout le reste c'est du type event
                c = Color.Red;
                b = BitmapDescriptorFactory.HueRed;
            }
        }
    }
}