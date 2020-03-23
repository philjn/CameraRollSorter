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
                  
            string imagesDirectory = @"c:\temp\camera";
            IEnumerable<string> files = System.IO.Directory.EnumerateFiles(imagesDirectory, "*.*", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                try
                {
                    IEnumerable<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(file);
                    var subIfdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                    var dateTime = subIfdDirectory?.GetDescription(ExifIfd0Directory.TagDateTime);
                    Trace.WriteLine($"File: {file}, DateTime: {dateTime}");
                    if (DateTime.TryParseExact(dateTime, dateTimePattern, null, System.Globalization.DateTimeStyles.None, out DateTime imageDate))
                    {
                        var imageYear = imageDate.Year;
                        var imageMonth = imageDate.Month;
                        var yearDir = Path.Combine(imagesDirectory, imageYear.ToString());
                        if(!System.IO.Directory.Exists(yearDir))
                        {
                            System.IO.Directory.CreateDirectory(yearDir);
                        }

                        var yearMonthDir = Path.Combine(yearDir, imageMonth.ToString("00"));
                        if(!System.IO.Directory.Exists(yearMonthDir))
                        {
                            System.IO.Directory.CreateDirectory(yearMonthDir);
                        }

                        File.Copy(file, Path.Combine(yearMonthDir, Path.GetFileName(file)));
                    }
                }
                catch
                {
                    Trace.WriteLine($"Skipping {file}, could not retrieve image data");
                }
            }
        }
    }
}
