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
        static void Main(string[] args)
        {
            const string dateTimePattern = "yyyy:MM:dd HH:mm:ss";
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Listeners.Add(new DefaultTraceListener());
                  
            string imagesDirectory = @"G:\OlderPictures\Bulk";
            IEnumerable<string> files = System.IO.Directory.EnumerateFiles(imagesDirectory, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                try
                {
                    IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(file);
                    var subIfdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                    var dateTime = subIfdDirectory?.GetDescription(ExifIfd0Directory.TagDateTime);
                    if(string.IsNullOrEmpty(dateTime))
                    {
                        var alternateSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                        dateTime = alternateSubIfdDirectory?.GetDescription(ExifSubIfdDirectory.TagDateTimeOriginal);
                    }

                    if (string.IsNullOrEmpty(dateTime))
                    {
                        Debugger.Break();
                    }

                    Trace.WriteLine($"File: {file}, DateTime: {dateTime}");
                    if (DateTime.TryParseExact(dateTime, dateTimePattern, null, System.Globalization.DateTimeStyles.None, out DateTime imageDate))
                    {
                        var imageYear = imageDate.Year;
                        var imageMonth = imageDate.Month;
                        string fullMonthName = imageDate.ToString("MMMM");
                        string fullYear = imageDate.ToString("yyyy");
                        var yearDir = Path.Combine(imagesDirectory, fullYear);
                        if(!System.IO.Directory.Exists(yearDir))
                        {
                            System.IO.Directory.CreateDirectory(yearDir);
                        }

                        var yearMonthDir = Path.Combine(yearDir, $"{fullMonthName} {fullYear}");
                            
                        if(!System.IO.Directory.Exists(yearMonthDir))
                        {
                            System.IO.Directory.CreateDirectory(yearMonthDir);
                        }

                        File.Copy(file, GetPhotoFileName(file, imageMonth.ToString("00"), imageDate.Day.ToString("00"), fullYear));
                    }
                }
                catch
                {
                    Trace.WriteLine($"Skipping {file}, could not retrieve image data");
                }
            }
        }

        public static string GetPhotoFileName(string originalFilename, string month, string day, string year)
        {
            int index = 1;
            string extension = Path.GetExtension(originalFilename);
            string newFileName = Path.Combine(Path.GetDirectoryName(originalFilename), $"{year}{month}{day}_{index.ToString("0000")}{extension}");
            while(File.Exists(newFileName))
            {
                newFileName = Path.Combine(Path.GetDirectoryName(newFileName), $"{year}{month}{day}_{index.ToString("0000")}{extension}");
                index++;
            }
            return newFileName;
        }
    }
}
