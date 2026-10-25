using System;
using System.Collections.Generic;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using CsvHelper;
using Exceptionless;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServiceStack;
using SrumData;

#if NET472
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;
#else
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using File = System.IO.File;
#endif


namespace SrumECmd;

internal class Program
{
    private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    private static readonly string Header =
        $"SrumECmd version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
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

    private static async Task<int> Main(string[] args)
    {
        ExceptionlessClient.Default.Startup("wPXTiiouhEbK0s19lCgjiDThpfrW0ODU8RskdPEk");

        _args = args;

        var csvOpt = new Option<string>(
            "--csv")
        {
            Description = "Directory to save CSV formatted results to. Be sure to include the full path in double quotes"
          //  Required = true
        };
        
        var fOpt = new Option<string>("-f")
        {
            Description = "SRUDB.dat file to parse"
           // Required = true
        };
        
        var rOpt = new Option<string>("-r")
        {
            Description = "SOFTWARE hive to process. This is optional, but recommended\r\n"
        };
        
        var dOpt = new Option<string>("-d")
        {
            Description = "Directory to recursively process, looking for SRUDB.dat and SOFTWARE hive. This mode is primarily used with KAPE so both SRUDB.dat and SOFTWARE hive can be located"
        };
        
       var dtOpt = new Option<string>(
            "--dt")
        {
            Description = "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options\r\n",
            DefaultValueFactory = _ => "yyyy-MM-dd HH:mm:ss"
        };

        var tempOpt = new Option<string>("--temp")
        {
            Description = TempFolder.Help
        };

        var debugOpt = new Option<bool>("--debug")
        {
            Description = "Show debug information during processing",
            DefaultValueFactory = _ => false
        };
        var traceOpt = new Option<bool>("--trace")
        {
            Description = "Show trace information during processing",
            DefaultValueFactory = _ => false
        };
        var coOpt = new Option<bool>("--co")
        {
            Description = "When true, save all console messages to <timestamp>_SrumECmd_<version>_ConsoleLog.txt in the --csv directory. Use --co false to turn it off",
            DefaultValueFactory = _ => true
        };

        _rootCommand = new RootCommand
        {
           fOpt,
           rOpt,
           dOpt,
           csvOpt,
           dtOpt,
           tempOpt,
           debugOpt,
           traceOpt,
           coOpt
        
        };

        _rootCommand.Description = Header + "\r\n\r\n" + Footer;
        
        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt),result.GetValue(rOpt),result.GetValue(dOpt),result.GetValue(csvOpt),result.GetValue(dtOpt),result.GetValue(tempOpt),result.GetValue(debugOpt),result.GetValue(traceOpt),result.GetValue(coOpt)));

        var exitCode = await _rootCommand.Parse(args).InvokeAsync();

        Log.CloseAndFlush();

        return exitCode;
    }

    /// <returns>0 on success; 1 for invalid arguments, a database that could not be read or an export that failed</returns>
    private static int DoWork(string f, string r, string d, string csv, string dt, string temp, bool debug, bool trace, bool co)
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

        //the time in the output names, and in the console log's
        var ts = DateTimeOffset.UtcNow;

        var consoleLog = co ? ConsoleLog.GetPath(ts, csv) : null;
        if (consoleLog != null)
        {
            conf.WriteTo.File(consoleLog, outputTemplate: ConsoleLog.Template);
        }

        Log.Logger = conf.CreateLogger();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine();
            Log.Fatal(
                "Non-Windows platforms not supported due to the need to load ESE specific Windows libraries! Exiting...");
            Console.WriteLine();
            return 1;
        }

        if (f.IsNullOrEmpty() && d.IsNullOrEmpty())
        {
            var aaa = new CustomHelpAction(new HelpAction());
            aaa.Invoke(_rootCommand.Parse("Either -f or -d is required. Exiting"));

            return 1;
        }


        if (f.IsNullOrEmpty() == false && !File.Exists(f))
        {
            Log.Warning("File '{File}' not found. Exiting", f);
            return 1;
        }

        if (d.IsNullOrEmpty() == false && !Directory.Exists(d))
        {
            Log.Warning("Directory '{D}' not found. Exiting", d);
            return 1;
        }

        if (r.IsNullOrEmpty() == false && !File.Exists(r))
        {
            Log.Warning("SOFTWARE hive '{R}' not found. Exiting", r);
            return 1;
        }

        if (csv.IsNullOrEmpty())
        {
            var aaa = new CustomHelpAction(new HelpAction());
            aaa.Invoke(_rootCommand.Parse("--csv is required. Exiting"));

            return 1;
        }

        Log.Information("{Header}", Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}\r\n", string.Join(" ", _args));

        if (consoleLog != null)
        {
            Log.Information("Console log: {Path}\r\n", consoleLog);
        }

        if (IsAdministrator() == false)
        {
            Log.Warning("Warning: Administrator privileges not found!\r\n");
        }

        var sw = new Stopwatch();
        sw.Start();

        Srum sr = null;

        //folders under -d that could not be searched (exit code 1)
        var searchErrors = 0;

        if (d.IsNullOrEmpty() == false)
        {
            IEnumerable<string> files2;

#if NET6_0_OR_GREATER
            files2 = FindEntries(d, "SRUDB.DAT", ref searchErrors);

            f = files2.FirstOrDefault();

            if (f.IsNullOrEmpty())
            {
                Log.Warning("Did not locate any files named 'SRUDB.dat'! Exiting");
                //nothing to do is not a failure, unless part of the folder could not be searched
                return SearchResult(d, searchErrors);
            }

            string esentutlOutput = "";
            try
            {
                var esentutlProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "esentutl.exe",
                        Arguments = $"/mh \"{f}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                esentutlProcess.Start();
                esentutlOutput = esentutlProcess.StandardOutput.ReadToEnd();
                esentutlProcess.WaitForExit();

                if (esentutlProcess.ExitCode != 0)
                {
                    Log.Warning("esentutl.exe did not run successfully. Exit code: {ExitCode}",
                        esentutlProcess.ExitCode);
                }

                string state = Regex.Match(esentutlOutput, @"State:\s*(.*)").Groups[1].Value.Trim();
                string formatVersion = Regex.Match(esentutlOutput, @"Format ulVersion:\s*.*\(.* (\d{4})\)").Groups[1].Value;
                string engineVersion = Regex.Match(esentutlOutput, @"Engine ulVersion:\s*.*\(.* (\d{4})\)").Groups[1].Value;
                
                // if the Format Version is newer (aka larger) than the Engine Version, find a newer system to repair the SRUDB.dat file with. If Format Version is equal or older (aka smaller), then you should be able to repair the SRUDB.dat file on your system 

                Log.Information("Found SRUM database file '{F}' (State: {State}, Format Version: {FormatVersion}, Engine Version: {EngineVersion})", f, state, formatVersion, engineVersion);
                
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while running esentutl.exe");
            }
            
            files2 = FindEntries(d, "SOFTWARE", ref searchErrors);

            r = files2.FirstOrDefault();

            if (r.IsNullOrEmpty())
            {
                Log.Warning("Did not locate any files named 'SOFTWARE'! Registry data will not be extracted");
            }
            else
            {
                Log.Information("Found SOFTWARE hive '{R}'!", r);
            }

#elif NET472
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

            //(AlphaFS does not call ErrorFilter when ContinueOnException is set: the folder was skipped silently)
            ilter.ErrorFilter = (errorCode, errorMessage, pathProcessed) =>
            {
                searchErrors += ReportUnreadable(pathProcessed, errorMessage);
                return true;
            };

            const DirectoryEnumerationOptions dirEnumOptions =
                DirectoryEnumerationOptions.Files | DirectoryEnumerationOptions.Recursive |
                DirectoryEnumerationOptions.SkipReparsePoints |
                DirectoryEnumerationOptions.BasicSearch;

             files2 =
                Directory.EnumerateFileSystemEntries(d, dirEnumOptions, ilter);

            f = files2.FirstOrDefault();

            if (f.IsNullOrEmpty())
            {
                Log.Warning("Did not locate any files named 'SRUDB.dat'! Exiting");
                //nothing to do is not a failure, unless part of the folder could not be searched
                return SearchResult(d, searchErrors);
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

            //(AlphaFS does not call ErrorFilter when ContinueOnException is set: the folder was skipped silently)
            ilter.ErrorFilter = (errorCode, errorMessage, pathProcessed) =>
            {
                searchErrors += ReportUnreadable(pathProcessed, errorMessage);
                return true;
            };

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

            if (searchErrors > 0)
            {
                Log.Error("Could not access all files in '{D}': {Count:N0} folder(s) could not be read", d, searchErrors);
            }

            Console.WriteLine();
        }

        //the database is read from a copy, made in --temp or Temp next to SrumECmd (never the user's %TEMP%)
        var tempFolder = TempFolder.Prepare(temp, out var tempCreated, out var tempError);
        if (tempFolder == null)
        {
            Log.Error("The folder for the temporary copy of the database can't be written to ({Error}). Give one with --temp. Exiting", tempError);
            Console.WriteLine();
            return 1;
        }

        try
        {
            Log.Information("Processing '{F}'...", f);
            //read from a copy in the temporary folder, with its logs replayed (or repaired with esentutl /p): nothing next to it changes
            try
            {
                sr = new Srum(f, r, tempFolder);
            }
            finally
            {
                TempFolder.RemoveIfEmpty(tempFolder, tempCreated);
            }

            Console.WriteLine();
            Log.Information("Processing complete!");
            Console.WriteLine();

            Log.Information("{EnergyUse} {EnergyUsagesCount:N0}", "Energy Usage count:".PadRight(30),
                sr.EnergyUsages.Count);
            Log.Information("{AppTimelineProviders} {AppTimelineProvidersCount:N0}",
                "AppTimelineProvider count:".PadRight(30),
                sr.TimelineProviders.Count);
            Log.Information("{vfuprovs} {vfuprovsCount:N0}", "vfuprov count:".PadRight(30),
                sr.Vfuprovs.Count);
            Log.Information("{AppResourceUseInfos} {AppResourceUseInfosCount:N0}",
                "App Resource Usage count:".PadRight(30), sr.AppResourceUseInfos.Count);
            Log.Information("{NetworkConnections} {NetworkConnectionsCount:N0}",
                "Network Connection count:".PadRight(30), sr.NetworkConnections.Count);
            Log.Information("{NetworkUsages} {NetworkUsagesCount:N0}", "Network Usage count:".PadRight(30),
                sr.NetworkUsages.Count);
            Log.Information("{PushNotifications} {PushNotificationsCount:N0}", "Push Notification count:".PadRight(30),
                sr.PushNotifications.Count);

            //the providers seen on Server 2022 (Issue #15), when present
            foreach (var (label, count) in new[]
                     {
                         ("ProcessorTime count:", sr.ProcessorTimes.Count), ("SizeInBytes count:", sr.SizeInBytesInfos.Count),
                         ("TotalUsed count:", sr.TotalUsedInfos.Count), ("NetworkBytes count:", sr.NetworkBytesInfos.Count)
                     })
            {
                if (count > 0)
                {
                    Log.Information("{Label} {Count:N0}", label.PadRight(30), count);
                }
            }

            Console.WriteLine();
        }
        catch (Exception e)
        {
            Log.Error(e,
                "Error processing file! Message: {Message}.\r\n\r\nThe transaction logs were replayed into a copy of the database and, if it was still dirty, the copy was repaired with 'esentutl.exe /p', so the database is probably damaged beyond repair. 'esentutl.exe /mh SRUDB.dat' shows its state",
                e.Message);
            Console.WriteLine();
            //a database that could not be read (access denied, locked, IO errors) is a failure (1); a damaged one is not (0)
            return e is IOException or UnauthorizedAccessException || searchErrors > 0 ? 1 : 0;
        }

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
                return 1;
            }
        }

        Log.Information("CSV output will be saved to '{Csv}'\r\n", csv);

        var exportFailed = false;

        //(Timestamp ignored --dt: it was always yyyy-MM-dd HH:mm:ss, the --dt default)
        exportFailed |= !ExportCsv(sr.EnergyUsages.Values, csv, ts, "EnergyUsage", EnergyUsage.TableName, map =>
        {
            map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt));
            map.Map(t => t.EventTimestamp).Convert(t => $"{t.Value.EventTimestamp?.ToString(dt)}");
        });

        exportFailed |= !ExportCsv(sr.TimelineProviders.Values, csv, ts, "AppTimelineProvider", TimelineProvider.TableName, map =>
        {
            map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt));
            map.Map(t => t.EndTime).Convert(t => t.Value.EndTime.ToString(dt));
        });

        exportFailed |= !ExportCsv(sr.Vfuprovs.Values, csv, ts, "vfuprov", Vfuprov.TableName, map =>
        {
            map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt));
            map.Map(t => t.EndTime).Convert(t => t.Value.EndTime.ToString(dt));
            map.Map(t => t.StartTime).Convert(t => t.Value.StartTime.ToString(dt));
        });

        exportFailed |= !ExportCsv(sr.AppResourceUseInfos.Values, csv, ts, "AppResourceUseInfo", AppResourceUseInfo.TableName,
            map => map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt)));

        exportFailed |= !ExportCsv(sr.NetworkConnections.Values, csv, ts, "NetworkConnections", NetworkConnection.TableName, map =>
        {
            map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt));
            map.Map(t => t.ConnectStartTime).Convert(t => t.Value.ConnectStartTime.ToString(dt));
        });

        exportFailed |= !ExportCsv(sr.NetworkUsages.Values, csv, ts, "NetworkUsages", NetworkUsage.TableName,
            map => map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt)));

        exportFailed |= !ExportCsv(sr.PushNotifications.Values, csv, ts, "PushNotifications", PushNotification.TableName,
            map => map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt)));

        //the providers seen on Server 2022 (Issue #15): only when there are rows, as most systems don't have them
        if (sr.ProcessorTimes.Count > 0)
        {
            exportFailed |= !ExportCsv(sr.ProcessorTimes.Values, csv, ts, "ProcessorTime", ProcessorTimeInfo.TableName,
                map => map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt)));
        }

        if (sr.SizeInBytesInfos.Count > 0)
        {
            exportFailed |= !ExportCsv(sr.SizeInBytesInfos.Values, csv, ts, "SizeInBytes", SizeInBytesInfo.TableName,
                map => map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt)));
        }

        if (sr.TotalUsedInfos.Count > 0)
        {
            exportFailed |= !ExportCsv(sr.TotalUsedInfos.Values, csv, ts, "TotalUsed", TotalUsedInfo.TableName,
                map => map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt)));
        }

        if (sr.NetworkBytesInfos.Count > 0)
        {
            exportFailed |= !ExportCsv(sr.NetworkBytesInfos.Values, csv, ts, "NetworkBytes", NetworkBytesInfo.TableName,
                map => map.Map(t => t.Timestamp).Convert(t => t.Value.Timestamp.ToString(dt)));
        }

        sw.Stop();

        if (sr.ParseErrors.Count > 0)
        {
            Log.Warning("{Count:N0} problem(s) found while reading the database (everything else was exported):", sr.ParseErrors.Count);
            foreach (var parseError in sr.ParseErrors.Take(50))
            {
                Log.Warning("\t{ParseError}", parseError);
            }

            if (sr.ParseErrors.Count > 50)
            {
                Log.Warning("\t... and {Count:N0} more (use --debug to see them all as they happen)", sr.ParseErrors.Count - 50);
            }

            Console.WriteLine();
        }

        //a SOFTWARE hive that could not be read (locked, access denied) is a failure (1); a damaged one is only a warning (0)
        if (sr.SoftwareHiveNotAccessed)
        {
            Log.Error("Unable to read SOFTWARE hive '{R}': user and network profile names are missing (see above)", r);
            Console.WriteLine();
        }

        Log.Information("Processing completed in {TotalSeconds:N4} seconds\r\n", sw.Elapsed.TotalSeconds);

        return exportFailed || searchErrors > 0 || sr.SoftwareHiveNotAccessed ? 1 : 0;
    }

    /// <summary>The exit code when -d had no SRUDB.dat: 0, or 1 when part of the folder could not be searched</summary>
    private static int SearchResult(string d, int searchErrors)
    {
        if (searchErrors == 0)
        {
            return 0;
        }

        Log.Error("Could not access all files in '{D}': {Count:N0} folder(s) could not be read", d, searchErrors);
        return 1;
    }

#if NET6_0_OR_GREATER
    /// <summary>
    ///     Files under a folder, recursively, with this name (case-insensitive). Folders that can't be read are skipped and
    ///     counted (IgnoreInaccessible skipped them silently)
    /// </summary>
    private sealed class EntryFinder : System.IO.Enumeration.FileSystemEnumerator<string>
    {
        private readonly string _name;

        //the folder whose handle is opened right after ShouldRecurseIntoEntry returns true: when that fails, ContinueOnError
        //is called before ShouldIncludeEntry for the same entry. Null: the folder being searched (the root, when the base
        //constructor can't open it; field initializers run before the base constructor, so Unreadable is there)
        private string _lastRecursed;

        /// <summary>Folders that could not be read (null: the root folder) and why</summary>
        public readonly List<(string Folder, string Reason)> Unreadable = new();

        public EntryFinder(string directory, string name) : base(directory,
            new EnumerationOptions { IgnoreInaccessible = false, RecurseSubdirectories = true, AttributesToSkip = 0 })
        {
            _name = name;
        }

        protected override bool ShouldRecurseIntoEntry(ref System.IO.Enumeration.FileSystemEntry entry)
        {
            _lastRecursed = entry.ToFullPath();
            return true;
        }

        protected override bool ShouldIncludeEntry(ref System.IO.Enumeration.FileSystemEntry entry)
        {
            _lastRecursed = null;
            return System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(_name, entry.FileName, true);
        }

        protected override string TransformEntry(ref System.IO.Enumeration.FileSystemEntry entry)
        {
            return entry.ToFullPath();
        }

        protected override bool ContinueOnError(int error)
        {
            Unreadable.Add((_lastRecursed, new System.ComponentModel.Win32Exception(error).Message));
            _lastRecursed = null;
            return true;
        }
    }

    /// <summary>The first match is enough, as before (the search stops there)</summary>
    private static IEnumerable<string> FindEntries(string directory, string name, ref int errors)
    {
        using var finder = new EntryFinder(directory, name);
        var found = new List<string>();
        if (finder.MoveNext())
        {
            found.Add(finder.Current);
        }

        foreach (var (folder, reason) in finder.Unreadable)
        {
            errors += ReportUnreadable(folder ?? directory, reason);
        }

        return found;
    }
#endif

    //-d is searched twice (SRUDB.dat, then SOFTWARE): each folder that can't be read is reported once
    private static readonly HashSet<string> UnreadableFolders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Logs a folder that could not be read, the first time; 1 then, else 0</summary>
    private static int ReportUnreadable(string folder, string reason)
    {
        if (UnreadableFolders.Add(folder) == false)
        {
            return 0;
        }

        Log.Error("Unable to read folder {Folder}: {Reason}", folder, reason?.Trim());
        return 1;
    }

    private static int Depth(Type type)
    {
        var depth = 0;
        for (var t = type; t?.BaseType != null; t = t.BaseType)
        {
            depth += 1;
        }

        return depth;
    }

    /// <summary>
    ///     One CSV (written even when it has no rows, as before)
    /// </summary>
    private static bool ExportCsv<T>(IEnumerable<T> records, string csv, DateTimeOffset ts, string name, string tableName,
        Action<CsvHelper.Configuration.ClassMap<T>> configure)
    {
        try
        {
            Log.Debug("Dumping {Name} table '{TableName}'", name, tableName);

            var outFile = Path.Combine(csv, $"{ts:yyyyMMddHHmmss}_SrumECmd_{name}_Output.csv");

            //(the writers were never closed)
            using var swCsv = new StreamWriter(outFile, false, Encoding.UTF8);
            using var csvWriter = new CsvWriter(swCsv, CultureInfo.InvariantCulture);

            var map = csvWriter.Context.AutoMap<T>();
            configure(map);

            //SrumRecord's columns (Id, Timestamp, ExeInfo, ...) first, as in the other CSVs, then the table's own
            if (map.MemberMaps.Select(m => m.Data.Member?.DeclaringType).Distinct().Count() > 1)
            {
                var ordered = map.MemberMaps.OrderBy(m => Depth(m.Data.Member?.DeclaringType)).ToList();
                for (var i = 0; i < ordered.Count; i++)
                {
                    ordered[i].Data.Index = i;
                    ordered[i].Data.IsIndexSet = true;
                }
            }

            csvWriter.Context.RegisterClassMap(map);
            csvWriter.WriteHeader<T>();
            csvWriter.NextRecord();

            csvWriter.WriteRecords(records);

            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error exporting '{Name}' data! Error: {Message}", name, e.Message);
            return false;
        }
    }

    private class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public CustomHelpAction(HelpAction action)
        {
            _defaultHelp = action;
        }

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);

            Log.Warning("{Msg}", string.Join(" ",parseResult.Tokens));

            return result;
        }
    }

    
}

