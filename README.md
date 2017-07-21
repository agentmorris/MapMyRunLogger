# MyMyRunLogger

This is a set of tools I use to bulk-download data from MapMyRun, then make a nice Web page about my workouts:

[http://dmorris.net/projects/runlog/](http://dmorris.net/projects/runlog/)

I also used the downloaded files to upload my MMR history to Strava.

There are three C# projects; all of them live in MyMyRunLogger.sln.


##  MyMyRunLogger.csproj

This is the main project that actually downloads stuff from MapMyRun.  Specifically, it takes a list of my workout IDs, logs in to MMR, downloads .tcx files for every workout, and generates an .xml file and a thumbnail for every workout.  After running this project, you'll have the pile of .tcx files you need to upload to Strava.  Username and password are hard-coded.

There are three ways one could theoretically get a list of workout IDs, not counting a fully-manual approach... 

(1) I got the list of workout IDs by paying $5 to become a premium member, downloading my whole workout history as a .csv file (which only gives me IDs for each workout, no other useful information), and canceling my membership.  This project is built around this approach.

(2) One could write new code to log in to MMR using the same auth code, then crawl one's account.  This is harder than it used to be since there is no workout list anymore, just a javascript-based workout calendar.

(3) One could manually open every workout in your browser, and write code to crawl your browser history for URLs.  I estimate the workout-clicking could be done at about two workouts per second, so this would be pretty quick, but it was worth my $5 to avoid doing this.


## RunPageGenerator.csproj

This is the code that takes all those thumbnails and .xml files (from MyMyRunLogger.csproj) and makes a pretty Web page like this one:

[http://dmorris.net/projects/runlog/](http://dmorris.net/projects/runlog/)


## AuthTest.csproj

Some basic tools for testing the code I use to log in and authenticate at MapMyRun.
