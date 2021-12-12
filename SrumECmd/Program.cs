using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CsvHelper;
using Exceptionless;

using NLog;
using NLog.Config;
using NLog.Targets;
using ServiceStack;
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

    internal class Program
    {
        private static Logger _logger;

        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        private static string header =
            $"SrumECmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
            "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
            "\r\nhttps://github.com/EricZimmerman/Srum";


        private static string footer = @"Examples: SrumECmd.exe -f ""C:\Temp\SRUDB.dat"" -r ""C:\Temp\SOFTWARE"" --csv ""C:\Temp\"" " + "\r\n\t " +
                                       @" SrumECmd.exe -f ""C:\Temp\SRUDB.dat"" --csv ""c:\temp""" + "\r\n\t " +
                                       @" SrumECmd.exe -d ""C:\Temp"" --csv ""c:\temp""" + "\r\n\t " +
                                       "\r\n\t" +
                                       "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

        private static string[] _args;
        private static RootCommand _rootCommand;

        public static bool IsAdministrator()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return true;
            }
            
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

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

        private static async Task Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("wPXTiiouhEbK0s19lCgjiDThpfrW0ODU8RskdPEk");

            _args = args;
    
            SetupNLog();

            _logger = LogManager.GetCurrentClassLogger();

            var csvOption = new Option<string>(
                "--csv",
                "Directory to save CSV formatted results to. Be sure to include the full path in double quotes");
            csvOption.IsRequired = true;
            
            _rootCommand = new RootCommand
            {
                new Option<string>(
                    "-f",

                    description: "Amcache.hve file to parse"),
                new Option<string>(
                    "-r",
                    
                    description: "SOFTWARE hive to process. This is optional, but recommended\r\n"),
            
                new Option<string>(
                    "-d",
                
                    "Directory to recursively process, looking for SRUDB.dat and SOFTWARE hive. This mode is primarily used with KAPE so both SRUDB.dat and SOFTWARE hive can be located"),
              
                csvOption,
              
                new Option<string>(
                    "--dt",
                    getDefaultValue:()=>"yyyy-MM-dd HH:mm:ss",
                    "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options\r\n"),
              
                new Option<bool>(
                    "--debug",
                    getDefaultValue:()=>false,
                    "Show debug information during processing"),
            
                new Option<bool>(
                    "--trace",
                    getDefaultValue:()=>false,
                    "Show trace information during processing"),
                
            };

            _rootCommand.Description = header + "\r\n\r\n" +footer;

            _rootCommand.Handler = CommandHandler.Create<string,string,string,string,string,bool,bool>(DoWork);
            
            await _rootCommand.InvokeAsync(args);
        }

       

        private static void DoWork(string f, string r, string d, string csv, string dt, bool debug, bool trace)
        {
            if (f.IsNullOrEmpty() && d.IsNullOrEmpty())
            {
                var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
                    
                var hc = new HelpContext(helpBld,_rootCommand,Console.Out);

                helpBld.Write(hc);
                    
                _logger.Warn("Either -f or -d is required. Exiting\r\n");
                return;
                
            }
           

            if (f.IsNullOrEmpty() == false && !File.Exists(f))
            {
                _logger.Warn($"File '{f}' not found. Exiting");
                return;
            }

            if (d.IsNullOrEmpty() == false && !Directory.Exists(d))
            {
                _logger.Warn($"Directory '{d}' not found. Exiting");
                return;
            }

            if (csv.IsNullOrEmpty())
            {
                var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);
                    
                var hc = new HelpContext(helpBld,_rootCommand,Console.Out);

                helpBld.Write(hc);

                _logger.Warn("--csv is required. Exiting\r\n");
                return;
            }

            _logger.Info(header);
            _logger.Info("");
            _logger.Info($"Command line: {string.Join(" ", _args)}\r\n");

            if (IsAdministrator() == false)
            {
                _logger.Fatal("Warning: Administrator privileges not found!\r\n");
            }

            if (debug)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Debug);
            }

            if (trace)
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

            if (d.IsNullOrEmpty() == false)
            {
                //kape mode, so find the files
                var ilter = new DirectoryEnumerationFilters();
                ilter.InclusionFilter = fsei =>
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

                ilter.RecursionFilter = entryInfo => !entryInfo.IsMountPoint && !entryInfo.IsSymbolicLink;

                ilter.ErrorFilter = (errorCode, errorMessage, pathProcessed) => true;

                var dirEnumOptions =
                    DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive |
                    DirectoryEnumerationOptions.SkipReparsePoints | DirectoryEnumerationOptions.ContinueOnException |
                    DirectoryEnumerationOptions.BasicSearch;

                var files2 =
                    Directory.EnumerateFileSystemEntries(d, dirEnumOptions, ilter);

                f = files2.FirstOrDefault();

                if (f.IsNullOrEmpty())
                {
                    _logger.Warn("Did not locate any files named 'SRUDB.dat'! Exiting");
                    return;
                }

                _logger.Info($"Found SRUM database file '{f}'!");

                ilter = new DirectoryEnumerationFilters();
                ilter.InclusionFilter = fsei =>
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

                ilter.RecursionFilter = entryInfo => !entryInfo.IsMountPoint && !entryInfo.IsSymbolicLink;

                ilter.ErrorFilter = (errorCode, errorMessage, pathProcessed) => true;

                files2 =
                    Directory.EnumerateFileSystemEntries(d, dirEnumOptions, ilter);

                r = files2.FirstOrDefault();

                if (r.IsNullOrEmpty())
                {
                    _logger.Warn("Did not locate any files named 'SOFTWARE'! Registry data will not be extracted");
                }
                else
                {
                    _logger.Info($"Found SOFTWARE hive '{r}'!");
                }

                Console.WriteLine();
            }

            try
            {
                _logger.Info($"Processing '{f}'...");
                sr = new Srum(f, r);

                _logger.Warn("\r\nProcessing complete!\r\n");
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
                _logger.Info("If the database is dirty, **make a copy of your files**, ensure all files in the directory are not Read-only, open a PowerShell session as an admin, and repair by using the following commands (change directories to the location of SRUDB.dat first):\r\n\r\n'esentutl.exe /r sru /i'\r\n'esentutl.exe /p SRUDB.dat'\r\n\r\n");
                Environment.Exit(0);
            }

            if (csv.IsNullOrEmpty() == false)
            {
                if (Directory.Exists(csv) == false)
                {
                    _logger.Warn(
                        $"Path to '{csv}' doesn't exist. Creating...");

                    try
                    {
                        Directory.CreateDirectory(csv);
                    }
                    catch (Exception)
                    {
                        _logger.Fatal(
                            $"Unable to create directory '{csv}'. Does a file with the same name exist? Exiting");
                        return;
                    }
                }


                var outName = string.Empty;

                var outFile = string.Empty;

                _logger.Warn($"CSV output will be saved to '{csv}'\r\n");

                try
                {
                    _logger.Debug($"Dumping Energy Usage tables '{EnergyUsage.TableName}'");

                    outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_EnergyUsage_Output.csv";

                    outFile = Path.Combine(csv, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Context.AutoMap<EnergyUsage>();
                    foo.Map(t => t.Timestamp).Convert(t =>
                        $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.EventTimestamp).Convert(t =>
                        $"{t.Value.EventTimestamp?.ToString(dt)}");

                    _csvWriter.Context.RegisterClassMap(foo);
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

                    outFile = Path.Combine(csv, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Context.AutoMap<Unknown312>();
                    foo.Map(t => t.Timestamp).Convert(t =>
                        $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.EndTime).Convert(t =>
                        $"{t.Value.EndTime.ToString(dt)}");

                    _csvWriter.Context.RegisterClassMap(foo);
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

                    outFile = Path.Combine(csv, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Context.AutoMap<UnknownD8F>();
                    foo.Map(t => t.Timestamp).Convert(t =>
                        $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.EndTime).Convert(t =>
                        $"{t.Value.EndTime.ToString(dt)}");
                    foo.Map(t => t.StartTime).Convert(t =>
                        $"{t.Value.StartTime.ToString(dt)}");

                    _csvWriter.Context.RegisterClassMap(foo);
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

                    outFile = Path.Combine(csv, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Context.AutoMap<AppResourceUseInfo>();
                    foo.Map(t => t.Timestamp).Convert(t =>
                        $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");

                    _csvWriter.Context.RegisterClassMap(foo);
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

                    outFile = Path.Combine(csv, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Context.AutoMap<NetworkConnection>();
                    foo.Map(t => t.Timestamp).Convert(t =>
                        $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    foo.Map(t => t.ConnectStartTime).Convert(t =>
                        $"{t.Value.ConnectStartTime.ToString(dt)}");

                    _csvWriter.Context.RegisterClassMap(foo);
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

                    outFile = Path.Combine(csv, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Context.AutoMap<NetworkUsage>();
                    foo.Map(t => t.Timestamp).Convert(t =>
                        $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");

                    _csvWriter.Context.RegisterClassMap(foo);
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

                    outFile = Path.Combine(csv, outName);

                    _swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                    _csvWriter = new CsvWriter(_swCsv, CultureInfo.InvariantCulture);

                    var foo = _csvWriter.Context.AutoMap<PushNotification>();
                    foo.Map(t => t.Timestamp).Convert(t =>
                        $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");

                    _csvWriter.Context.RegisterClassMap(foo);
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
                    $"Processing completed in {sw.Elapsed.TotalSeconds:N4} seconds\r\n");
            }
        }

    }
    
}