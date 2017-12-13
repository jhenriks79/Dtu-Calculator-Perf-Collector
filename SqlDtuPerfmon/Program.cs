using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace SqlDtuPerfmon
{
    public class Program
    {
        private static readonly CultureInfo _cultureInfo = CultureInfo.GetCultureInfo("en-US");
        private static readonly List<PerfItem> PerfItems = new List<PerfItem>();

        public static void Main(string[] args)
        {
            try
            {
                // Get all the perf counters
                var performanceCounters = new List<PerformanceCounter>
                {
                    GetProcessorCounter(),
                    GetDiskReadCounter(),
                    GetDiskWriteCounter()
                };

                // Check if sql exists - if not proceed anyway
                var sqlCounter = GetSqlCounter();
                if (sqlCounter != null)
                    performanceCounters.Add(sqlCounter);

                CollectCounters(performanceCounters);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                Console.ReadLine();
            }
        }

        private static void CollectCounters(List<PerformanceCounter> performanceCounters)
        {
            var sampleInterval = int.Parse(ConfigurationManager.AppSettings["SampleInterval"]);
            var maxSamples = int.Parse(ConfigurationManager.AppSettings["MaxSamples"]);

            // Check if existing perfmon file
            var fileInfo = new FileInfo(ConfigurationManager.AppSettings["CsvPath"]);
            var fileExt = fileInfo.Extension;

            var filePath = fileInfo.FullName.Replace(fileExt, "");
            filePath = $"{filePath}-{DateTime.Now:yyyyMMddhhmmss}{fileExt}";

            // This should never happen but try to delete the file
            if (File.Exists(filePath)) File.Delete(filePath);

            for (var i = 0; i < maxSamples; i++)
            {
                if (Console.KeyAvailable) break;

                // Collect the counters
                PerfItems.Add(new PerfItem
                {
                    CollectedDate = DateTime.UtcNow,
                    Cpu = performanceCounters[0].NextValue(),
                    DiskRead = performanceCounters[1].NextValue(),
                    DiskWrite = performanceCounters[2].NextValue(),
                    LogBytes = performanceCounters.Count == 4 ? performanceCounters[3].NextValue() : 0f
                });

                // Write the counters to disk and to the console
                WriteCounters(PerfItems[PerfItems.Count - 1], i, filePath);
                DisplayCounters(PerfItems[PerfItems.Count - 1], i);

                Thread.Sleep(sampleInterval * 1000);
            }
        }

        private static void DisplayCounters(PerfItem perfItem, int i)
        {
            const int offset = 22;

            if (i == 0)
                Console.WriteLine($"|{"Interval",offset}|{"Cpu",offset}|{"Disk Read",offset}|{"Disk Write",offset}|{"Log Bytes",offset}|");

            Console.WriteLine($"|{perfItem.CollectedDate,offset}|{perfItem.Cpu,offset}|{perfItem.DiskRead,offset}|{perfItem.DiskWrite,offset}|{perfItem.LogBytes,offset}|");
        }

        private static PerformanceCounter GetDiskReadCounter()
        {
            return GetPerfCounter(ConfigurationManager.AppSettings["DiskCategory"],
                ConfigurationManager.AppSettings["DiskInstance"], ConfigurationManager.AppSettings["DiskCounter1"]);
        }

        private static PerformanceCounter GetDiskWriteCounter()
        {
            return GetPerfCounter(ConfigurationManager.AppSettings["DiskCategory"],
                ConfigurationManager.AppSettings["DiskInstance"], ConfigurationManager.AppSettings["DiskCounter2"]);
        }

        private static PerformanceCounter GetPerfCounter(string category, string instance, string counter)
        {
            var perfCateogies = PerformanceCounterCategory.GetCategories();
            PerformanceCounterCategory perfCategory = null;

            foreach (var item in perfCateogies)
                if (item.CategoryName.Equals(category, StringComparison.OrdinalIgnoreCase))
                {
                    perfCategory = item;
                    break;
                }

            if (perfCategory == null)
                throw new Exception(
                    string.Format(_cultureInfo,
                        $"{category} doesn't exist. Try running perfmon.exe to identify the correct {category} category."));

            var perfInstance = perfCategory.GetCounters(instance);

            if (perfInstance.Length == 0)
                throw new Exception(
                    string.Format(_cultureInfo,
                        $"{instance} doesn't exist. Try running perfmon.exe to identify the correct {instance} instance."));

            PerformanceCounter perfCounter = null;

            foreach (var item in perfInstance)
                if (item.CounterName.Equals(counter, StringComparison.OrdinalIgnoreCase))
                {
                    perfCounter = item;
                    break;
                }

            if (perfCounter != null) return perfCounter;

            throw new Exception(
                string.Format(_cultureInfo,
                    $"{counter} doesn't exist. Try running perfmon.exe to identify the correct {counter} counter."));
        }

        private static PerformanceCounter GetProcessorCounter()
        {
            return GetPerfCounter(ConfigurationManager.AppSettings["ProcessorCategory"],
                ConfigurationManager.AppSettings["ProcessorInstance"],
                ConfigurationManager.AppSettings["ProcessorCounter"]);
        }

        private static PerformanceCounter GetSqlCounter()
        {
            // Wrap with try/catch so counter is optional
            try
            {
                return GetPerfCounter(ConfigurationManager.AppSettings["SqlCategory"],
                    ConfigurationManager.AppSettings["SqlInstance"], ConfigurationManager.AppSettings["SqlCounter"]);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                Console.WriteLine();
                return null;
            }
        }

        private static void WriteCounters(PerfItem perfItem, int i, string filePath)
        {
            // Open the file for writing
            using (var streamWriter = File.AppendText(filePath))
            {
                if (i == 0)
                    streamWriter.WriteLine("Interval|% Processor Time|Disk Reads/sec|Disk Writes/sec|Log Bytes Flushed/sec");

                streamWriter.WriteLine(
                    string.Format(_cultureInfo, "{0}|{1}|{2}|{3}|{4}", perfItem.CollectedDate, perfItem.Cpu, perfItem.DiskRead, perfItem.DiskWrite, perfItem.LogBytes));
            }
        }

        public class PerfItem
        {
            public DateTime CollectedDate { get; set; }

            public float Cpu { get; set; }

            public float DiskRead { get; set; }

            public float DiskWrite { get; set; }

            public float LogBytes { get; set; }
        }
    }
}