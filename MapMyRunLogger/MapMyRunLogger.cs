/******
 * 
 * MayMyRunLogger.cs
 * 
 * Logs in to mapmyrun.com, downloads all the workouts in a specified .csv file as .tcx/.xml/thumbnail files that 
 * I use to upload to other sites and/or make a pretty Web page.
 * 
 * Dan Morris, 2017
 * 
 * Released under the Granola Bar License: if you find this code useful, send me a granola bar.
 * 
 *******/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net;
using System.Web.Script.Serialization;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;

// Variable assigned but never used (I use these variables when I un-comment a chunk of code to download a certain subset of data)
#pragma warning disable 414

namespace MapMyRunLogger
{  
    public class MapMyRunLogger
    {
        // The list of workouts to download (see readme.txt)
        const String RUN_LIST_PATH = @"..\..\data\workout_history.csv";

        const String LOGIN_PATH = @"..\..\data\login.txt";

        // The MMR authentication URL
        const String MMR_AUTH_URL = "https://www.mapmyrun.com/auth/login";

		// A username and password for authentication
        //
        // Will prompt if blank.
        static String MMR_USERNAME = "";
        static String MMR_PW = "";

		// We're going to retrieve thumbnails for every workout; what size should they be?
        const String IMAGE_SIZE_STRING = "1000x500";
        
        // Sanity-checks on our parsing of the workout list
        const int RAW_TOKENS_PER_LINE = 31;
        const int N_COLUMNS = 15;

        // If 1, don't use parallel.foreach
#if DEBUG
        const int MAX_DOWNLOAD_THREADS = 1;
#else
        const int MAX_DOWNLOAD_THREADS = 50;
#endif

        // Column identifiers for the workout list file
        enum RUN_LIST_COLUMNS : int
        {
            COL_DATE_SUBMITTED = 0,
            COL_DATE,
            COL_TYPE,
            COL_CALORIES,
            COL_DISTANCE,
            COL_DURATION,
            COL_PACE,
            COL_MAXPACE,
            COL_AVGSPEED,
            COL_MAXSPEED,
            COL_HR,
            COL_STEPS,
            COL_NOTES,
            COL_SOURCE,
            COL_LINK
        }

		// Given the username and pw hard-coded above, authenticate with MapMyRun and
		// return a cookie container with the relevant auth tokens.
        static CookieContainer Authenticate()
        {

            CookieContainer requestCookieContainer = new CookieContainer();

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(MMR_AUTH_URL);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.Accept = "application/json, text/plain, */*";
            httpWebRequest.CookieContainer = requestCookieContainer;

            String json = new JavaScriptSerializer().Serialize(new
            {
                username = MMR_USERNAME,
                password = MMR_PW
            });

            byte[] data = Encoding.ASCII.GetBytes(json);
            httpWebRequest.ContentLength = data.Length;

            using (Stream stream = httpWebRequest.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            CookieCollection cookies = httpResponse.Cookies;
            CookieContainer container = new CookieContainer();
            container.Add(cookies);

            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                String authResult = streamReader.ReadToEnd();
            }

            return container;

		} // static CookieContainer Authenticate()


		// Given a cookie container with relevant tokens (e.g. authentication),
		// retrieve the data at the given URL.
		static String GetPage(String url, CookieContainer cookies)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.CookieContainer = cookies;
            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

            String result = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            return result;
        }


        static void Main(string[] args)
        {
            String cwd = System.IO.Directory.GetCurrentDirectory();

            if (!System.IO.Directory.Exists(MMRConstants.OUTPUT_PATH))
            {
                // throw new Exception("Output dir not found");
                System.IO.Directory.CreateDirectory(MMRConstants.OUTPUT_PATH);
            }

            // Parse file
            if (!(System.IO.File.Exists(RUN_LIST_PATH)))
                throw new Exception("Run list file not found");

            // Prompt for login
            if (MMR_USERNAME.Length == 0)
            {
                if (System.IO.File.Exists(LOGIN_PATH))
                {
                    String[] loginLines = System.IO.File.ReadAllLines(LOGIN_PATH);
                    MMR_USERNAME = loginLines[0];
                    MMR_PW = loginLines[1];
                }
                else
                {
                    Console.Write("Enter username: ");
                    MMR_USERNAME = Console.ReadLine().Trim();
                    
                    Console.Write("Enter pw: ");
                    MMR_PW = Console.ReadLine().Trim();
                    
                }
            }

            Console.WriteLine("Using username {0}", MMR_USERNAME);
            Console.WriteLine("Using pw {0}", MMR_PW);
            // Date Submitted,Workout Date,Activity Type,Calories Burned (kCal),Distance (mi),
            //   Workout Time (seconds),Avg Pace (min/mi),Max Pace,Avg Speed (mi/h),Max Speed,
            //   Avg Heart Rate,Steps,Notes,Source,Link

            // Lines look like:
            //
            // "June 11, 2017","June 11, 2017",Run,315,3.08999,1215,6.55,0,9.16031,0,,3626,,,http://www.mapmyrun.com/workout/2255711738

            String[] lines = System.IO.File.ReadAllLines(RUN_LIST_PATH);

            List<Workout> workouts = new List<Workout>();

            // First line is a header
            for(int iLine=1; iLine<lines.Length; iLine++)
            {
                String line = lines[iLine].Trim();
                if (line.Length == 0) continue;

                // This is a semi-quirky regex for handling comma-delimited strings with quotes.  It works,
                // but also produces empty values for every other output token.
                Regex csvSplit = new Regex("(?:^|,)(\"(?:[^\"]+|\"\")*\"|[^,]*)", RegexOptions.Compiled);
                String[] rawTokens = csvSplit.Split(line);
                if (rawTokens.Length != RAW_TOKENS_PER_LINE)
                {
                    throw new Exception(String.Format("Parse error on line {0}: {1}", iLine, line));
                }

                // Remove the even-indexed tokens, which are regex artifacts
                List<String> lTokens = new List<String>();
                for(int iToken=0; iToken<rawTokens.Length; iToken++)
                {
                    if (iToken % 2 == 0) continue;
                    rawTokens[iToken] = Regex.Replace(rawTokens[iToken],"^\"(.*)\"$", "$1");
                    lTokens.Add(rawTokens[iToken]);
                }
                
                if (lTokens.Count != N_COLUMNS)
                {
                    throw new Exception(String.Format("Parse error on line {0}: {1}", iLine, line));
                }

                Workout w = new Workout();

                // They mix "April" with "Feb."; remove periods
                String s = lTokens[(int)(RUN_LIST_COLUMNS.COL_DATE)].Replace(".", "");

                // They only use one non-standard month abbreviation that DateTime.Parse doesn't know about
                s = s.Replace("Sept", "Sep");
                w.date = DateTime.Parse(s);
                w.distanceMiles = Double.Parse(lTokens[(int)(RUN_LIST_COLUMNS.COL_DISTANCE)]);
                w.durationSeconds = Double.Parse(lTokens[(int)(RUN_LIST_COLUMNS.COL_DURATION)]);
                w.link = lTokens[(int)(RUN_LIST_COLUMNS.COL_LINK)];
                
                workouts.Add(w);

            } // ...for each line

            // Find the longest workout
            List<Workout> sortedWorkouts = workouts.OrderByDescending(x => x.distanceMiles).ToList();

            Console.WriteLine("Longest workout ({0} miles on {1}) has URL {2}",
                sortedWorkouts[0].distanceMiles, sortedWorkouts[0].date, sortedWorkouts[0].link);

            // Authenticate
            CookieContainer cookies = Authenticate();

            // Fetch titles, download tcx files
            if (MAX_DOWNLOAD_THREADS <= 1)
                foreach (Workout w in sortedWorkouts) { ProcessWorkout(w, cookies); }
            else
                Parallel.ForEach(sortedWorkouts, new ParallelOptions { MaxDegreeOfParallelism = MAX_DOWNLOAD_THREADS }, (w) => { ProcessWorkout(w, cookies); });


            // ...for each workout

            // Write warnings out to a file
            using (StreamWriter sw = new StreamWriter(System.IO.Path.Combine(MMRConstants.OUTPUT_PATH, "warnings.txt")))
            {
                foreach(Workout w in sortedWorkouts)
                {
                    if (!(w.warning.Length == 0))
                    {
                        sw.WriteLine("Warning in workout {0}:\n{1}", w.id, w.warning);
                    }
                }
            }

        } // static void Main(string[] args)

		// Used only when I un-comment a one-time block I used for downloading runs before a certain date
        static int filesInExportDir = 0;
        static int iExportDir = 0;

		// Given a Workout object that we've retrieved from a URL, do all the stuff we do with it...
		// grab the .tcx data, fetch a thumbnail, etc., then write it out to an .xml file.
        static void ProcessWorkout(Workout wIn, CookieContainer cookies)
        {
            Workout w = wIn;

            System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(w.GetType());

            // For non-authenticated requests
            WebClient wc = new WebClient();

            // If we already have all the data we need about this workout, move on

            // Download the workout page (not the .tcx file) just to get the workout title

            String[] tokens = w.link.Split('/');
            String id = tokens[tokens.Length - 1];
            w.id = id;
            String tcxUrl = "http://www.mapmyrun.com/workout/export/" + id + "/tcx%20HTTP/1.1";
            String tcxOutFile = System.IO.Path.Combine(MMRConstants.OUTPUT_PATH, "tcx_export." + id + ".tcx");
            String imageFile = System.IO.Path.Combine(MMRConstants.OUTPUT_PATH, "thumbnail." + id + ".png");
            String xmlOutFile = System.IO.Path.Combine(MMRConstants.OUTPUT_PATH, "xml_info." + id + ".xml");

            try
            {
                // Read existing XML if available
                if (System.IO.File.Exists(xmlOutFile))
                {
                    Workout deseralizedWorkout = null;
                    using (XmlReader reader = XmlReader.Create(xmlOutFile))
                    {
                        deseralizedWorkout = (Workout)xmlSerializer.Deserialize(reader);
                    }
                    w = deseralizedWorkout;
                }
                
                /*
                // I had already started using Strava when I wrote this project; I ran this block of code once to download 
				// only runs I want to export to Strava, i.e. runs I had @ MMR before I started using Strava.  The general 
				// state of this function, with this block commented out, retrieves everything and generates the whole Web page.

                if (w.date <= new DateTime(2015, 7, 17, 0, 0, 0, 0))
                {
                    if (!System.IO.File.Exists(tcxOutFile))
                    {
                        Console.WriteLine("Error finding file {0}", tcxOutFile);
                    }
                    String exportPath = System.IO.Path.Combine(MMRConstants.OUTPUT_PATH, "strava", string.Format("{0:D2}", iExportDir));
                    if (!System.IO.Directory.Exists(exportPath)) System.IO.Directory.CreateDirectory(exportPath);                    
                    String tcxOutFileStravaExport = System.IO.Path.Combine(exportPath, "tcx_export." + id + ".tcx");
                    System.IO.File.Copy(tcxOutFile, tcxOutFileStravaExport);
                    filesInExportDir++;
                    if (filesInExportDir == 25)
                    {
                        filesInExportDir = 0;
                        iExportDir++;
                    }
                }
                return;
                */

                // Fetch the page content, from which we'll get a title for this workout.  We'll also get links
                // to the thumbnail and the route page.

                String pageData = GetPage(w.link, cookies);

                Regex titleR = new Regex("<.[^>]+og:title[^>]+content=\"(.*)\"[^>]+>");
                Match m = titleR.Match(pageData);
                if (m.Success)
                {
                    String workoutTitle = m.Groups[1].Value;
                    Console.WriteLine("Processing workout {0}: {1}", w.id, workoutTitle);
                    w.title = workoutTitle;
                }
                else
                {
                    String s = String.Format("Could not find title for workout {0}", w.link);
                    w.warning += String.Format("{0}\n", s);
                    Console.WriteLine(s);
                }

                // Fetch the route page for this run, from which we can extract the location.
                //
                // We could also get elevation/climb information from the route page, but I'm not
                // doing that yet.

                // link: "/ca/victoria-british-columbia/personal-gps-victoria-marathon-2012-route-144228347",
                // link: "/routes/view/1611374389",
                Regex routeLinkR = new Regex("link:.*\"(.*route.*)\"");
                m = routeLinkR.Match(pageData);
                if (m.Success)
                {
                    String routeUrl = "http://mapmyrun.com" + m.Groups[1].Value;
                    w.routeLink = routeUrl;
                    String routePageData = GetPage(routeUrl, cookies);

                    // This version of city/state turned out to be US-specific...
                    // Regex locationR = new Regex("route_city:.*\"([^\"]*)\".*route_state:\"([^\"]*)\"", RegexOptions.Singleline);

                    // This version is international-run-friendly
                    /*
                     <meta name="Region" content="WA US" />
                     <meta name="City" content="Redmond" />
                     */
                    Regex locationR = new Regex("name=\"Region\" content=\"([^\"]*)\".*name=\"City\" content=\"([^\"]*)\"", RegexOptions.Singleline);
                    m = locationR.Match(routePageData);
                    if (m.Success)
                    {
                        w.startLocation = m.Groups[2].Value + ", " + m.Groups[1].Value;
                    }
                    else
                    {
                        String s = String.Format("Could not find location for workout {0} ({1})", w.link, w.title);
                        w.warning += String.Format("{0}\n", s);
                        Console.WriteLine(s);
                    }
                }
                else
                {
                    String s = String.Format("Could not find route for workout {0} ({1})", w.link, w.title);
                    w.warning += String.Format("{0}\n", s);
                    Console.WriteLine(s);
                }

                // Download a thumbnail (really the 1000 x N image) for this run
                if (System.IO.File.Exists(imageFile))
                {
                    // <meta property="og:image" content="http://drzetlglcbfx.cloudfront.net/routes/thumbnail/55433136?size=200x200" />
                    Regex thumbnailR = new Regex("<.[^>]+og:image[^>]+content=\"(.*)\"[^>]+>");
                    m = thumbnailR.Match(pageData);
                    if (m.Success)
                    {
                        String originalImageUrl = m.Groups[1].Value;
                        if (!originalImageUrl.Contains("200x200"))
                        {
                            String s = String.Format("Thumbnail id error for workout {0} ({1})", w.link, w.title);
                            w.warning += String.Format("{0}\n", s);
                            Console.WriteLine(s);
                        }
                        String bigImageUrl = originalImageUrl.Replace("200x200", IMAGE_SIZE_STRING);
                        wc.DownloadFile(bigImageUrl, imageFile);
                    }
                    else
                    {
                        String s = String.Format("Could not find thumbnail for workout {0} ({1})", w.link, w.title);
                        w.warning += String.Format("{0}\n", s);
                        Console.WriteLine(s);
                    }
                }

                if (!System.IO.File.Exists(tcxOutFile))
                {
                    // Download the tcx file
                    String tcxData = GetPage(tcxUrl, cookies);
                    System.IO.File.WriteAllText(tcxOutFile, tcxData);
                }

                using (XmlWriter writer = XmlWriter.Create(xmlOutFile))
                {
                    xmlSerializer.Serialize(writer, w);
                }
            }
            catch (Exception e)
            {
                String s = String.Format("Unknown error for workout {0}: {1}", w.link, e.ToString());
                w.warning += String.Format("{0}\n", s);
            }

		} // static void ProcessWorkout(Workout wIn, CookieContainer cookies)

	} // class MapMyRunLogger

} // namespace MapMyRunLogger
