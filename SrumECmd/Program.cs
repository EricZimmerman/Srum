using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using Alphaleonis.Win32.Filesystem;
using CsvHelper;
using Exceptionless;
using Fclp;
using Fclp.Internals.Extensions;
using NLog;
using NLog.Config;
using NLog.Targets;
using SrumData;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace SrumECmd
{
    public class ApplicationArguments
    {
        public string FileDb { get; set; }
        public string FileReg { get; set; }
        public string Directory { get; set; }
        public string CsvDirectory { get; set; }

        public string DateTimeFormat { get; set; }

        public bool Debug { get; set; }
        public bool Trace { get; set; }
    }

    class Program
    {
        private static Logger _logger;

        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static void SetupNLog()
        {
            if (File.Exists(Path.Combine(BaseDirectory, "Nlog.config")))
            {
                return;
            }

            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }

        static void Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("wPXTiiouhEbK0s19lCgjiDThpfrW0ODU8RskdPEk");


            SetupNLog();

            _logger = LogManager.GetCurrentClassLogger();


            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>
            {
                IsCaseSensitive = false
            };

            _fluentCommandLineParser.Setup(arg => arg.FileDb)
                .As('f')
                .WithDescription("SRUDB.dat file to process. Either this or -d is required");

            _fluentCommandLineParser.Setup(arg => arg.FileReg)
                .As('r')
                .WithDescription("SOFTWARE hive to process. This is optional, but recommended\r\n");

            _fluentCommandLineParser.Setup(arg => arg.Directory)
                .As('d')
                .WithDescription("Directory to recursively process, looking for SRUDB.dat and SOFTWARE hive. This mode is primarily used with KAPE so both SRUDB.dat and SOFTWARE hive can be located");

            _fluentCommandLineParser.Setup(arg => arg.CsvDirectory)
                .As("csv")
                .WithDescription(
                    "Directory to save CSV formatted results to. Be sure to include the full path in double quotes\r\n");

            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying time stamps. Default is: yyyy-MM-dd HH:mm:ss.fffffff\r\n")
                .SetDefault("yyyy-MM-dd HH:mm:ss.fffffff");

            _fluentCommandLineParser.Setup(arg => arg.Debug)
                .As("debug")
                .WithDescription("Show debug information during processing").SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.Trace)
                .As("trace")
                .WithDescription("Show trace information during processing\r\n").SetDefault(false);



            var header =
                $"SrumECmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
                "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                "\r\nhttps://github.com/EricZimmerman/Srum";


            var footer = @"Examples: SrumECmd.exe -f ""C:\Temp\SRUDB.dat"" -r ""C:\Temp\SOFTWARE"" --csv ""C:\Temp\"" " + "\r\n\t " +
                         @" SrumECmd.exe -f ""C:\Temp\SRUDB.dat"" --csv ""c:\temp""" + "\r\n\t " +
                         @" SrumECmd.exe -d ""C:\Temp"" --csv ""c:\temp""" + "\r\n\t " +

                         "\r\n\t" +
                         "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

            _fluentCommandLineParser.SetupHelp("?", "help")
                .WithHeader(header)
                .Callback(text => _logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _logger.Error("");
                _logger.Error(result.ErrorText);

                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                return;
            }

            if (UsefulExtension.IsNullOrEmpty(_fluentCommandLineParser.Object.FileDb) &&
                UsefulExtension.IsNullOrEmpty(_fluentCommandLineParser.Object.Directory))
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("Either -f or -d is required. Exiting\r\n");
                return;
            }

            if (UsefulExtension.IsNullOrEmpty(_fluentCommandLineParser.Object.FileDb) == false &&
                !File.Exists(_fluentCommandLineParser.Object.FileDb))
            {
                _logger.Warn($"File '{_fluentCommandLineParser.Object.FileDb}' not found. Exiting");
                return;
            }

            if (UsefulExtension.IsNullOrEmpty(_fluentCommandLineParser.Object.Directory) == false &&
                !Directory.Exists(_fluentCommandLineParser.Object.Directory))
            {
                _logger.Warn($"Directory '{_fluentCommandLineParser.Object.Directory}' not found. Exiting");
                return;
            }

            if (UsefulExtension.IsNullOrEmpty(_fluentCommandLineParser.Object.CsvDirectory) )
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                _logger.Warn("--csv is required. Exiting\r\n");
                return;
            }

            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

            if (IsAdministrator() == false)
            {
                _logger.Fatal($"Warning: Administrator privileges not found!\r\n");
            }

            if (_fluentCommandLineParser.Object.Debug)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Debug);
            }

            if (_fluentCommandLineParser.Object.Trace)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Trace);
            }

            LogManager.ReconfigExistingLoggers();

            var sw = new Stopwatch();
            sw.Start();

            var ts = DateTimeOffset.UtcNow;

            CsvWriter _csvWriter = null;
            StreamWriter _swCsv = null;

            Srum sr = null;

            if (_fluentCommandLineParser.Object.Directory.IsNullOrEmpty() == false)
            {
                //kape mode, so find the files
                var f = new DirectoryEnumerationFilters();
                f.InclusionFilter = fsei =>
                {
                    if (fsei.FileSize == 0)
                    {
                        return false;
                    }
                    
                    if (fsei.FileName.ToUpperInvariant() == "SRUDB.DAT")
                    {
                        return true;
                    }

                    return false;
                };

                f.RecursionFilter = entryInfo => !entryInfo.IsMountPoint && !entryInfo.IsSymbolicLink;

                f.ErrorFilter = (errorCode, errorMessage, pathProcessed) => true;

                var dirEnumOptions =
                    DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive |
                    DirectoryEnumerationOptions.SkipReparsePoints | DirectoryEnumerationOptions.ContinueOnException |
                    DirectoryEnumerationOptions.BasicSearch;

                var files2 =
                    Directory.EnumerateFileSystemEntries(_fluentCommandLineParser.Object.Directory, dirEnumOptions, f);

                _fluentCommandLineParser.Object.FileDb = files2.FirstOrDefault();

                if (_fluentCommandLineParser.Object.FileDb.IsNullOrEmpty())
                {
                    _logger.Warn($"Did not locate any files named 'SRUDB.dat'! Exiting");
                    return;
                }

                _logger.Info($"Found SRUM database file '{_fluentCommandLineParser.Object.FileDb}'!");

                f = new DirectoryEnumerationFilters();
                f.InclusionFilter = fsei =>
                {
                    if (fsei.FileSize == 0)
                    {
                        return false;
                    }
                    
                    if (fsei.FileName.ToUpperInvariant() == "SOFTWARE")
                    {
                        return true;
                    }

                    return false;
                };

                f.RecursionFilter = entryInfo => !entryInfo.IsMountPoint && !entryInfo.IsSymbolicLink;

                f.ErrorFilter = (errorCode, errorMessage, pathProcessed) => true;
                
                files2 =
                    Directory.EnumerateFileSystemEntries(_fluentCommandLineParser.Object.Directory, dirEnumOptions, f);

                _fluentCommandLineParser.Object.FileReg = files2.FirstOrDefault();

                if (_fluentCommandLineParser.Object.FileReg.IsNullOrEmpty())
                {
                    _logger.Warn($"Did not locate any files named 'SOFTWARE'! Registry data will not be extracted");
                }
                else
                {
                    _logger.Info($"Found SOFTWARE hive '{_fluentCommandLineParser.Object.FileReg}'!");
                }

                Console.WriteLine();

            }

            try
            {
                _logger.Info($"Processing '{_fluentCommandLineParser.Object.FileDb}'...");
                sr = new Srum(_fluentCommandLineParser.Object.FileDb, _fluentCommandLineParser.Object.FileReg);

                _logger.Warn($"\r\nProcessing complete!\r\n");
                _logger.Info($"{"Energy Usage count:".PadRight(30)} {sr.EnergyUsages.Count:N0}");
                _logger.Info($"{"Unknown 312 count:".PadRight(30)} {sr.Unknown312s.Count:N0}");
                _logger.Info($"{"Unknown D8F count:".PadRight(30)} {sr.UnknownD8Fs.Count:N0}");
                _logger.Info($"{"App Resource Usage count:".PadRight(30)} {sr.AppResourceUseInfos.Count:N0}");
                _logger.Info($"{"Network Connection count:".PadRight(30)} {sr.NetworkConnections.Count:N0}");
                _logger.Info($"{"Network Usage count:".PadRight(30)} {sr.NetworkUsages.Count:N0}");
                _logger.Info($"{"Push Notification count:".PadRight(30)} {sr.PushNotifications.Count:N0}");
                Console.WriteLine();
            }
            catch (Exception e)
            {
                _logger.Error($"Error processing file! Message: {e.Message}.\r\n\r\nThis almost always means the database is dirty and must be repaired. This can be verified by running 'esentutl.exe /mh SRUDB.dat' and examining the 'State' property");
                Console.WriteLine();
                _logger.Info("If the database is dirty, **make a copy of your files**, open a PowerShell session as an admin, and repair by using the following commands (change directories to the location of SRUDB.dat first):\r\n\r\n'esentutl.exe /r sru /i'\r\n'esentutl.exe /p SRUDB.dat'\r\n\r\n");
                Environment.Exit(0);
            }

            if (_fluentCommandLineParser.Object.CsvDirectory.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(_fluentCommandLineParser.Object.CsvDirectory) == false)
                {
                    _logger.Warn(
                        $"Path to '{_fluentCommandLineParser.Object.CsvDirectory}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(_fluentCommandLineParser.Object.CsvDirectory);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{_fluentCommandLineParser.Object.CsvDirectory}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }


                var outName = string.Empty;

                var outFile = string.Empty;

                _logger.Warn($"CSV output will be saved to '{_fluentCommandLineParser.Object.CsvDirectory}'\r\n");

                try
                {
                    _logger.Debug($"Dumping Energy Usage tables '{EnergyUsage.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_EnergyUsage_Output.csv";

                    outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Configuration.AutoMap<EnergyUsage>();
                    foo.Map(t => t.Timestamp).ConvertUsing(t =>
                        $"{t.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.EventTimestamp).ConvertUsing(t =>
                        $"{t.EventTimestamp?.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}");

                    _csvWriter.Configuration.RegisterClassMap(foo);
                    _csvWriter.WriteHeader<EnergyUsage>();
                    _csvWriter.NextRecord();

                    _csvWriter.WriteRecords(sr.EnergyUsages.Values);

                    _csvWriter.Flush();
                    _swCsv.Flush();
                }
                catch (Exception e)
                {
                    _logger.Error($"Error exporting 'EnergyUsage' data! Error: {e.Message}");
                }


                try
                {
                    _logger.Debug($"Dumping Unknown 312 table '{Unknown312.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_Unknown312_Output.csv";

                    outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Configuration.AutoMap<Unknown312>();
                    foo.Map(t => t.Timestamp).ConvertUsing(t =>
                        $"{t.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.EndTime).ConvertUsing(t =>
                        $"{t.EndTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}");

                    _csvWriter.Configuration.RegisterClassMap(foo);
                    _csvWriter.WriteHeader<Unknown312>();
                    _csvWriter.NextRecord();

                    _csvWriter.WriteRecords(sr.Unknown312s.Values);

                    _csvWriter.Flush();
                    _swCsv.Flush();
                }
                catch (Exception e)
                {
                    _logger.Error($"Error exporting 'Unknown312' data! Error: {e.Message}");
                }

                try
                {
                    _logger.Debug($"Dumping Unknown D8F table '{UnknownD8F.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_UnknownD8F_Output.csv";

                    outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Configuration.AutoMap<UnknownD8F>();
                    foo.Map(t => t.Timestamp).ConvertUsing(t =>
                        $"{t.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.EndTime).ConvertUsing(t =>
                        $"{t.EndTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}");
                    foo.Map(t => t.StartTime).ConvertUsing(t =>
                        $"{t.StartTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}");
                    
                    _csvWriter.Configuration.RegisterClassMap(foo);
                    _csvWriter.WriteHeader<UnknownD8F>();
                    _csvWriter.NextRecord();

                    _csvWriter.WriteRecords(sr.UnknownD8Fs.Values);

                    _csvWriter.Flush();
                    _swCsv.Flush();
                }
                catch (Exception e)
                {
                    _logger.Error($"Error exporting 'UnknownD8F' data! Error: {e.Message}");
                }

                try
                {
                    _logger.Debug($"Dumping AppResourceUseInfo table '{AppResourceUseInfo.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_AppResourceUseInfo_Output.csv";

                    outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Configuration.AutoMap<AppResourceUseInfo>();
                    foo.Map(t => t.Timestamp).ConvertUsing(t =>
                        $"{t.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    
                    _csvWriter.Configuration.RegisterClassMap(foo);
                    _csvWriter.WriteHeader<AppResourceUseInfo>();
                    _csvWriter.NextRecord();

                    _csvWriter.WriteRecords(sr.AppResourceUseInfos.Values);

                    _csvWriter.Flush();
                    _swCsv.Flush();
                }
                catch (Exception e)
                {
                    _logger.Error($"Error exporting 'AppResourceUseInfo' data! Error: {e.Message}");
                }

                try
                {
                    _logger.Debug($"Dumping NetworkConnection table '{NetworkConnection.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_NetworkConnections_Output.csv";

                    outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Configuration.AutoMap<NetworkConnection>();
                    foo.Map(t => t.Timestamp).ConvertUsing(t =>
                        $"{t.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.ConnectStartTime).ConvertUsing(t =>
                        $"{t.ConnectStartTime.ToString(_fluentCommandLineParser.Object.DateTimeFormat)}");

                    _csvWriter.Configuration.RegisterClassMap(foo);
                    _csvWriter.WriteHeader<NetworkConnection>();
                    _csvWriter.NextRecord();

                    _csvWriter.WriteRecords(sr.NetworkConnections.Values);

                    _csvWriter.Flush();
                    _swCsv.Flush();
                }
                catch (Exception e)
                {
                    _logger.Error($"Error exporting 'NetworkConnection' data! Error: {e.Message}");
                }

                try
                {
                    _logger.Debug($"Dumping NetworkUsage table '{NetworkUsage.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_NetworkUsages_Output.csv";

                    outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Configuration.AutoMap<NetworkUsage>();
                    foo.Map(t => t.Timestamp).ConvertUsing(t =>
                        $"{t.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    
                    _csvWriter.Configuration.RegisterClassMap(foo);
                    _csvWriter.WriteHeader<NetworkUsage>();
                    _csvWriter.NextRecord();

                    _csvWriter.WriteRecords(sr.NetworkUsages.Values);

                    _csvWriter.Flush();
                    _swCsv.Flush();
                }
                catch (Exception e)
                {
                    _logger.Error($"Error exporting 'NetworkUsage' data! Error: {e.Message}");
                }

                try
                {
                    _logger.Debug($"Dumping PushNotification table '{PushNotification.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_PushNotifications_Output.csv";

                    outFile = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Configuration.AutoMap<PushNotification>();
                    foo.Map(t => t.Timestamp).ConvertUsing(t =>
                        $"{t.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    
                    _csvWriter.Configuration.RegisterClassMap(foo);
                    _csvWriter.WriteHeader<PushNotification>();
                    _csvWriter.NextRecord();

                    _csvWriter.WriteRecords(sr.PushNotifications.Values);

                    _csvWriter.Flush();
                    _swCsv.Flush();
                }
                catch (Exception e)
                {
                    _logger.Error($"Error exporting 'PushNotification' data! Error: {e.Message}");
                }

                
                sw.Stop();

                _logger.Debug("");

                _logger.Error(
                    $"Procesing completed in {sw.Elapsed.TotalSeconds:N4} seconds\r\n");


            }
        }
    }
}
