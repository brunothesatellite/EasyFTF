using System;
using System.Collections.Generic;

using Android.App;
using Android.Widget;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Globalization;
using Android.Net;
using Android.Text;

namespace EasyFTF
{

	// Holds all GC functions, other various functions, including a cookiejar
    public class GCStuffs
    {
        // indicate if user is premium
        public bool UserIsPremium = false;

		// Authentication cookie for GC.com
		// This one is instanciated in CheckGCAccount
	    public CookieContainer _cookieJar = null;
		
		// True if saving of exportdata is in progress
		static bool _bSaving = false;
		
        /// <summary>
        /// Error value during a conversion
        /// </summary>
        static public String _sErrorValue = "#ERR";

		// Semi-axes of WGS-84 geoidal reference
		private const double WGS84_a = 6378137.0; // Major semiaxis [m]
		private const double WGS84_b = 6356752.3; // Minor semiaxis [m]
		
		// Constructor, does nothing fancy
        public GCStuffs()
        {
        }

        /// <summary>
        /// Get all subsets from a string, between 2 tags
        /// exemple:
        /// "toto is a weird guy, really is a stupid guy, I think it is a dummy guy"
        /// tag1 = "is a"
        /// tag2 = " guy"
        /// result = " weird", " stupid", " dummy"
        /// If the subset is not found, return ""
        /// </summary>
        /// <param name="tag1">beginning tag</param>
        /// <param name="tag2">end tag (if "", search will only be performed on tag1)</param>
        /// <param name="text">text to look into</param>
        /// <returns>If the subset is not found, return "", otherwise the list of matching subsets</returns>
        public static List<String> GetSnippetsFromText(String tag1, String tag2, String text)
        {
            // Recherche toutes les chaines contenues entre tag1 et tag2 dans text
            List<String> r = new List<string>();
            int istart = 0;
            int i1 = -1;
            if (text != "")
            {
                do
                {
                    i1 = text.IndexOf(tag1, istart);
                    if (i1 != -1)
                    {
                        int l1 = i1 + tag1.Length;
                        if (tag2 != "")
                        {
                            // présence d'un tag de fin
                            int i2 = text.IndexOf(tag2, i1 + 1);
                            if (i2 != -1)
                            {
                                r.Add(text.Substring(l1, i2 - l1));
                                istart = i2 + tag2.Length; // on repart pour un tour
                            }
                            else
                            {
                                // pas de tag de fin trouvé, on arrête là
                                i1 = -1;
                            }
                        }
                        else
                        {
                            // pas de tag de fin fourni
                            r.Add(text.Substring(l1));
                            i1 = -1; // on stoppe là
                        }
                    }
                }
                while (i1 != -1);

            }
            return r;
        }

        /// <summary>
        /// Get a subset from a string, between 2 tags
        /// exemple:
        /// "toto is a weird guy"
        /// tag1 = "is a"
        /// tag2 = " guy"
        /// result = " weird"
        /// If the subset is not found, return ""
        /// </summary>
        /// <param name="tag1">beginning tag</param>
        /// <param name="tag2">end tag (if "", search will only be performed on tag1)</param>
        /// <param name="text">text to look into</param>
        /// <returns>If the subset is not found, return "", otherwise a matching subset</returns>
        public static String GetSnippetFromText(String tag1, String tag2, String text)
        {
            // Recherche la première occurence d'une chaine contenue entre tag1 et tag2 dans text
            String r = "";
            if (text != "")
            {

                int i1 = text.IndexOf(tag1);
                if (i1 != -1)
                {
                    int l1 = i1 + tag1.Length;
                    if (tag2 != "")
                    {
                        int i2 = text.IndexOf(tag2, l1);
                        if (i2 != -1)
                        {
                            r = text.Substring(l1, i2 - l1);
                        }
                    }
                    else
                    {
                        r = text.Substring(l1);
                    }
                }
            }
            return r;
        }

        /// <summary>
        /// Retrieve VIEWSTATE information from a Geocaching.com webpage
        /// </summary>
        /// <param name="response">webpage to parse</param>
        /// <param name="__VIEWSTATEFIELDCOUNT">retrieved __VIEWSTATEFIELDCOUNT value</param>
        /// <param name="__VIEWSTATE">retrieved __VIEWSTATE list</param>
        /// <param name="__VIEWSTATEGENERATOR">retrieved __VIEWSTATEGENERATOR value</param>
        public static void GetViewState(String response, ref String __VIEWSTATEFIELDCOUNT, ref String[] __VIEWSTATE, ref String __VIEWSTATEGENERATOR)
        {
            // __VIEWSTATEFIELDCOUNT" value="2" />
            // id="__VIEWSTATE" value="
            // id="__VIEWSTATE1" value="
            // <input type="hidden" name="__VIEWSTATEGENERATOR" id="__VIEWSTATEGENERATOR" value="25748CED" />

            __VIEWSTATEFIELDCOUNT = GetSnippetFromText("__VIEWSTATEFIELDCOUNT\" value=\"", "\"", response);
            if (__VIEWSTATEFIELDCOUNT == "")
            {
                __VIEWSTATEFIELDCOUNT = "1";
            }
            int ivscount = Int32.Parse(__VIEWSTATEFIELDCOUNT);
            __VIEWSTATE = new string[ivscount];

            for (int i = 0; i < ivscount; i++)
            {
                if (i == 0)
                {
                    __VIEWSTATE[i] = GetSnippetFromText("id=\"__VIEWSTATE\" value=\"", "\"", response);
                }
                else
                {
                    __VIEWSTATE[i] = GetSnippetFromText("id=\"__VIEWSTATE" + i.ToString() + "\" value=\"", "\"", response);
                }
            }

            __VIEWSTATEGENERATOR = GetSnippetFromText("__VIEWSTATEGENERATOR\" value=\"", "\"", response);
        }

        /// <summary>
        /// Execute a regular expression
        /// </summary>
        /// <param name="input">input text</param>
        /// <param name="param">regular expression</param>
        /// <returns>result</returns>
        public static String DoRegex(String input, String param)
        {
            Match match = Regex.Match(input, param, RegexOptions.IgnoreCase);

            // Here we check the Match instance.
            if (match.Success)
            {
                // Finally, we get the Group value and display it.
                return match.Groups[1].Value;
            }
            else
                return "s";
        }

		// Check a GC account validity by simulating a login
		// If bForceRegeneration, do not try to recalculate cookiejar, save a HUGE amount of time
        public bool CheckGCAccount(String username, String password, bool bForceRegeneration, Activity dad)
        {
            try
            {
                if ((username == "") || (password == ""))
                {
                    return false;
                }
                else
                {

                    // Si on a deja un cookie valide et qu'on ne force pas sa régénération, alors on le retourne
                    // Gain de temps énorme
                    if ((_cookieJar != null) && (!bForceRegeneration))
                        return true;

                    /* Penser à inclure les dépendances suivantes : 
                     * using System.Net;
                     * using System.IO;
                     * using System.Web;
                     * 
                     * et les références :
                     * System.Web
                     * System.Net
                     * 
                     * Définir les variables suivantes :
                     * String username = "METTRE VOTRE LOGIN ICI";
                     * String password = "METTRE VOTRE MDP EN CLAIR ICI";
                     */

                    // Notre container de cookies
                    CookieContainer cookieJar = new CookieContainer();

                    // Authentification sur GC.com
                    // ***************************
                    
                    // TSL 1.2
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                    // Récupération des VIEWSTATE pour s'authentifier
                    string VOID_URL = "https://www.geocaching.com/account/login";
                    HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(VOID_URL);
                    objRequest.CookieContainer = cookieJar;

                    HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
                    String post_response = "";
                    using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
                    {
                        post_response = responseStream.ReadToEnd();
                        responseStream.Close();
                    }
                    int cookieCount = cookieJar.Count;

                    // On récupère le token de vérification
                    String token = GCStuffs.GetSnippetFromText("__RequestVerificationToken\" type=\"hidden\" value=\"", "\"", post_response);

                    // Préparation des données du POST
                    Dictionary<String, String> post_values = new Dictionary<String, String>();
                    post_values.Add("Username", username);
                    post_values.Add("Password", password);
                    post_values.Add("__RequestVerificationToken", token);
                    

                    // Encodage des données du POST
                    String post_string = "";
                    foreach (KeyValuePair<String, String> post_value in post_values)
                    {
                        post_string += post_value.Key + "=" + HttpUtility.UrlEncode(post_value.Value) + "&";
                    }
                    post_string = post_string.TrimEnd('&');

                    // Création de la requête pour s'authentifier
                    objRequest = (HttpWebRequest)WebRequest.Create(VOID_URL);
                    objRequest.Method = "POST";
                    objRequest.ContentLength = post_string.Length;
                    objRequest.ContentType = "application/x-www-form-urlencoded";
                    objRequest.CookieContainer = cookieJar;
                    
                    // on envoit les POST data dans un stream (écriture)
                    StreamWriter myWriter = null;
                    myWriter = new StreamWriter(objRequest.GetRequestStream());
                    myWriter.Write(post_string);
                    myWriter.Close();

                    // lecture du stream de réponse et conversion en chaine
                    objResponse = (HttpWebResponse)objRequest.GetResponse();
                    using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
                    {
                        post_response = responseStream.ReadToEnd();
                        responseStream.Close();
                    }

                    // Pour le debug, interrogation du nombre de cookies retournés et trace de cette valeur
                    // peut être supprimé
                    cookieCount = cookieJar.Count;
                    bool loggedin = true; // DEPRECATED !!!  post_response.Contains("isLoggedIn: true,");
                    String userloginfromgc = GCStuffs.GetSnippetFromText("currentUsername: \"", "\",", post_response);
                    if (loggedin && (userloginfromgc.ToLower() == username.ToLower()))
                    {
						// Un petit dernier pour la route, on vérifie si on est premium
                        UserIsPremium = false;
                        try
                        {
                            UserIsPremium = post_response.Contains("currentUserIsPremium: true");
                        }
                        catch(Exception)
                        {
                        	// On considère qu'on est premium...
                        	UserIsPremium = true;
                        }
						
						if (UserIsPremium)
						{
							_cookieJar = cookieJar;
							return true;
						}
						else
						{
                            dad.RunOnUiThread(() => Toast.MakeText(dad, dad.Resources.GetString(Resource.String.NoPremium), ToastLength.Short).Show());
                            _cookieJar = null;
							return false;
						}
                    }
                    else
                    {
                        _cookieJar = null;
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                _cookieJar = null;
                return false;
            }
        }

		// Generate post values for notification creation
        static public String GeneratePostString(String post_response, double dlat, double dlon, int distance, String name, Tuple<int, String, List<String>, int> tyo, String email, bool checknotif)
        {
            // On récupère les viewstates
            String __VIEWSTATEFIELDCOUNT = "";
            String[] __VIEWSTATE = null;
            String __VIEWSTATEGENERATOR = "";
            GetViewState(post_response, ref __VIEWSTATEFIELDCOUNT, ref __VIEWSTATE, ref __VIEWSTATEGENERATOR);

            // Préparation des données du POST
            Dictionary<String, String> post_values = new Dictionary<String, String>();
            post_values.Add("__EVENTTARGET", "");
            post_values.Add("__EVENTARGUMENT", "");
            post_values.Add("__LASTFOCUS", "");

            // Le nom                 
            post_values.Add("ctl00$ContentBody$LogNotify$tbName", name);

            // Le type
            post_values.Add("ctl00$ContentBody$LogNotify$ddTypeList", tyo.Item1.ToString());

            // IL FAUT PUBLIER POUR ACTIVER LA SUITE !!!
            // *****************************************

            // la notif
            foreach(String n in tyo.Item3)
				post_values.Add(n, "checked");

            // Les coordonnées en degrées
            post_values.Add("ctl00$ContentBody$LogNotify$LatLong", "4");

            // Les valeurs des coordonnées
            // Le nord 
            String slat = ConvertDegreesToDDMM(dlat, true);
            if (slat.Contains("N"))
                post_values.Add("ctl00$ContentBody$LogNotify$LatLong:_selectNorthSouth", "1");
            else
                post_values.Add("ctl00$ContentBody$LogNotify$LatLong:_selectNorthSouth", "-1");

            // l'est
            String slon = ConvertDegreesToDDMM(dlon, false);
            if (slon.Contains("E"))
                post_values.Add("ctl00$ContentBody$LogNotify$LatLong:_selectEastWest", "1");
            else
                post_values.Add("ctl00$ContentBody$LogNotify$LatLong:_selectEastWest", "-1");

            // enfin les valeurs numériques des coordonnées
            post_values.Add("ctl00$ContentBody$LogNotify$LatLong$_inputLatDegs", (Math.Abs(dlat)).ToString().Replace(',', '.'));
            post_values.Add("ctl00$ContentBody$LogNotify$LatLong$_inputLongDegs", (Math.Abs(dlon)).ToString().Replace(',', '.'));

            // La distance en km
            post_values.Add("ctl00$ContentBody$LogNotify$tbDistance", distance.ToString());

            // l'email
            if (email != "")
                post_values.Add("ctl00$ContentBody$LogNotify$ddlAltEmails", email);

            // activer les notifs
            if (checknotif)
				post_values.Add("ctl00$ContentBody$LogNotify$cbEnable", "checked");

            // Le submit
            post_values.Add("ctl00$ContentBody$LogNotify$btnGo", "submit");

            // Les viewstate
            post_values.Add("__VIEWSTATE", __VIEWSTATE[0]);
            if (__VIEWSTATE.Length > 1)
            {
                for (int i = 1; i < __VIEWSTATE.Length; i++)
                {
                    post_values.Add("__VIEWSTATE" + i.ToString(), __VIEWSTATE[i]);
                }
                post_values.Add("__VIEWSTATEFIELDCOUNT", __VIEWSTATE.Length.ToString());
            }
            if (__VIEWSTATEGENERATOR != "")
                post_values.Add("__VIEWSTATEGENERATOR", __VIEWSTATEGENERATOR);

            // Encodage des données du POST
            String post_string = "";
            foreach (KeyValuePair<String, String> post_value in post_values)
            {
                post_string += post_value.Key + "=" + HttpUtility.UrlEncode(post_value.Value) + "&";
            }
            post_string = post_string.TrimEnd('&');
            return post_string;
        }

		// Generate post request for notification creation
        static public String GeneratePostRequets(String url, String post_string, CookieContainer cookieJar)
        {
            String post_response = "";

            // Création de la requête pour s'authentifier
            HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
            objRequest.Method = "POST";
            objRequest.ContentLength = post_string.Length;
            objRequest.ContentType = "application/x-www-form-urlencoded";
            objRequest.CookieContainer = cookieJar;

            // on envoit les POST data dans un stream (écriture)
            StreamWriter myWriter = null;
            myWriter = new StreamWriter(objRequest.GetRequestStream());
            myWriter.Write(post_string);
            myWriter.Close();

            // lecture du stream de réponse et conversion en chaine
            HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
            using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
            {
                post_response = responseStream.ReadToEnd();
                responseStream.Close();
            }
            return post_response;
        }

		// Perform coordinates conversion
        static private String OneConvertDDMMSSS2DDD(string coord, string negative, string positive, bool bLat)
        {
            try
            {
                // Lat
                String slat = coord;
                // north of south ?
                double sign = 1.0;
                if (slat.Contains(negative))
                    sign = -1.0;
                slat = slat.Replace(" ", "");
                slat = slat.Replace(positive, "");
                slat = slat.Replace(negative, "");
                int ipos = slat.IndexOf("°");
                if (ipos != -1)
                {
                    double d, m, s;
                    String sd = slat.Substring(0, ipos).Replace(",", ".");
                    if (Double.TryParse(sd, NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out d))
                    {
                        if (!CheckLonLatValidity(d, bLat))
                            return _sErrorValue;

                        // ok we have the degress
                        // No try to get the minutes
                        int ipos2 = slat.IndexOf("'");
                        if (ipos2 != -1)
                        {
                            String sm = slat.Substring(ipos + 1, ipos2 - ipos).Replace(",", ".");
                            sm = sm.Replace("'", "");
                            if (Double.TryParse(sm, NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out m))
                            {
                                d += m / 60.0;

                                // Now the seconds
                                String ss = slat.Substring(ipos2 + 1).Replace(",", ".");
                                ss = ss.Replace("'", "");
                                ss = ss.Replace("\"", "");

                                // Check again lat/lon
                                if (!CheckLonLatValidity(d, bLat))
                                    return _sErrorValue;

                                if (Double.TryParse(ss, NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out s))
                                {
                                    d += s / (60.0 * 60.0);
                                    d = d * sign;

                                    // Check again lat/lon
                                    if (!CheckLonLatValidity(d, bLat))
                                        return _sErrorValue;
                                    return String.Format("{0:0.######}", d).Replace(",", ".");
                                }
                                else
                                    return _sErrorValue;
                            }
                            else
                                return _sErrorValue;
                        }
                        else
                            return _sErrorValue;
                    }
                    else
                        return _sErrorValue;
                }
                else
                    return _sErrorValue;
            }
            catch (Exception)
            {
                return _sErrorValue;
            }
        }

		/// <summary>
        /// Try to convert coordinates from any format to decimal degrees
        /// </summary>
        /// <param name="ctrltxt">latitude and longitude</param>
        /// <param name="sLat">valid latitude or #ERR</param>
        /// <param name="sLon">valid longitude or #ERR</param>
        /// <returns>True if both coordinates are valid</returns>
        static public bool TryToConvertCoordinates(String ctrltxt, ref String sLat, ref String sLon)
        {
            sLat = _sErrorValue;
            sLon = _sErrorValue;
			ctrltxt = ctrltxt.ToUpper(); // Pour être sûr d'avoir les N, S, E, W en majuscule
            try
            {
                // On essaie de convertir les valeurs
                // ctrl.Text doit contenir la latitude puis la longitude 
                // - en degrés décimaux séparés par un espace
                // - en DD MM.MMM séparés par un espace
                // - en DD MM SSS séparés par un espace
                if (ctrltxt.Contains("N") ||
                    ctrltxt.Contains("S") ||
                    ctrltxt.Contains("E") ||
                    ctrltxt.Contains("W"))
                {
                    // on est en DD MM.MMM ou DD MM SSS
                    // DD° MM.MMM : N 48° 46.164 E 01° 58.048
                    // DD° MM' SS.SSS : N 48° 46' 9.9 E 01° 58' 2.9
                    
                    // Si on a des ' ou '' ou " alors on tente en DDMMSSS
                    if (ctrltxt.Contains("'") ||
                        ctrltxt.Contains("''") ||
                        ctrltxt.Contains("\""))
                    {
                        // Peut être en DD MM SSS ?
                        if (DDMMSSStoDDD(ctrltxt, ref sLat, ref sLon))
                        {
                            // On a converti
                        }
                        else if (DDMMMtoDDD(ctrltxt, ref sLat, ref sLon)) // au cas ou on ait mis des ' derrière les minutes décimales
                        {
                            // On a converti
                        }
                        else
                            return false;
                    }
                    else
                    {
                        // On va tenter de convertir en degrés décimaux
                        // Test en DDMMM
                        if (DDMMMtoDDD(ctrltxt, ref sLat, ref sLon))
                        {
                            // On a converti
                        }
                        else
                            return false;
                    }
                }
                else
                {
                    // On est en degrés décimaux
                    // On isole la latitude et la longitude
                    double dlon = double.MaxValue;
                    double dlat = double.MaxValue;
                    if (SplitLongitudeLatitude(ctrltxt.Replace(",", "."), ref dlon, ref dlat))
                    {
                        // C'est top, tout va bien
                        sLon = dlon.ToString().Replace(",", ".");
                        sLat = dlat.ToString().Replace(",", ".");
                    }
                    else
                    {
                        // On essaie tout de même de traduire ce qu'on peut
                        if (dlon != Double.MaxValue)
                            sLon = dlon.ToString().Replace(",", ".");
                        if (dlat != Double.MaxValue)
                            sLat = dlat.ToString().Replace(",", ".");
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }
		
		/// <summary>
        /// Text is latitude then longitude in decimal degrees
        /// </summary>
        /// <param name="text">Text is latitude then longitude in decimal degrees</param>
        /// <param name="dlon">longitude</param>
        /// <param name="dlat">latitude</param>
        /// <returns>true is split succeeded</returns>
        static public bool SplitLongitudeLatitude(String text, ref double dlon, ref double dlat)
        {
            dlon = Double.MaxValue;
            dlat = Double.MaxValue;
            String lat = "";
            String lon = "";
            try 
	        {
		        // Expect "longitude latitude"
                String lonlat = text;
                lonlat = text.TrimStart(' ');
                lonlat = text.TrimEnd(' ');
                if ((lonlat != "") && (lonlat.Contains(" ")))
                {
                    // On découpe
                    int pos = lonlat.IndexOf(' ');
                    if (pos <= 0)
                        return false;
                    lat = lonlat.Substring(0, pos);
                    lon = lonlat.Substring(pos + 1);
                    lon = lon.Trim();
                    lat = lat.Trim();
                    bool result = true;
                    if (CheckLonLatValidity(lon, false))
                        dlon = ConvertToDouble(lon);
                    else
                        result = false;

                    if (CheckLonLatValidity(lat, true))
                        dlat = ConvertToDouble(lat);
                    else
                        result = false;

                    return result;
                }
                else
                {
                    // On tente tout de même de convertir la première valeur, la latitude
                    dlat = ConvertToDouble(lonlat);
                    return false;
                }
	        }
	        catch (Exception)
	        {
                return false;
	        }
        }
		
		/// <summary>
        /// Convert coordinnates from any supported format to three formats
        /// </summary>
        /// <param name="coords">Coordinates to convert</param>
        /// <returns>Converted coordinates (with #ERR if error)</returns>
        static public String ConvertCoordinates(String coords)
        {
            String info = "";

            try
            {
                String sLat = "";
                String sLon = "";
                String sLat2 = _sErrorValue;
                String sLon2 = _sErrorValue;
                double dLat = Double.MaxValue;
                double dLon = Double.MaxValue;

                bool bOK = TryToConvertCoordinates(coords, ref sLat, ref sLon);
                if (sLat != _sErrorValue)
                    dLat = ConvertToDouble(sLat);
                if (sLon != _sErrorValue)
                    dLon = ConvertToDouble(sLon);
                info += /*"DD.DDDDDD: " + */sLat + " " + sLon + "\n";

                sLat2 = ConvertDegreesToDDMM(dLat, true);
                sLon2 = ConvertDegreesToDDMM(dLon, false);
                info += /*"DD° MM.MMM: " + */sLat2 + " " + sLon2 + "\n";

                sLat2 = ConvertDegreesToDDMMSSTT(dLat, true);
                sLon2 = ConvertDegreesToDDMMSSTT(dLon, false);
                info += /*"DD° MM' SS.SSS: " + */sLat2 + " " + sLon2;
            }
            catch (Exception)
            {
            }

            return info;
        }
		
        /// <summary>
        /// Convert decimal degrees to minutes, seconds, tenths of seconds
        /// </summary>
        /// <param name="decimal_degrees">value to convert</param>
        /// <param name="bLat">true if value is a latitude</param>
        /// <returns>converted coordinate</returns>
        static public String ConvertDegreesToDDMMSSTT(double decimal_degrees, bool bLat)
        {
            if (!CheckLonLatValidity(decimal_degrees, bLat))
                return _sErrorValue;


            // set decimal_degrees value here
            if (bLat)
                return OneConvertDDD2DDMMSSS(decimal_degrees.ToString().Replace(",", "."), "S", "N", true);
            else
                return OneConvertDDD2DDMMSSS(decimal_degrees.ToString().Replace(",", "."), "W", "E", false);
        }

        /// <summary>
        /// Convert one degree value in DDMM
        /// </summary>
        /// <param name="value">value to convert</param>
        /// <param name="bLat">true if value is a latitude</param>
        /// <returns>converted coordinate</returns>
        static public String ConvertDegreesToDDMM(double value, bool bLat)
        {
            if (!CheckLonLatValidity(value, bLat))
                return _sErrorValue;

            double ov = value;
            int deg = (int)value;
            value = Math.Abs(value - deg);
            double min = value * 60;

            String s;
            if (bLat)
            {
                if (ov < 0d)
                    s = "S ";
                else
                    s = "N ";
                deg = Math.Abs(deg);
                s += deg.ToString() + "° " + String.Format("{0:0.###}", min);
            }
            else
            {
                if (ov < 0d)
                    s = "W ";
                else
                    s = "E ";
                deg = Math.Abs(deg);
                s += deg.ToString() + "° " + String.Format("{0:0.###}", min);
            }
            // Maudits français !
            s = s.Replace(",", ".");
            return s;
        }

        /// <summary>
        /// Convert decimal degrees to minutes, seconds, tenths of seconds
        /// NO SAFETY CHECK HERE !!!
        /// </summary>
        /// <param name="decimal_degrees">decimal degrees</param>
        /// <param name="minutes">minutes</param>
        /// <param name="seconds">seconds</param>
        /// <param name="tenths">tenths of seconds</param>
        static public void ConvertDegreesToDDMMSSTT(double decimal_degrees, out double minutes, out double seconds, out double tenths)
        {
            // set decimal_degrees value here
            minutes = (decimal_degrees - Math.Floor(decimal_degrees)) * 60.0;
            seconds = (minutes - Math.Floor(minutes)) * 60.0;
            tenths = (seconds - Math.Floor(seconds)) * 10.0;
            // get rid of fractional part
            minutes = Math.Floor(minutes);
            seconds = Math.Floor(seconds);
            tenths = Math.Floor(tenths);
        }

        /// <summary>
        /// Check if longitude and latitude in decimal degrees are within valid range
        /// </summary>
        /// <param name="decimal_degrees">longitude or latitude</param>
        /// <param name="bLat">True if latitude</param>
        /// <returns>true if value is correct</returns>
        static public bool CheckLonLatValidity(String decimal_degrees, bool bLat)
        {
            try
            {
                double coord = ConvertToDouble(decimal_degrees);
                return CheckLonLatValidity(coord, bLat);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Convert a string to double, regardless the current culture string format
        /// Will handle . or , as a decimal separator
        /// </summary>
        /// <param name="s">string to convert</param>
        /// <returns>double value</returns>
        static public Double ConvertToDouble(String s)
        {
            return Double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Check if longitude and latitude in decimal degrees are within valid range
        /// </summary>
        /// <param name="decimal_degrees_lon">longitude in decimal degrees (-180 / +180)</param>
        /// <param name="decimal_degrees_lat">latitude in decimal degrees (-90 / +90)</param>
        /// <returns>true if both values are correct</returns>
        static public bool CheckLonLatValidity(String decimal_degrees_lon, String decimal_degrees_lat)
        {
            try
            {
                double lon = ConvertToDouble(decimal_degrees_lon);
                double lat = ConvertToDouble(decimal_degrees_lat);
                return CheckLonLatValidity(lon, lat);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Check if longitude and latitude in decimal degrees are within valid range
        /// </summary>
        /// <param name="decimal_degrees">longitude or latitude</param>
        /// <param name="bLat">True if latitude</param>
        /// <returns>true if value is correct</returns>
        static public bool CheckLonLatValidity(double decimal_degrees, bool bLat)
        {
            if (bLat)
            {
                if ((decimal_degrees < -90.0) ||
                    (decimal_degrees > 90.0))
                    return false;
                else
                    return true;
            }
            else
            {
                if ((decimal_degrees < -180.0) ||
                    (decimal_degrees > 180.0))
                    return false;
                else
                    return true;
            }
        }

        /// <summary>
        /// converts coordinates from DDMMM to DDD
        /// From DD° MM.MMM : N 48° 46.164 E 01° 58.048
        /// To DD.DDDDDD : 48.769408 1.967473
        /// </summary>
        /// <param name="LatLonDDMMM">coordinates to convert</param>
        /// <param name="sLat">output, N or S</param>
        /// <param name="sLon">output, E or W</param>
        /// <returns>true if coordinates are valid</returns>
        static public bool DDMMMtoDDD(String LatLonDDMMM, ref String sLat, ref String sLon)
        {
            // Now it's tricky...
            String c = LatLonDDMMM;
            c = c.TrimStart(null);
            c = c.TrimEnd(null);

            int iNS = Math.Max(c.IndexOf("N"), c.IndexOf("S"));
            int iEW = Math.Max(c.IndexOf("E"), c.IndexOf("W"));
            if ((iNS == -1) || (iEW == -1))
                return false;

            String c1 = "";
            String c2 = "";
            if (iNS < iEW)
            {
                c1 = c.Substring(0, iEW);
                c2 = c.Substring(iEW);
            }
            else
            {
                c2 = c.Substring(0, iNS);
                c1 = c.Substring(iNS);
            }

            sLat = OneConvertDDMMM2DDD(c1, "S", "N", true);
            sLon = OneConvertDDMMM2DDD(c2, "W", "E", false);
            if ((sLat != _sErrorValue) && (sLon != _sErrorValue))
                return true;
            else
                return false;
        }

        /// <summary>
        /// converts coordinates from DDMMM to DDD
        /// From DD.DDDDDD : 48.769408 1.967473
        /// To DD° MM' SS.SSS : N 48° 46' 9.9 E 01° 58' 2.9
        /// </summary>
        /// <param name="LatLonDDMMSSS">coordinates to convert</param>
        /// <param name="sLat">output, N or S</param>
        /// <param name="sLon">output, E or W</param>
        /// <returns>true if coordinates are valid</returns>
        static public bool DDMMSSStoDDD(String LatLonDDMMSSS, ref String sLat, ref String sLon)
        {
            // Now it's tricky...
            String c = LatLonDDMMSSS;
            c = c.TrimStart(null);
            c = c.TrimEnd(null);

            int iNS = Math.Max(c.IndexOf("N"), c.IndexOf("S"));
            int iEW = Math.Max(c.IndexOf("E"), c.IndexOf("W"));
            if ((iNS == -1) || (iEW == -1))
                return false;

            String c1 = "";
            String c2 = "";
            if (iNS < iEW)
            {
                c1 = c.Substring(0, iEW);
                c2 = c.Substring(iEW);
            }
            else
            {
                c2 = c.Substring(0, iNS);
                c1 = c.Substring(iNS);
            }

            sLat = OneConvertDDMMSSS2DDD(c1, "S", "N", true);
            sLon = OneConvertDDMMSSS2DDD(c2, "W", "E", false);
            if ((sLat != _sErrorValue) && (sLon != _sErrorValue))
                return true;
            else
                return false;
        }

		// Performs coordinate conversion
        static private String OneConvertDDD2DDMMSSS(String coord, String negative, String positive, bool bLat)
        {
            double lat;
            if (coord != "")
                coord = coord.Replace(",", ".");
            if (Double.TryParse(coord, NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out lat))
            {
                if (!CheckLonLatValidity(lat, bLat))
                    return _sErrorValue;

                int d = (int)lat;
                String NSEW = (d < 0) ? negative + " " : positive + " ";
                d = Math.Abs(d);

                double m = (Math.Abs(lat) - (double)d) * 60.0;
                double s = (Math.Abs(m) - (double)(int)m) * 60.0;
                return NSEW + String.Format(CultureInfo.InvariantCulture, "{0:00}", d) + "° " + String.Format(CultureInfo.InvariantCulture, "{0:00}", (int)m) + "' " + String.Format(CultureInfo.InvariantCulture, "{0:0.0}", s);
            }
            else
            {
                return _sErrorValue;
            }
        }

		// Performs coordinate conversion
        static private String OneConvertDDMMM2DDD(string coord, string negative, string positive, bool bLat)
        {
            try
            {
                // Lat
                String slat = coord;
                // north of south ?
                double sign = 1.0;
                if (slat.Contains(negative))
                    sign = -1.0;
                slat = slat.Replace(" ", "");
                slat = slat.Replace(positive, "");
                slat = slat.Replace(negative, "");
                int ipos = slat.IndexOf("°");
                if (ipos != -1)
                {
                    double d, m;
                    String sd = slat.Substring(0, ipos).Replace(",", ".");
                    if (Double.TryParse(sd, NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out d))
                    {
                        if (!CheckLonLatValidity(d, bLat))
                            return _sErrorValue;

                        // ok we have the degress
                        // No try to get the minutes
                        String sm = slat.Substring(ipos + 1).Replace(",", ".");
                        sm = sm.Replace("'", "");
                        if (Double.TryParse(sm, NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out m))
                        {
                            d += m / 60.0;
                            d = d * sign;

                            // ATTENTION ON PEUT AVOIR
                            // N 48° 456 par exemple
                            // Il faut donc rechecker la lon/lat pour vérifier sa validité
                            if (!CheckLonLatValidity(d, bLat))
                                return _sErrorValue;

                            return String.Format("{0:0.######}", d).Replace(",", ".");
                        }
                        else
                            return _sErrorValue;
                    }
                    else
                        return _sErrorValue;
                }
                else
                    return _sErrorValue;
            }
            catch (Exception)
            {
                return _sErrorValue;
            }
        }

        /// <summary>
        /// Check if longitude and latitude in decimal degrees are within valid range
        /// </summary>
        /// <param name="decimal_degrees_lon">longitude in decimal degrees (-180 / +180)</param>
        /// <param name="decimal_degrees_lat">latitude in decimal degrees (-90 / +90)</param>
        /// <returns>true if both values are correct</returns>
        static public bool CheckLonLatValidity(double decimal_degrees_lon, double decimal_degrees_lat)
        {
            if ((decimal_degrees_lon < -180.0) ||
                (decimal_degrees_lon > 180.0) ||
                (decimal_degrees_lat < -90.0) ||
                (decimal_degrees_lat > 90.0))
                return false;
            else
                return true;
        }

		// Retrieve list of notifications
        public List<Notif> ListNotifications()
        {

            String post_response = "";
            if (_cookieJar == null)
                return null;

            // Pour récupérer les notifications
            String url = "https://www.geocaching.com/notify/default.aspx";
            HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
            objRequest.CookieContainer = _cookieJar; // surtout récupérer le container de cookie qui est maintenant renseigné avec le cookie d'authentification
            HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
            using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
            {
                post_response = responseStream.ReadToEnd();
                responseStream.Close();
            }

            // On parse
            // On récupère la tables des notifications
            List<Notif> lsentries = new List<Notif>();
            String table = GetSnippetFromText("<table class=\"Table\">", "</table>", post_response);
            List<String> entries = GetSnippetsFromText("<tr", "</tr>", table);
            int i = 1;
            foreach (String entry in entries)
            {
                Notif n = new Notif();

                n.Name = GetSnippetFromText("<strong>", "</strong>", entry);
                n.Id = GetSnippetFromText("edit.aspx?NID=", "\"", entry);
                String notif = GetSnippetFromText("<br />", "\n", entry);
                int pos = notif.IndexOf(":");
                if (pos != -1)
                    notif = notif.Substring(pos + 2);
                n.NotifType = notif;

				// Enabled ?
            	n.Enabled = false;
            	if (entry.Contains("checkbox_on.png\" alt=\"Checked\""))
            	{
            		n.Enabled = true;
            	}
				
                n.Type = GetSnippetFromText(".gif\" alt=\"", "\"", entry);

				// Resource id
                n.ResourceId = GCNotification.GetResourceId(n.Type);

                // Pas besoin de mettre le type, c'est redondant, on a déjà l'icone
                n.Info = /*n.Type + ", " + */n.NotifType;
				if (n.Info.Length > 50)
				{
					// On limite le texte à 50 caractères
					// Sinon on tronque et on ajoute (...), soit 5 caractères
					n.Info = n.Info.Substring(0, 45) + "(...)";
				}
                n.Number = i;
                lsentries.Add(n);
                i++;
            }
            return lsentries;
        }

		// Retrieve list of emails for notifications
		public List<String> GetListOfEmails()
		{
            // Pour récupérer les emails
			List<String> lsemails = new List<string>();
			try
			{
				String url = "https://www.geocaching.com/notify/edit.aspx";
				HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
				objRequest.CookieContainer = _cookieJar; // surtout récupérer le container de cookie qui est maintenant renseigné avec le cookie d'authentification
				HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
				String post_response = "";
				using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
				{
					post_response = responseStream.ReadToEnd();
					responseStream.Close();
				}
				
				String mails = GetSnippetFromText("ctl00$ContentBody$LogNotify$ddlAltEmails","select>",post_response);
				lsemails = GetSnippetsFromText("value=\"", "\">", mails);
				
			}
			catch(Exception)
			{
				
			}
			return lsemails;
		}
		
		// Delete list of notifications (using their ids)
        public bool DeleteNotifications(MainActivity main, ProgressDialog progressDialog, List<String> ids)
        {
			try
			{
                int nb = 1;
				foreach (String id in ids)
				{
                    // Is it canceled ?
                    if (main._Canceled)
                        break; // Yes

                    // Update progress
                    progressDialog.Progress = nb;

                    // on attaque la suppression
                    String url = "https://www.geocaching.com/notify/edit.aspx?NID=" + id;
					HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
					objRequest.CookieContainer = _cookieJar; // surtout récupérer le container de cookie qui est maintenant renseigné avec le cookie d'authentification
					HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
					String post_response = "";
					using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
					{
						post_response = responseStream.ReadToEnd();
						responseStream.Close();
					}

					// On récupère les viewstates
					String __VIEWSTATEFIELDCOUNT = "";
					String[] __VIEWSTATE = null;
					String __VIEWSTATEGENERATOR = "";
					GetViewState(post_response, ref __VIEWSTATEFIELDCOUNT, ref __VIEWSTATE, ref __VIEWSTATEGENERATOR);

					// Préparation des données du POST
					Dictionary<String, String> post_values = new Dictionary<String, String>();
					post_values.Add("__EVENTTARGET", "");
					post_values.Add("__EVENTARGUMENT", "");
					post_values.Add("__LASTFOCUS", "");

					// Le submit
					post_values.Add("ctl00$ContentBody$LogNotify$btnArchive", "Delete Notification");

					// Les viewstate
					post_values.Add("__VIEWSTATE", __VIEWSTATE[0]);
					if (__VIEWSTATE.Length > 1)
					{
						for (int i = 1; i < __VIEWSTATE.Length; i++)
						{
							post_values.Add("__VIEWSTATE" + i.ToString(), __VIEWSTATE[i]);
						}
						post_values.Add("__VIEWSTATEFIELDCOUNT", __VIEWSTATE.Length.ToString());
					}
					if (__VIEWSTATEGENERATOR != "")
						post_values.Add("__VIEWSTATEGENERATOR", __VIEWSTATEGENERATOR);

					// Encodage des données du POST
					String post_string = "";
					foreach (KeyValuePair<String, String> post_value in post_values)
					{
						post_string += post_value.Key + "=" + HttpUtility.UrlEncode(post_value.Value) + "&";
					}
					post_string = post_string.TrimEnd('&');

					// Création de la requête pour s'authentifier
					objRequest = (HttpWebRequest)WebRequest.Create(url);
					objRequest.Method = "POST";
					objRequest.ContentLength = post_string.Length;
					objRequest.ContentType = "application/x-www-form-urlencoded";
					objRequest.CookieContainer = _cookieJar;

					// on envoit les POST data dans un stream (écriture)
					StreamWriter myWriter = null;
					myWriter = new StreamWriter(objRequest.GetRequestStream());
					myWriter.Write(post_string);
					myWriter.Close();

					// lecture du stream de réponse et conversion en chaine
					objResponse = (HttpWebResponse)objRequest.GetResponse();
					using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
					{
						post_response = responseStream.ReadToEnd();
						responseStream.Close();
					}

                    nb++;
				}
			
				return true;
			}
			catch(Exception)
			{
				return false;
			}
        }

		// Toggle list of notifications (using their ids)
        public bool ToggleNotifications(MainActivity main, ProgressDialog progressDialog, List<String> ids)
        {
			try
			{
                int nb = 1;
				foreach (String id in ids)
				{
                    // Is it canceled ?
                    if (main._Canceled)
                        break; // Yes

                    // Update progress
                    progressDialog.Progress = nb;

                    // on attaque le toggling
                    // Pour basculer le toggle sur une notification, il suffit
                    // d'appeler https://www.geocaching.com/notify/default.aspx?did=<ID>
                    // ça bascule l'état de notification tout simplement

                    String url = "https://www.geocaching.com/notify/default.aspx?did=" + id;
					HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
					objRequest.CookieContainer = _cookieJar; // surtout récupérer le container de cookie qui est maintenant renseigné avec le cookie d'authentification
					HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
					String post_response = "";
					using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
					{
						post_response = responseStream.ReadToEnd();
						responseStream.Close();
					}

                    nb++;
				}
			
				return true;
			}
			catch(Exception)
			{
				return false;
			}
        }
		
		// Get save path for application
		static public String GetSavePath()
        {
            string path = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            String filepath = path + "/EZFTF.dat";
            return filepath;
        }

		// Load configuration from savepath
		static public List<String> LoadDataString()
        {
            StreamReader reader = null;
            String filepath = GetSavePath();
            List<String> lines = new List<String>();
            try
            {
                if (File.Exists(filepath))
                {
                    reader = new StreamReader(filepath);
                    if (reader == null)
                    {
                        return lines;
                    }

                    String line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                    reader.Close();
                }
				
				// Ligne 1 : login
				// Ligne 2 : password
				if ((lines.Count >= 2) && (lines[1] != ""))
				{
					// On déchiffre
					String password_decyph = "";
					StringCipher.CustomDecrypt(lines[1], ref password_decyph);
					lines[1] = password_decyph;
				}
				
                return lines;
            }
            catch (Exception)
            {
                if (reader != null)
                    reader.Close();
                return lines;
            }
        }

		// export configuration in save path
        static public bool ExportData(String login, String password)
        {
            if (_bSaving)
                return false;

            _bSaving = true;

            try
            {
				// On chiffre le password
				String password_cyph = StringCipher.CustomEncrypt(password);
				
                String s = login + "\r\n" + password_cyph;
                String filepath = GetSavePath();
                if (filepath == "")
                    return false;

                StreamWriter writer = new StreamWriter(filepath, false);
                writer.WriteLine(s);
                writer.Close();
                _bSaving = false;
                return true;
            }
            catch (Exception)
            {
                _bSaving = false;
                return false;
            }
        }
		
		// Check network status
		static public bool CheckNetworkAccess(Activity dad)
		{
			// Create a ConnectivityManager
			ConnectivityManager connectivityManager = (ConnectivityManager) dad.GetSystemService(Android.Content.Context.ConnectivityService);
			NetworkInfo activeConnection = connectivityManager.ActiveNetworkInfo;
			bool isOnline = (activeConnection != null) && activeConnection.IsConnected;
			
			// Bonus :
			/*
			NetworkInfo wifiInfo = connectivityManager.GetNetworkInfo(ConnectivityType.Wifi);
			if(wifiInfo.IsConnected)
			{
			  Log.Debug(TAG, "Wifi connected.");
			  _wifiImage.SetImageResource(Resource.Drawable.green_square);
			} else
			{
			  Log.Debug(TAG, "Wifi disconnected.");
			  _wifiImage.SetImageResource(Resource.Drawable.red_square);
			}
			*/
			
			return isOnline;
		}

		// Check if notification validation failed
		static public String CheckValidationMessage(String html)
        {
        	// On regarde si on a un message de warning
    		String warning = GetSnippetFromText("ctl00_ContentBody_LogNotify_ValidationSummary1", "</div>", html);
    		warning = GetSnippetFromText("<ul>", "</ul>", warning);
    		if (warning != "")
    		{
    			// Shit
    			warning = Html.FromHtml(warning).ToString();
    		}
    		return warning;
        }
		
		// Check if we have a warning message
		static public String CheckWarningMessage(String html)
        {
        	// On regarde si on a un message de warning
    		return GetSnippetFromText("<p class=\"Warning\">", "</p>", html);
        }
		
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="post_response"></param>
        /// <returns></returns>
        public GCNotification GetNotificationData(String id, ref String post_response)
        {
            GCNotification gcn = null;
            try
            {
                gcn = new GCNotification();

                // Pour récupérer les notifications
                String url = "https://www.geocaching.com/notify/edit.aspx?NID=" + id;
                HttpWebRequest objRequest = (HttpWebRequest)WebRequest.Create(url);
                objRequest.CookieContainer = _cookieJar; // surtout récupérer le container de cookie qui est maintenant renseigné avec le cookie d'authentification
                HttpWebResponse objResponse = (HttpWebResponse)objRequest.GetResponse();
                using (StreamReader responseStream = new StreamReader(objResponse.GetResponseStream()))
                {
                    post_response = responseStream.ReadToEnd();
                    responseStream.Close();
                }

                // On va parser les informations
                // Le nom
                String txt = GetSnippetFromText("ctl00$ContentBody$LogNotify$tbName\" type=\"text\" value=\"", "\" id=\"", post_response);
                gcn.name = txt;

                // Le type id + nom
                txt = GetSnippetFromText("ctl00$ContentBody$LogNotify$ddTypeList", "/select", post_response);
                txt = GetSnippetFromText("<option selected=\"selected\"", "/option", txt); // ==>  value="2">Traditional Cache<
                String sid = GetSnippetFromText(" value=\"", "\">", txt);
                int typeid = Int32.Parse(sid);
                String type = GetSnippetFromText(">", "<", txt);

                // Les types de notifications
                List<String> typepublishchecked = new List<string>();
                txt = GetSnippetFromText("ctl00_ContentBody_LogNotify_cblLogTypeList", "/table", post_response);
                List<String> entries = GetSnippetsFromText("<tr>", "</tr>", txt);
                foreach (String entry in entries)
                {
                    if (entry.Contains("checked=\"checked\""))
                    {
                        // Elle est checked
                        typepublishchecked.Add(GetSnippetFromText("name=\"", "\"", entry));
                    }
                }

                // on construit les datas
                // typeid, type
                gcn.data = new Tuple<int, string, List<string>, int>(typeid, type, typepublishchecked, -1);

                // la distance
                txt = GetSnippetFromText("ctl00$ContentBody$LogNotify$tbDistance\" type=\"text\" value=\"", "\" id=", post_response);
                gcn.distance = Int32.Parse(txt);

                // L'email
                txt = GetSnippetFromText("ctl00$ContentBody$LogNotify$ddlAltEmails", "/select", post_response);
                txt = GetSnippetFromText("selected=\"selected\" value=\"", "\">", txt);
                gcn.email = txt;

                // Notification checkée ?
                gcn.checknotif = post_response.Contains("name=\"ctl00$ContentBody$LogNotify$cbEnable\" checked=\"checked\"");


                // Et les coordonnées
                // Nord ou sud ?
                txt = GetSnippetFromText("ctl00$ContentBody$LogNotify$LatLong:_selectNorthSouth", "/select", post_response);
                txt = GetSnippetFromText("selected=\"selected\" value=\"", "\">", txt);
                String nordsud = "";
                if (txt == "1")
                    nordsud = "N";
                else
                    nordsud = "S";

                // degres ns
                String degns = GetSnippetFromText("ctl00$ContentBody$LogNotify$LatLong$_inputLatDegs\" type=\"text\" value=\"", "\" maxlength", post_response);

                // min ns
                String minns = GetSnippetFromText("ctl00$ContentBody$LogNotify$LatLong$_inputLatMins\" type=\"text\" value=\"", "\" maxlength", post_response);

                // Est ou ouest ?
                txt = GetSnippetFromText("ctl00$ContentBody$LogNotify$LatLong:_selectEastWest", "/select", post_response);
                txt = GetSnippetFromText("selected=\"selected\" value=\"", "\">", txt);
                String estouest = "";
                if (txt == "1")
                    estouest = "E";
                else
                    estouest = "W";

                // degres ew
                String degew = GetSnippetFromText("ctl00$ContentBody$LogNotify$LatLong$_inputLongDegs\" type=\"text\" value=\"", "\" maxlength", post_response);

                // min ew
                String minew = GetSnippetFromText("ctl00$ContentBody$LogNotify$LatLong$_inputLongMins\" type=\"text\" value=\"", "\" maxlength", post_response);

                // Coordinates format :
                // N 48° 46.164 E 01° 58.048
                String coords = nordsud + " " + degns + "° " + minns + " " + estouest + " " + degew + "° " + minew;
                String sLat = "";
                String sLon = "";
                bool bOK = TryToConvertCoordinates(coords, ref sLat, ref sLon);
                if (sLat != _sErrorValue)
                    gcn.dlat = ConvertToDouble(sLat);
                else
                    throw new Exception();
                if (sLon != _sErrorValue)
                    gcn.dlon = ConvertToDouble(sLon);
                else
                    throw new Exception();

            }
            catch (Exception)
            {
                gcn = null;
            }

            return gcn;
        }
    
		// 'halfSideInKm' is the half length of the bounding box you want in kilometers (it's the radius basically).
		public static BoundingBox GetBoundingBox(MapPoint point, double halfSideInKm)
		{            
			// Bounding box surrounding the point at given coordinates,
			// assuming local approximation of Earth surface as a sphere
			// of radius given by WGS84
			var lat = Deg2rad(point.Latitude);
			var lon = Deg2rad(point.Longitude);
			var halfSide = 1000 * halfSideInKm;

			// Radius of Earth at given latitude
			var radius = WGS84EarthRadius(lat);
			// Radius of the parallel at given latitude
			var pradius = radius * Math.Cos(lat);

			var latMin = lat - halfSide / radius;
			var latMax = lat + halfSide / radius;
			var lonMin = lon - halfSide / pradius;
			var lonMax = lon + halfSide / pradius;

			return new BoundingBox { 
				MinPoint = new MapPoint { Latitude = Rad2deg(latMin), Longitude = Rad2deg(lonMin) },
				MaxPoint = new MapPoint { Latitude = Rad2deg(latMax), Longitude = Rad2deg(lonMax) }
			};            
		}

		// degrees to radians
		private static double Deg2rad(double degrees)
		{
			return Math.PI * degrees / 180.0;
		}

		// radians to degrees
		private static double Rad2deg(double radians)
		{
			return 180.0 * radians / Math.PI;
		}

		// Earth radius at a given latitude, according to the WGS-84 ellipsoid [m]
		private static double WGS84EarthRadius(double lat)
		{
			// http://en.wikipedia.org/wiki/Earth_radius
			var An = WGS84_a * WGS84_a * Math.Cos(lat);
			var Bn = WGS84_b * WGS84_b * Math.Sin(lat);
			var Ad = WGS84_a * Math.Cos(lat);
			var Bd = WGS84_b * Math.Sin(lat);
			return Math.Sqrt((An*An + Bn*Bn) / (Ad*Ad + Bd*Bd));
		}
	}
	
	// Class that defines a point in degrees
	public class MapPoint
	{
		public double Longitude { get; set; } // In Degrees
		public double Latitude { get; set; } // In Degrees
	}

	// Class that defines a box in degrees
	public class BoundingBox
	{
		public MapPoint MinPoint { get; set; }
		public MapPoint MaxPoint { get; set; }
	}        
}