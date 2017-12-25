/******
 * 
 * RunPageGenerator.cs
 * 
 * Given the .xml files and thumbnails retrieved via MapMyRunLogger, generates a pretty Web page.
 * 
 * Dan Morris, 2017
 * 
 * Released under the Granola Bar License: if you find this code useful, send me a granola bar.
 * 
 *******/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MapMyRunLogger
{
    public class RunPageGenerator
    {
        const String HTML_OUTPUT_DIR = @"..\..\data\HTML";
        static String HTML_THUMBS_DIR = System.IO.Path.Combine(HTML_OUTPUT_DIR, "thumbs");
        const String HTML_TEMPLATE = "run_log_template.html";
        const String HTML_OUTPUT_FILE = "index.html";

        const int THUMBNAIL_W = 150;
        const int THUMBNAIL_H = 75;

        // I used this list to manually ignore runs that were uninteresting, but in ways that
        // weren't easy to automate.
        static List<String> ignoreIds = new List<String>()
        {
            "191148587", "2165938553"
        };

		// This list lets me manually replace a few run names that were either not retrieved correctly
		// or otherwise problematic as reported by MMR.
        static List<String> customNames = new List<String>()
        {
            "191379775:2012 Victoria Marathon",
            "33891958:2010 Rock 'n Roll Las Vegas Marathon",
            "1350188:",
            "75080108:",
            "1709491742:Iron Horse Half",
            "1728825173:Labor Day Half",
            "854103369:Rain Run",
            "106725882:Mercer Island Half",
            "1462996013:Cinco de Mayo Half",
            "1320035709:Rain Run",
            "372511309:Labor Day Half",
            "1157741835:Labor Day Half",
            "1207192289:Snohomish River Run",
            "1029170451:Snoqualmie Valley Half"

        };

        static void Main(string[] args)
        {

            String HTML_OUTPUT_PATH = System.IO.Path.Combine(HTML_OUTPUT_DIR, HTML_OUTPUT_FILE);
            String HTML_TEMPLATE_PATH = System.IO.Path.Combine(HTML_OUTPUT_DIR, HTML_TEMPLATE);

            if (!System.IO.Directory.Exists(HTML_OUTPUT_DIR))
            {
                // throw new Exception(String.Format("Could not find html directory, currently in {0}", Environment.CurrentDirectory));
                System.IO.Directory.CreateDirectory(HTML_OUTPUT_DIR);
            }

            if (!System.IO.Directory.Exists(HTML_THUMBS_DIR))
            {
                System.IO.Directory.CreateDirectory(HTML_THUMBS_DIR);
            }

            // Enumerate all .xml files (workouts)
            String[] workoutFiles = System.IO.Directory.GetFiles(MMRConstants.OUTPUT_PATH, "*.xml");

            Workout dummy = new Workout();
            System.Xml.Serialization.XmlSerializer xmlSerializer = new System.Xml.Serialization.XmlSerializer(dummy.GetType());

            List<Workout> workouts = new List<Workout>();
            int iWorkout = 0;

			// For every workout...
            //
            // Decide whether we're going to include this workout
            foreach (String workoutFile in workoutFiles)
            {
                // Read the data for this workout
                Workout w = null;
                using (XmlReader reader = XmlReader.Create(workoutFile)) { w = (Workout)xmlSerializer.Deserialize(reader); }

				// Are we supposed to be ignoring this workout?
                bool ignore = false;
                foreach (String ignoreId in ignoreIds)
                {
                    if (ignoreId.Equals(w.id))
                    {
                        Console.WriteLine("Ignoring workout {0} ({1})", w.id, w.title);
                        ignore = true;
                        break;
                    }
                }
                if (ignore) continue;

                foreach(String customName in customNames)
                {
                    if (customName.StartsWith(w.id))
                    {
                        String[] tokens = customName.Split(':');
                        w.title = tokens[1];
                    }
                }

                // Ignore workouts that are so short that they clearly aren't real
                if (w.distanceMiles <= 2 || w.durationSeconds == 0)
                {
                    Console.WriteLine("Ignoring bogus workout {0} ({1}, {2}, {3})", w.id, w.title, w.distanceMiles, w.durationSeconds);
                    continue;
                }
                
                // Is this workout interesting?
                bool interesting = false;

                String titleString = w.title.Trim();

                // Convert first char to upper
                if (titleString.Length > 0)
                    titleString = titleString.First().ToString().ToUpper() + titleString.Substring(1);

				// Don't replicate the MMR generic titles, better to have no title
                if (titleString.ToLower().StartsWith("mapmyrun")) titleString = "";
                if (titleString.StartsWith("Regular Run")) titleString = "";
                if (titleString.StartsWith("A MapMy")) titleString = "";
                if (titleString.StartsWith("Run / Jog")) titleString = "";
                if (titleString.ToLower().StartsWith("unknown")) titleString = "";
                if (titleString.ToLower().Equals("run")) titleString = "";
                if (titleString.ToLower().Equals("workout")) titleString = "";
                if (titleString.ToLower().Equals("regular run")) titleString = "";
                w.title = titleString;

                // Convert first char to upper
                String location = w.startLocation;
                if (location.Length > 0)
                    location = location.First().ToString().ToUpper() + location.Substring(1);
                w.startLocation = location;
                location = location.ToLower().Trim();

                double minutesPerMile = (w.durationSeconds / 60) / w.distanceMiles;

                // Not runs... this is likely one of the few times I got on a bike...
                if (minutesPerMile < 5) continue;

                w.paceMinutesPerMile = minutesPerMile;

                // First determine if we want to keep this run...

                // if (w.title.Length > 0) interesting = true;

                // Long runs are interesting
                if (w.distanceMiles >= 15) interesting = true;

                // Fast runs are interesting
                if (minutesPerMile < 8) interesting = true;

                // Intervals are interseting
                if (w.title.ToLower().Contains("x")) interesting = true;

                // Runs in unusual places are interesting
                if (location.Length > 0 && (!location.Contains("wa"))) interesting = true;

                if (!interesting) continue;

                // Give weights (priorities) to runs that will affect how they get sorted in the output list...
                //
                // Default weight is zero.

                // Really long runs (marathons)
                if (w.distanceMiles >= 25) w.weight = 10.0;

                // 5k races
                else if (minutesPerMile <= 6.5 && w.title.Contains("5k")) w.weight = 6.0;

                // Fast long runs (half marathons)
                else if (w.distanceMiles >= 12.2 && minutesPerMile < 8) w.weight = 5.0;

                // Fast short runs
                else if (minutesPerMile <= 6.75) w.weight = 3.0;

                // Runs in interesting places
                else if (!location.Contains("wa")) w.weight = 2.0;

                // Interval repeats
                else if (titleString.Contains("x")) w.weight = 1.0;

                // else if (titleString.Contains("arathon") || titleString.Contains("5k")) w.weight = 3.0;
                
                workouts.Add(w);

            } // ...for each workout

            // Sort the list by weight (interestingness)
            workouts = workouts.OrderByDescending(x => x.weight).ThenBy(x => x.paceMinutesPerMile).ToList();

            // Generate HTML
            StringBuilder htmlStringBuilder = new StringBuilder();

            iWorkout = 0;

            // For each workout...
            //
            // Copy thumbnails, create HTML table rows
            foreach (Workout w in workouts)
            {
                Console.WriteLine("Processing workout {0} of {1}: {2}", iWorkout, workouts.Count, w.title);

                // Copy this workout's thumbnail

                bool includeImage = true;

                String thumbnailInputPath = System.IO.Path.Combine(MMRConstants.OUTPUT_PATH, "thumbnail." + w.id + ".png");
                // String smallThumbnailInputPath = System.IO.Path.Combine(MMRConstants.OUTPUT_PATH, "thumbnail." + w.id + ".small.png");

                if (!System.IO.File.Exists(thumbnailInputPath)) includeImage = false;

                if (includeImage)
                {
                    System.Drawing.Image img = System.Drawing.Image.FromFile(thumbnailInputPath);

                    // Weird blank thumbnails come up as black squares
                    if (img.Width == img.Height) includeImage = false;

                    String outfile = System.IO.Path.Combine(HTML_THUMBS_DIR, "thumbnail." + w.id + ".png");
                    if (!System.IO.File.Exists(outfile)) System.IO.File.Copy(thumbnailInputPath, outfile, false);

                    String smallOutfile = System.IO.Path.Combine(HTML_THUMBS_DIR, "thumbnail." + w.id + ".small.png");
                    Image smallImage = Resize(img, THUMBNAIL_W, THUMBNAIL_H, false);
                    smallImage.Save(smallOutfile, ImageFormat.Png);                    
                }

				// Create a row with odd or even row class
				String tdClass = "oddruntdclass";
                if (iWorkout % 2 == 0) tdClass = "evenruntdclass";
                htmlStringBuilder.Append(String.Format("<tr class=\"runtrclass\">\n"));

				// Put run IDs in comments
                String commentString = "<!-- " + w.id + "-->" + String.Format("\n");
                htmlStringBuilder.Append(commentString);

				// Clean up and embed the date string
                String dateString = w.date.ToString("MMM d, yyyy").Replace(" ","&nbsp;");
                htmlStringBuilder.Append(String.Format("<td class=\"runtrclass {0}\"><p class=\"innerpclass\">{1}</p></td>\n",
                    tdClass, dateString));

				// Format and embed the duration
                // String durationString = w.durationSeconds.ToString();
                TimeSpan t = TimeSpan.FromSeconds(w.durationSeconds);
                String durationString = String.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);                    
                htmlStringBuilder.Append(String.Format("<td class=\"runtrclass {0}\"><p class=\"innerpclass\">{1}</p></td>\n",
                    tdClass, durationString));

				// Format and embed the distance
				String milesString = w.distanceMiles.ToString("F1");
                htmlStringBuilder.Append(String.Format("<td class=\"runtrclass {0}\"><p class=\"innerpclass\">{1}</p></td>\n",
                    tdClass, milesString));

				// Format and embed the pace
				double paceMinutesPerMile = (w.durationSeconds / w.distanceMiles) / 60;
                t = TimeSpan.FromMinutes(paceMinutesPerMile);
                String paceString = String.Format("{0:D1}:{1:D2}", t.Minutes, t.Seconds);
                htmlStringBuilder.Append(String.Format("<td class=\"runtrclass {0}\"><p class=\"innerpclass\">{1}</p></td>\n",
                    tdClass, paceString));

				// Format and embed the title
				String titleString = w.title;
                htmlStringBuilder.Append(String.Format("<td class=\"runtrclass {0}\"><p class=\"innerpclass\">{1}</p></td>\n",
                    tdClass, titleString));

				// Format and embed the location
				String locationString = w.startLocation.Trim();
                if (locationString.EndsWith(" US")) locationString = locationString.Replace(" US", "");
                htmlStringBuilder.Append(String.Format("<td class=\"runtrclass {0}\"><p class=\"innerpclass\">{1}</p></td>\n",
                    tdClass, locationString));

                // thumbnail.675686.png
                if (includeImage)
                {
                    String imgLink = String.Format("<a href=\"thumbs/thumbnail.{0}.png\"><img class=\"thumbnailimg\" src=\"thumbs/thumbnail.{1}.small.png\"/></a>",
                        w.id, w.id);
                    htmlStringBuilder.Append(String.Format("<td class=\"runtrclass {0}\">{1}</td>\n",
                        tdClass, imgLink));
                }
                else
                {
                    htmlStringBuilder.Append(String.Format("<td style=\"padding-top:15px;\" class=\"runtrclass {0}\"><p class=\"innerpclass\">{1}</p></td>\n",
                        tdClass, ""));
                }

                iWorkout++;

            } // ...for each workout

            String contentHtml = htmlStringBuilder.ToString();

            String html = System.IO.File.ReadAllText(HTML_TEMPLATE_PATH);

            html = html.Replace("$CONTENT", contentHtml);

            // Write output file
            System.IO.File.WriteAllText(HTML_OUTPUT_PATH, html);

		} // static void Main(string[] args)

        // From http://www.c-sharpcorner.com/article/resize-image-in-c-sharp/

        /// <summary>  
        /// resize an image and maintain aspect ratio  
        /// </summary>  
        /// <param name="image">image to resize</param>  
        /// <param name="newWidth">desired width</param>  
        /// <param name="maxHeight">max height</param>  
        /// <param name="onlyResizeIfWider">if image width is smaller than newWidth use image width</param>  
        /// <returns>resized image</returns>  
        public static Image Resize(Image image, int newWidth, int maxHeight, bool onlyResizeIfWider)
        {
            if (onlyResizeIfWider && image.Width <= newWidth) newWidth = image.Width;

            var newHeight = image.Height * newWidth / image.Width;
            if (newHeight > maxHeight)
            {
                // Resize with height instead  
                newWidth = image.Width * maxHeight / image.Height;
                newHeight = maxHeight;
            }

            var res = new Bitmap(newWidth, newHeight);

            using (var graphic = Graphics.FromImage(res))
            {
                graphic.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphic.SmoothingMode = SmoothingMode.HighQuality;
                graphic.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphic.CompositingQuality = CompositingQuality.HighQuality;
                graphic.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return res;
        }

    } // public class RunPageGenerator

} // namespace MapMyRunLogger
