using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace CameraRollSorter
{
    class Program
    {
        public const string dateTimePattern = "yyyy:MM:dd HH:mm:ss";

        public const string dateTimePatternFS = "ddd MMM dd HH:mm:ss zzz yyyy"; // Wed Dec 17 22:56:46 -08:00 2008

        static void Main(string[] args)
        {
            
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Listeners.Add(new DefaultTraceListener());
            Trace.Listeners.Add(new TextWriterTraceListener(DateTime.Now.ToString("yyyy-MM-dd-HH-mm") +".log"));

            string imagesDirectory = args[0];
            string outputDirectory = args[1];
            string backupDirectory = args[2];

            if (!System.IO.Directory.Exists(outputDirectory))
            {
                System.IO.Directory.CreateDirectory(outputDirectory);
            }

            if (!System.IO.Directory.Exists(backupDirectory))
            {
                System.IO.Directory.CreateDirectory(backupDirectory);
            }

            IEnumerable<string> files = System.IO.Directory.EnumerateFiles(imagesDirectory, "*.jpg", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                try
                {
                    var imageDate = GetDateCreated(file);
                    Trace.WriteLine($"File: {file}, DateTime: {imageDate}");
                    if (imageDate != DateTime.MinValue)
                    {
                        var imageYear = imageDate.Year;
                        var imageMonth = imageDate.Month;
                        string fullMonthName = imageDate.ToString("MMMM");
                        string fullYear = imageDate.ToString("yyyy");
                        var yearDir = Path.Combine(outputDirectory, fullYear);
                        if(!System.IO.Directory.Exists(yearDir))
                        {
                            System.IO.Directory.CreateDirectory(yearDir);
                        }

                        var yearMonthDir = Path.Combine(yearDir, $"{fullMonthName} {fullYear}");
                            
                        if(!System.IO.Directory.Exists(yearMonthDir))
                        {
                            System.IO.Directory.CreateDirectory(yearMonthDir);
                        }

                        var photoFileName = GetPhotoFileName(file, yearMonthDir, imageMonth.ToString("00"), imageDate.Day.ToString("00"), fullYear);

                        Trace.WriteLine($"Copy {file} to {photoFileName}");

                        File.Copy(file, photoFileName);

                        var backupFilename = GetBackupPath(file, backupDirectory);

                        Trace.WriteLine($"Moving {file} to {backupFilename}");
                        File.Move(file, backupFilename);
                    }
                    else
                    {
                        Trace.WriteLine($"Couldn't parse proper DateTime from : {file}");
                        Debugger.Break();
                    }
                }
                catch
                {
                    Trace.WriteLine($"Skipping {file}, could not retrieve image data");
                }
            }
        }

        public static string GetPhotoFileName(string originalFilename, string destination, string month, string day, string year)
        {
            int index = 1;
            string extension = Path.GetExtension(originalFilename);
            string newFileName = Path.Combine(destination, $"{year}{month}{day}_{index.ToString("0000")}{extension}");
            while(File.Exists(newFileName))
            {
                index++;
                newFileName = Path.Combine(destination, $"{year}{month}{day}_{index.ToString("0000")}{extension}");
            }
            return newFileName;
        }

        public static DateTime GetDateCreated(string fileName)
        {
            IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(fileName);
            var alternateSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var dateTime = alternateSubIfdDirectory?.GetDescription(ExifSubIfdDirectory.TagDateTimeOriginal);
            DateTime imageDateTime;

            if (string.IsNullOrEmpty(dateTime))
            {
                var subIfdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                dateTime = subIfdDirectory?.GetDescription(ExifIfd0Directory.TagDateTime);
            }

            if (string.IsNullOrEmpty(dateTime))
            {
                var fileCreateTime = directories.OfType<MetadataExtractor.Formats.FileSystem.FileMetadataDirectory>().FirstOrDefault();
                dateTime = fileCreateTime?.GetDescription(MetadataExtractor.Formats.FileSystem.FileMetadataDirectory.TagFileModifiedDate);
            }

            if (string.IsNullOrEmpty(dateTime))
            {
                Debugger.Break();
            }

            if(!DateTime.TryParseExact(dateTime, dateTimePattern, null, System.Globalization.DateTimeStyles.None, out imageDateTime))
            {
                DateTime.TryParseExact(dateTime, dateTimePatternFS, null, System.Globalization.DateTimeStyles.None, out imageDateTime);
            }

            return imageDateTime;
        }

        public static string GetBackupPath(string file, string destination)
        {
            string sourceRoot = Path.GetPathRoot(file);
            file = file.Replace(sourceRoot, "");
            string destPath = Path.Combine(destination, file);
            var destDir = Path.GetDirectoryName(destPath);
            if (!System.IO.Directory.Exists(destDir))
            {
                // this will create the whole path
                System.IO.Directory.CreateDirectory(destDir);
            }
            return destPath;
        }
    }
}
