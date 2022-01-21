using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
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
using Microsoft.Isam.Esent.Interop;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServiceStack;
using SrumData;

#if NET462
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
#else
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using File = System.IO.File;
#endif


namespace SrumECmd;

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
    private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    private static readonly string Header =
        $"SrumECmd version {Assembly.GetExecutingAssembly().GetName().Version}" +
        "\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        "\r\nhttps://github.com/EricZimmerman/Srum";


    private static readonly string Footer =
        @"Examples: SrumECmd.exe -f ""C:\Temp\SRUDB.dat"" -r ""C:\Temp\SOFTWARE"" --csv ""C:\Temp\"" " + "\r\n\t " +
        @"   SrumECmd.exe -f ""C:\Temp\SRUDB.dat"" --csv ""c:\temp""" + "\r\n\t " +
        @"   SrumECmd.exe -d ""C:\Temp"" --csv ""c:\temp""" + "\r\n\t " +
        "\r\n\t" +
        "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

    private static string[] _args;
    private static RootCommand _rootCommand;

    private static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }

        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task Main(string[] args)
    {
        ExceptionlessClient.Default.Startup("wPXTiiouhEbK0s19lCgjiDThpfrW0ODU8RskdPEk");

        _args = args;

        var csvOption = new Option<string>(
            "--csv",
            "Directory to save CSV formatted results to. Be sure to include the full path in double quotes")
        {
            IsRequired = true
        };

        _rootCommand = new RootCommand
        {
            new Option<string>(
                "-f",
                "SRUDB.dat file to parse"),
            new Option<string>(
                "-r",
                "SOFTWARE hive to process. This is optional, but recommended\r\n"),

            new Option<string>(
                "-d",
                "Directory to recursively process, looking for SRUDB.dat and SOFTWARE hive. This mode is primarily used with KAPE so both SRUDB.dat and SOFTWARE hive can be located"),

            csvOption,

            new Option<string>(
                "--dt",
                () => "yyyy-MM-dd HH:mm:ss",
                "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options\r\n"),

            new Option<bool>(
                "--debug",
                () => false,
                "Show debug information during processing"),

            new Option<bool>(
                "--trace",
                () => false,
                "Show trace information during processing")
        };

        _rootCommand.Description = Header + "\r\n\r\n" + Footer;

        _rootCommand.Handler = CommandHandler.Create<string, string, string, string, string, bool, bool>(DoWork);

        await _rootCommand.InvokeAsync(args);
        
        Log.CloseAndFlush();
    }

    private static void DoWork(string f, string r, string d, string csv, string dt, bool debug, bool trace)
    {
        var levelSwitch = new LoggingLevelSwitch();

        var template = "{Message:lj}{NewLine}{Exception}";

        if (debug)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        if (trace)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template)
            .MinimumLevel.ControlledBy(levelSwitch);

        Log.Logger = conf.CreateLogger();
        
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine();
            Log.Fatal("Non-Windows platforms not supported due to the need to load ESI specific Windows libraries! Exiting...");
            Console.WriteLine();
            Environment.Exit(0);
            return;
        }

        if (f.IsNullOrEmpty() && d.IsNullOrEmpty())
        {
            var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);

            var hc = new HelpContext(helpBld, _rootCommand, Console.Out);

            helpBld.Write(hc);

            Log.Warning("Either -f or -d is required. Exiting\r\n");
            return;
        }


        if (f.IsNullOrEmpty() == false && !File.Exists(f))
        {
            Log.Warning("File '{File}' not found. Exiting", f);
            return;
        }

        if (d.IsNullOrEmpty() == false && !Directory.Exists(d))
        {
            Log.Warning("Directory '{D}' not found. Exiting", d);
            return;
        }

        if (csv.IsNullOrEmpty())
        {
            var helpBld = new HelpBuilder(LocalizationResources.Instance, Console.WindowWidth);

            var hc = new HelpContext(helpBld, _rootCommand, Console.Out);

            helpBld.Write(hc);

            Log.Warning("--csv is required. Exiting\r\n");
            return;
        }

        Log.Information("{Header}", Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}\r\n", string.Join(" ", _args));

        if (IsAdministrator() == false)
        {
            Log.Warning("Warning: Administrator privileges not found!\r\n");
        }

        var sw = new Stopwatch();
        sw.Start();

        var ts = DateTimeOffset.UtcNow;

        Srum sr = null;

        if (d.IsNullOrEmpty() == false)
        {
            IEnumerable<string> files2;

#if NET6_0
            var enumerationOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive,
                RecurseSubdirectories = true,
                AttributesToSkip = 0
            };
                        
            files2 =
                Directory.EnumerateFileSystemEntries(d, "SRUDB.DAT",enumerationOptions);
            
            f = files2.FirstOrDefault();

            if (f.IsNullOrEmpty())
            {
                Log.Warning("Did not locate any files named 'SRUDB.dat'! Exiting");
                return;
            }

            Log.Information("Found SRUM database file '{F}'!", f);
            
            files2 =
                Directory.EnumerateFileSystemEntries(d, "SOFTWARE",enumerationOptions);
            
            r = files2.FirstOrDefault();

            if (r.IsNullOrEmpty())
            {
                Log.Warning("Did not locate any files named 'SOFTWARE'! Registry data will not be extracted");
            }
            else
            {
                Log.Information("Found SOFTWARE hive '{R}'!", r);
            }
            
            #elif NET462 
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

            const DirectoryEnumerationOptions dirEnumOptions =
                DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive |
                DirectoryEnumerationOptions.SkipReparsePoints | DirectoryEnumerationOptions.ContinueOnException |
                DirectoryEnumerationOptions.BasicSearch;

             files2 =
                Directory.EnumerateFileSystemEntries(d, dirEnumOptions, ilter);

            f = files2.FirstOrDefault();

            if (f.IsNullOrEmpty())
            {
                Log.Warning("Did not locate any files named 'SRUDB.dat'! Exiting");
                return;
            }

            Log.Information("Found SRUM database file '{F}'!", f);

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
                Log.Warning("Did not locate any files named 'SOFTWARE'! Registry data will not be extracted");
            }
            else
            {
                Log.Information("Found SOFTWARE hive '{R}'!", r);
            }
#endif
            
            
            


            Console.WriteLine();
        }

        try
        {
            Log.Information("Processing '{F}'...", f);
            sr = new Srum(f, r);
            
            Console.WriteLine();
            Log.Information("Processing complete!");
            Console.WriteLine();
            
            Log.Information("{EnergyUse} {EnergyUsagesCount:N0}", "Energy Usage count:".PadRight(30),
                sr.EnergyUsages.Count);
            Log.Information("{Unknown312s} {Unknown312sCount:N0}", "Unknown 312 count:".PadRight(30),
                sr.Unknown312s.Count);
            Log.Information("{UnknownD8Fs} {UnknownD8FsCount:N0}", "Unknown D8F count:".PadRight(30),
                sr.UnknownD8Fs.Count);
            Log.Information("{AppResourceUseInfos} {AppResourceUseInfosCount:N0}",
                "App Resource Usage count:".PadRight(30), sr.AppResourceUseInfos.Count);
            Log.Information("{NetworkConnections} {NetworkConnectionsCount:N0}",
                "Network Connection count:".PadRight(30), sr.NetworkConnections.Count);
            Log.Information("{NetworkUsages} {NetworkUsagesCount}", "Network Usage count:".PadRight(30),
                sr.NetworkUsages.Count);
            Log.Information("{PushNotifications} {PushNotificationsCount:N0}", "Push Notification count:".PadRight(30),
                sr.PushNotifications.Count);
            Console.WriteLine();
        }
        catch (Exception e)
        {
            Log.Error(e,
                "Error processing file! Message: {Message}.\r\n\r\nThis almost always means the database is dirty and must be repaired. This can be verified by running 'esentutl.exe /mh SRUDB.dat' and examining the 'State' property",
                e.Message);
            Console.WriteLine();
            Log.Information(
                "If the database is dirty, **make a copy of your files**, ensure all files in the directory are not Read-only, open a PowerShell session as an admin, and repair by using the following commands (change directories to the location of SRUDB.dat first):\r\n\r\n'esentutl.exe /r sru /i'\r\n'esentutl.exe /p SRUDB.dat'\r\n\r\n");
            Environment.Exit(0);
        }

        if (csv.IsNullOrEmpty() == false)
        {
            if (Directory.Exists(csv) == false)
            {
                Log.Information(
                    "Path to '{Csv}' doesn't exist. Creating...", csv);

                try
                {
                    Directory.CreateDirectory(csv);
                }
                catch (Exception)
                {
                    Log.Fatal(
                        "Unable to create directory '{Csv}'. Does a file with the same name exist? Exiting", csv);
                    return;
                }
            }


            string outName;

            string outFile;

            Log.Information("CSV output will be saved to '{Csv}'\r\n", csv);

            StreamWriter swCsv;
            CsvWriter csvWriter;
            try
            {
                Log.Debug("Dumping Energy Usage tables '{TableName}'", EnergyUsage.TableName);

                outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_EnergyUsage_Output.csv";

                outFile = Path.Combine(csv, outName);

                swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

                var foo = csvWriter.Context.AutoMap<EnergyUsage>();
                foo.Map(t => t.Timestamp).Convert(t =>
                    $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                foo.Map(t => t.EventTimestamp).Convert(t =>
                    $"{t.Value.EventTimestamp?.ToString(dt)}");

                csvWriter.Context.RegisterClassMap(foo);
                csvWriter.WriteHeader<EnergyUsage>();
                csvWriter.NextRecord();

                csvWriter.WriteRecords(sr.EnergyUsages.Values);

                csvWriter.Flush();
                swCsv.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting 'EnergyUsage' data! Error: {Message}", e.Message);
            }


            try
            {
                Log.Debug("Dumping Unknown 312 table '{TableName}'", Unknown312.TableName);

                outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_Unknown312_Output.csv";

                outFile = Path.Combine(csv, outName);

                swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

                var foo = csvWriter.Context.AutoMap<Unknown312>();
                foo.Map(t => t.Timestamp).Convert(t =>
                    $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                foo.Map(t => t.EndTime).Convert(t =>
                    $"{t.Value.EndTime.ToString(dt)}");

                csvWriter.Context.RegisterClassMap(foo);
                csvWriter.WriteHeader<Unknown312>();
                csvWriter.NextRecord();

                csvWriter.WriteRecords(sr.Unknown312s.Values);

                csvWriter.Flush();
                swCsv.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting 'Unknown312' data! Error: {Message}", e.Message);
            }

            try
            {
                Log.Debug("Dumping Unknown D8F table '{TableName}'", UnknownD8F.TableName);

                outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_UnknownD8F_Output.csv";

                outFile = Path.Combine(csv, outName);

                swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

                var foo = csvWriter.Context.AutoMap<UnknownD8F>();
                foo.Map(t => t.Timestamp).Convert(t =>
                    $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                foo.Map(t => t.EndTime).Convert(t =>
                    $"{t.Value.EndTime.ToString(dt)}");
                foo.Map(t => t.StartTime).Convert(t =>
                    $"{t.Value.StartTime.ToString(dt)}");

                csvWriter.Context.RegisterClassMap(foo);
                csvWriter.WriteHeader<UnknownD8F>();
                csvWriter.NextRecord();

                csvWriter.WriteRecords(sr.UnknownD8Fs.Values);

                csvWriter.Flush();
                swCsv.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting 'UnknownD8F' data! Error: {Message}", e.Message);
            }

            try
            {
                Log.Debug("Dumping App Resource Use Info table '{TableName}'", AppResourceUseInfo.TableName);

                outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_AppResourceUseInfo_Output.csv";

                outFile = Path.Combine(csv, outName);

                swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

                var foo = csvWriter.Context.AutoMap<AppResourceUseInfo>();
                foo.Map(t => t.Timestamp).Convert(t =>
                    $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");

                csvWriter.Context.RegisterClassMap(foo);
                csvWriter.WriteHeader<AppResourceUseInfo>();
                csvWriter.NextRecord();

                csvWriter.WriteRecords(sr.AppResourceUseInfos.Values);

                csvWriter.Flush();
                swCsv.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting 'AppResourceUseInfo' data! Error: {Message}", e.Message);
            }

            try
            {
                Log.Debug("Dumping Network Connection table '{TableName}'", NetworkConnection.TableName);

                outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_NetworkConnections_Output.csv";

                outFile = Path.Combine(csv, outName);

                swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

                var foo = csvWriter.Context.AutoMap<NetworkConnection>();
                foo.Map(t => t.Timestamp).Convert(t =>
                    $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");
                foo.Map(t => t.ConnectStartTime).Convert(t =>
                    $"{t.Value.ConnectStartTime.ToString(dt)}");

                csvWriter.Context.RegisterClassMap(foo);
                csvWriter.WriteHeader<NetworkConnection>();
                csvWriter.NextRecord();

                csvWriter.WriteRecords(sr.NetworkConnections.Values);

                csvWriter.Flush();
                swCsv.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting 'NetworkConnection' data! Error: {Message}", e.Message);
            }

            try
            {
                Log.Debug("Dumping Network Usage table '{TableName}'", NetworkUsage.TableName);

                outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_NetworkUsages_Output.csv";

                outFile = Path.Combine(csv, outName);

                swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

                var foo = csvWriter.Context.AutoMap<NetworkUsage>();
                foo.Map(t => t.Timestamp).Convert(t =>
                    $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");

                csvWriter.Context.RegisterClassMap(foo);
                csvWriter.WriteHeader<NetworkUsage>();
                csvWriter.NextRecord();

                csvWriter.WriteRecords(sr.NetworkUsages.Values);

                csvWriter.Flush();
                swCsv.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting 'NetworkUsage' data! Error: {Message}", e.Message);
            }

            try
            {
                Log.Debug("Dumping Push Notification table '{TableName}'", PushNotification.TableName);

                outName = $"{ts:yyyyMMddHHmmss}_SrumECmd_PushNotifications_Output.csv";

                outFile = Path.Combine(csv, outName);

                swCsv = new StreamWriter(outFile, false, Encoding.UTF8);

                csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

                var foo = csvWriter.Context.AutoMap<PushNotification>();
                foo.Map(t => t.Timestamp).Convert(t =>
                    $"{t.Value.Timestamp:yyyy-MM-dd HH:mm:ss}");

                csvWriter.Context.RegisterClassMap(foo);
                csvWriter.WriteHeader<PushNotification>();
                csvWriter.NextRecord();

                csvWriter.WriteRecords(sr.PushNotifications.Values);

                csvWriter.Flush();
                swCsv.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error exporting 'PushNotification' data! Error: {Message}", e.Message);
            }

            sw.Stop();

            Log.Information("Processing completed in {TotalSeconds:N4} seconds\r\n", sw.Elapsed.TotalSeconds);
        }
    }
}