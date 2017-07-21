/******
 * 
 * AuthTest.cs
 * 
 * Demo of making a simple json auth request to mapmyrun.com, with some extra commented-out
 * goop to remind myself how to do this for other sites with slightly different protocols.
 * 
 * Dan Morris, 2017
 * 
 * Released under the Granola Bar License: if you find this code useful, send me a granola bar.
 * 
 *******/

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;

namespace AuthTest
{
    class AuthTest
    {
        static void Main(string[] args)
        {
			// The URL I need to visit to input my username / password
			String authUrl = "https://www.mapmyrun.com/auth/login";

			// A data URL I'd like to retrieve once I'm authenticated
			String dataUrl = "http://www.mapmyrun.com/workout/191379775";

			// Files where I'll write the html pages I get back, just for debugging
            String dataOut = @"out.txt";
            String authOut = @"auth.txt";

			// Username and password for authentication
			String username = "username";
			String password = "password";

			// This is what a correctly-formed auth query to mapmyrun.com looks like (I got this via fiddler); I've replaced "*" with the word "star" to put
			// it in this comment.
			/*
            POST https://www.mapmyrun.com/auth/login HTTP/1.1
            Host: www.mapmyrun.com
            Connection: keep-alive
            Content-Length: 45
            Accept: application/json, text/plain, star/star
            Origin: https://www.mapmyrun.com
            User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36
            Content-Type: application/json
            DNT: 1
            Referer: https://www.mapmyrun.com/auth/login
            Accept-Encoding: gzip, deflate, br
            Accept-Language: en-US,en;q=0.8
            Cookie: my_home_tabselect=%23activity_feed; planner_type=list; optimizelySegments=%7B%22172178767%22%3A%22referral%22%2C%22173022868%22%3A%22false%22%2C%22173102711%22%3A%22gc%22%7D; optimizelyBuckets=%7B%7D; __utma=39663935.959121928.1497305017.1497309474.1497316563.3; __utmc=39663935; __utmz=39663935.1497305025.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none); optimizelyPendingLogEvents=%5B%5D; amplitude_idmapmyrun.com=eyJkZXZpY2VJZCI6IjU2MjBlZTkwLTRkOWUtNDA3Zi1hYTljLTQwMDkxNjE2ZmEzNlIiLCJ1c2VySWQiOm51bGwsIm9wdE91dCI6ZmFsc2UsInNlc3Npb25JZCI6MTQ5NzMyMzAwMTg5OSwibGFzdEV2ZW50VGltZSI6MTQ5NzMyMzAwMTg5OSwiZXZlbnRJZCI6MCwiaWRlbnRpZnlJZCI6MCwic2VxdWVuY2VOdW1iZXIiOjB9; _dy_csc_ses=t; _dy_ses_load_seq=84355%3A1497323001953; _dy_c_exps=; _ga=GA1.2.959121928.1497305017; _gid=GA1.2.394399346.1497305017; _dc_gtm_UA-273418-57=1; _dycst=dk.w.c.ws.frv4.; _dy_geo=US.NA.US_WA.US_WA_Bellevue; _dy_df_geo=United%20States.Washington.Bellevue; _dy_toffset=0; _dyus_8767255=22%7C0%7C0%7C0%7C0%7C0.0.1485886047286.1497323002523.11436955.0%7C162%7C24%7C5%7C117%7C15%7C0%7C0%7C0%7C0%7C0%7C0%7C15%7C0%7C0%7C0%7C0%7C0%7C15%7C0%7C0%7C0%7C0%7C1; _dy_soct=92327.125314.1497323001*94588.128721.1497323001

            {"username":"xxxx","password":"xxxxx"}
            */

			// When things weren't woring, I spent a lot of time trying to replicate the outgoing request from my browser, including
			// cookies.  This is the cookie string I got from fiddler, minus some private tokens I removed so I could distribute this.
			//
			// Turns out this wasn't necessary, but I'm keeping this here so I can refer back to it for similar projects in the future.
            String cookieString = "my_home_tabselect=%23activity_feed; planner_type=list; optimizelySegments=%7B%22172178767%22%3A%22referral%22%2C%22173022868%22%3A%22false%22%2C%22173102711%22%3A%22gc%22%7D; optimizelyBuckets=%7B%7D; __utma=39663935.959121928.1497305017.1497309474.1497316563.3; __utmb=39663935.3.9.1497317132558; __utmc=39663935; __utmz=39663935.1497305025.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none); optimizelyPendingLogEvents=%5B%5D; _dy_csc_ses=t; amplitude_idmapmyrun.com=eyJkZXZpY2VJZCI6IjU2MjBlZTkwLTRkOWUtNDA3Zi1hYTljLTQwMDkxNjE2ZmEzNlIiLCJ1c2VySWQiOm51bGwsIm9wdE91dCI6ZmFsc2UsInNlc3Npb25JZCI6MTQ5NzMxNjU1MDcyOSwibGFzdEV2ZW50VGltZSI6MTQ5NzMxNzQ0MDM2NSwiZXZlbnRJZCI6MCwiaWRlbnRpZnlJZCI6MCwic2VxdWVuY2VOdW1iZXIiOjB9; _dy_ses_load_seq=30973%3A1497317440658; _ga=GA1.2.959121928.1497305017; _gid=GA1.2.394399346.1497305017; _dy_c_exps=; _dc_gtm_UA-273418-57=1; _dy_soct=92327.125314.1497317440*94588.128721.1497317440; _dycst=dk.w.c.ms.frv4.; _dy_geo=US.NA.US_WA.US_WA_Bellevue; _dy_df_geo=United%20States.Washington.Bellevue; _dy_toffset=0; _dyus_8767255=20%7C0%7C0%7C0%7C0%7C0.0.1485886047286.1497317441743.11431394.0%7C162%7C24%7C5%7C117%7C14%7C0%7C0%7C0%7C0%7C0%7C0%7C14%7C0%7C0%7C0%7C0%7C0%7C14%7C0%7C0%7C0%7C0%7C1";

            String[] cookieTokens = cookieString.Split(new char[] { ';' });

			// Weirdly, though replicating the full cookie set was not necessary, including an empty cookie container in the httpWebRequest *was* necessary.
			CookieContainer requestCookieContainer = new CookieContainer();

			// If we were in fact adding individual cookies to the request, this is what it would look like
            /*
            foreach(String s in cookieTokens)
            {
                String[] kvp = s.Split(new char[] { '=' });
                String name = kvp[0].Trim();
                String value = kvp[1].Trim();
                Cookie c = new Cookie(name, value) { Domain = "www.mapmyrun.com" };
                requestCookieContainer.Add(c);
            }            
            */

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(authUrl);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.Accept = "application/json, text/plain, */*";           
            httpWebRequest.CookieContainer = requestCookieContainer;

			// httpWebRequest.UserAgent = "Mozilla / 5.0(Windows NT 10.0; Win64; x64) AppleWebKit / 537.36(KHTML, like Gecko) Chrome / 58.0.3029.110 Safari / 537.36";
			// httpWebRequest.Referer = "https://www.mapmyrun.com/auth/login";
			// httpWebRequest.Headers.Add("DNT", "1");
			// httpWebRequest.Headers.Add("Accept-Encoding", "gzip, deflate, br");
			// httpWebRequest.Headers.Add("Accept-Language:en-US,en;q=0.8");

			// Under certain circumstances, setting KeepAlive and ProtocolVersion is required to get your auth queries to work, as per this post:
			//
			// https://social.msdn.microsoft.com/Forums/en-US/8f690a5b-a6ec-472c-99f0-e78fb8036254/httpwebrequest-does-not-post-any-data?forum=netfxnetcom
			//
			// Once I stopped including a list of cookies, these became unnecessary for this application.
			//
			// httpWebRequest.KeepAlive = false;
			// httpWebRequest.ProtocolVersion = HttpVersion.Version10;

			// Serialize my username and password to json
			String json = new JavaScriptSerializer().Serialize(new
			{
				username = username,
				password = password
            });

			// Encode the json object to bytes
            byte[] data = Encoding.ASCII.GetBytes(json);
            httpWebRequest.ContentLength = data.Length;

			// Execute the request
            using (Stream stream = httpWebRequest.GetRequestStream()) { stream.Write(data, 0, data.Length); }

			HttpWebResponse httpResponse = null;
			try
			{
				httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			}
			catch (WebException we)
			{
				// If this is an error 400, you probably have an incorrect username/pw
				Console.WriteLine("Error executing auth request: {0}", we.ToString());
				return;
			}

			// The cookies that come back will include the auth tokens we need to execute subsequent requests
            CookieCollection cookies = httpResponse.Cookies;
            CookieContainer container = new CookieContainer();
            container.Add(cookies);

            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                String result = streamReader.ReadToEnd();
                System.IO.File.WriteAllText(authOut, result);
            }

			// Using the cookies we got back from the auth request, ask for some data
            httpWebRequest = (HttpWebRequest)WebRequest.Create(dataUrl);
            httpWebRequest.CookieContainer = container;
            httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                String result = streamReader.ReadToEnd();
                System.IO.File.WriteAllText(dataOut, result);

                if (!dataUrl.Contains("tcx"))
                {
                    // <meta property="og:title" content="Personal GPS: Victoria Marathon 2012" />
                    Regex titleR = new Regex("<.[^>]+og:title[^>]+content=\"(.*)\"[^>]+>");
                    Match m = titleR.Match(result);
                    if (m.Success)
                    {
                        String workoutTitle = m.Groups[1].Value.ToString();
                        Console.WriteLine("Workout title is {0}", workoutTitle);
                    }
                    
                }
            }         

        } // static void Main(string[] args)

    } // class Program

} // namespace AuthTest
