using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Avi;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using MetadataExtractor.Util;
using Microsoft.WindowsAPICodePack.Shell;

namespace CameraRollSorter
{
    class Program
    {
        public const string dateTimePattern = "yyyy:MM:dd HH:mm:ss";

        public const string dateTimePatternFS = "ddd MMM dd HH:mm:ss zzz yyyy"; // Wed Dec 17 22:56:46 -08:00 2008

        public const string dateTimeMediaPatternFS = "ddd MMM dd HH:mm:ss yyyy"; // THU DEC 07 22:09:22 2006

        static void Main(string[] args)
        {
            
            //Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Listeners.Add(new DefaultTraceListener());
            //Trace.Listeners.Add(new TextWriterTraceListener(DateTime.Now.ToString("yyyy-MM-dd-HH-mm") +".log"));

            string imagesDirectory = args[0];
            string outputDirectory = args[1];
            string backupDirectory = args[2];
            string fileExtension = args[3];

            bool enableBackup = false;

            if (!System.IO.Directory.Exists(outputDirectory))
            {
                System.IO.Directory.CreateDirectory(outputDirectory);
            }

            if (enableBackup && !System.IO.Directory.Exists(backupDirectory))
            {
                System.IO.Directory.CreateDirectory(backupDirectory);
            }

            IEnumerable<string> files = System.IO.Directory.EnumerateFiles(imagesDirectory, fileExtension, SearchOption.AllDirectories);
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

                        string yearMonthDir = Path.Combine(yearDir, $"{fullMonthName} {fullYear}");

                        // Favorites and highlights are in the year month dir 
                        if (IsFavorite(file))
                        {
                            yearMonthDir = Path.Combine(yearMonthDir, $"{fullMonthName} {fullYear} Favorites");
                        }
                        else if (IsHighlights(file))
                        {
                            yearMonthDir = Path.Combine(yearMonthDir, $"{fullMonthName} {fullYear} Highlights");
                        }
                            
                        if(!System.IO.Directory.Exists(yearMonthDir))
                        {
                            System.IO.Directory.CreateDirectory(yearMonthDir);
                        }

                        string mediaFileName = null;
                        if (fileExtension == "*.jpg")
                        {
                            mediaFileName = GetPhotoFileName(file, yearMonthDir, imageMonth.ToString("00"), imageDate.Day.ToString("00"), fullYear);
                        }
                        else
                        {
                            // retain original filename
                            mediaFileName = GetMediaFileName(file, yearMonthDir, imageMonth.ToString("00"), imageDate.Day.ToString("00"), fullYear);
                        }

                        if (mediaFileName != null)
                        {
                            Trace.WriteLine($"Copy {file} to {mediaFileName}");

                            File.Copy(file, mediaFileName);
                        }
                        else
                        {
                            Trace.WriteLine($"Skipping {file}, as the same one already exists in destination");
                        }

                        if (enableBackup)
                        {
                            var backupFilename = GetBackupPath(file, backupDirectory);

                            Trace.WriteLine($"Moving {file} to {backupFilename}");
                            File.Move(file, backupFilename);
                        }

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
                if (GetFileHash(newFileName) == GetFileHash(originalFilename))
                {
                    return null;
                }

                index++;
                newFileName = Path.Combine(destination, $"{year}{month}{day}_{index.ToString("0000")}{extension}");
            }
            return newFileName;
        }

        /// <summary>
        /// Takes original filename and puts in the correct destination
        /// </summary>
        /// <param name="originalFilename"></param>
        /// <param name="destination"></param>
        /// <param name="month"></param>
        /// <param name="day"></param>
        /// <param name="year"></param>
        /// <returns></returns>
        public static string GetMediaFileName(string originalFilename, string destination, string month, string day, string year)
        {
            int index = 1;
            string extension = Path.GetExtension(originalFilename);
            string newFileName = Path.Combine(destination, $"{Path.GetFileName(originalFilename)}");
            while (File.Exists(newFileName))
            {
                if (GetFileHash(newFileName) == GetFileHash(originalFilename))
                {
                    return null;
                }

                index++;
                newFileName = Path.Combine(destination, $"{Path.GetFileNameWithoutExtension(originalFilename)}_{index.ToString("0000")}{extension}");
            }
            return newFileName;
        }

        public static DateTime GetDateCreated(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            if (extension == ".jpg")
            {
                DateTime dateTime = GetImageDateCreated(fileName);
                // something isn't right if it's less than 1990, Digital cameras didn't exist then ;)
                if (dateTime.Year < 1990)
                {
                    return GetFileCreateTime(fileName);
                }
                return dateTime;
            }
            else
            {
                DateTime dateTime = GetVideoDateCreated(fileName);
                // something isn't right if it's less than 1990, Digital cameras didn't exist then ;)
                if (dateTime.Year < 1990)
                {
                    return GetFileCreateTime(fileName);
                }
                return dateTime;
            }
        }

        public static DateTime GetVideoDateCreated(string fileName)
        {
            try
            {
                IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(fileName);
                var aviDirectory = directories.OfType<AviDirectory>().FirstOrDefault();
                if (aviDirectory != null)
                {
                    string dateTimeAsString = aviDirectory.GetDescription(AviDirectory.TagDateTimeOriginal);
                    if (DateTime.TryParseExact(dateTimeAsString, dateTimeMediaPatternFS, null, System.Globalization.DateTimeStyles.None, out DateTime mediaDatetime))
                    {
                        return mediaDatetime;
                    }
                }

                var movDirectory = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (movDirectory != null)
                {
                    if (movDirectory.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out DateTime dateTime))
                    {
                        return dateTime;
                    }
                }
            }
            catch(ImageProcessingException ipe)
            {
                Trace.WriteLine($"Couldn't determine Video filetype {ipe.Message}");
            }

            var shellFile = ShellFile.FromFilePath(fileName);
            if (shellFile != null)
            {
                var dateCreated = shellFile.Properties.System.Media.DateEncoded;
                if (dateCreated != null)
                {
                    if (dateCreated.Value.HasValue)
                    {
                        return dateCreated.Value.Value;
                    }
                }
            }

            // when all else fails return the created time
            return GetFileCreateTime(fileName);
        }

        public static DateTime GetImageDateCreated(string fileName)
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

            if (!DateTime.TryParseExact(dateTime, dateTimePattern, null, System.Globalization.DateTimeStyles.None, out imageDateTime))
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

        public static string GetFileHash(string file)
        {
            string hash = string.Empty;
            try
            {
                using (SHA256 mySHA256 = SHA256.Create())
                // Create a fileStream for the file.
                using (FileStream fileStream = new FileStream(file, FileMode.Open))
                {
                    // Be sure it's positioned to the beginning of the stream.
                    fileStream.Position = 0;
                    // Compute the hash of the fileStream.
                    byte[] hashValue = mySHA256.ComputeHash(fileStream);
                    // Write the name and hash value of the file to the console.
                    hash = HashtoString(hashValue);
                    // Close the file.
                    fileStream.Close();
                }
            }
            catch (IOException e)
            {
                Trace.WriteLine($"I/O Exception: {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Trace.WriteLine($"Access Exception: {e.Message}");
            }

            return hash;
        }

        // Display the byte array in a readable format.
        public static string HashtoString(byte[] array)
        {
            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < array.Length; i++)
            {
                sBuilder.Append(array[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        public static bool IsFavorite(string filename)
        {
            return Path.GetDirectoryName(filename).Contains("favorite");
        }

        public static bool IsHighlights(string filename)
        {
            return Path.GetDirectoryName(filename).Contains("highlight");
        }

        public static DateTime GetFileCreateTime(string file)
        {
            return File.GetLastWriteTime(file);
        }
    }
}
