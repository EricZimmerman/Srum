using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Isam.Esent.Interop;
using Serilog;

namespace SrumData
{
    /// <summary>
    ///     ESE databases opened from copies in a temporary folder, never from where they were found: ESE writes its
    ///     checkpoint, logs and temporary files next to the databases, replays logs into them and attaches read-write.
    ///     The copies' transaction logs are replayed when there are any; a database that is still dirty (its logs were not
    ///     collected) is repaired with esentutl /p, as users were told to do by hand.
    /// </summary>
    internal sealed class EseWorkspace : IDisposable
    {
        //JET_bitReplayIgnoreMissingDB (esentutl /r ... /i): logs that refer to databases that are not there are ignored
        private const InitGrbit ReplayIgnoreMissingDb = (InitGrbit) 0x4;

        private static readonly string[] LogExtensions = {".log", ".chk", ".jrs"};

        private readonly Action<string> _warning;
        private readonly HashSet<string> _repaired = new(StringComparer.OrdinalIgnoreCase);
        private Instance _instance;

        private EseWorkspace(string workDirectory, Action<string> warning)
        {
            WorkDirectory = workDirectory;
            _warning = warning;
        }

        public string WorkDirectory { get; }

        public Session Session { get; private set; }

        /// <summary>
        ///     Copies <paramref name="databases" /> and the transaction log files named <paramref name="baseName" />*
        ///     from their folder, then starts ESE on the copies (replaying the logs when there are any)
        /// </summary>
        /// <param name="sourceDirectory">The folder of the databases</param>
        /// <param name="databases">Full paths of the databases to copy</param>
        /// <param name="baseName">ESE's three-letter base name of the log files: Svc (UAL), SRU (SRUM)</param>
        /// <param name="warning">Gets what was done to the copies (replay failed, repaired)</param>
        /// <param name="tempDirectory">Where the copies' folder is made (it must exist; null: the system's temp folder)</param>
        public static EseWorkspace Create(string sourceDirectory, IEnumerable<string> databases, string baseName,
            Action<string> warning, string tempDirectory = null)
        {
            var work = Path.Combine(tempDirectory ?? Path.GetTempPath(), $"{typeof(EseWorkspace).Namespace}-{Guid.NewGuid():N}");
            var logs = Path.Combine(work, "logs");
            Directory.CreateDirectory(logs);

            var ws = new EseWorkspace(work, warning);

            try
            {
                foreach (var db in databases)
                {
                    CopyFile(db, Path.Combine(work, Path.GetFileName(db)));
                }

                var logFiles = Directory.GetFiles(sourceDirectory)
                    .Where(f => Path.GetFileName(f).StartsWith(baseName, StringComparison.OrdinalIgnoreCase) &&
                                LogExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .ToList();

                foreach (var f in logFiles)
                {
                    CopyFile(f, Path.Combine(logs, Path.GetFileName(f)));
                }

                var hasLogs = logFiles.Any(f => Path.GetExtension(f).Equals(".log", StringComparison.OrdinalIgnoreCase));

                if (hasLogs)
                {
                    try
                    {
                        ws.Start(logs, baseName, true);
                        Log.Debug("Transaction logs in '{SourceDirectory}' replayed into the copies", sourceDirectory);
                    }
                    catch (Exception e)
                    {
                        ws.Stop();
                        warning(
                            $"The transaction logs in '{sourceDirectory}' could not be replayed ({e.Message}). The databases are read without them");
                    }
                }

                if (ws._instance == null)
                {
                    //an empty log folder of its own: no recovery, and nothing to confuse with the copied logs
                    var noLogs = Path.Combine(work, "nologs");
                    Directory.CreateDirectory(noLogs);
                    ws.Start(noLogs, baseName, false);
                }
            }
            catch
            {
                ws.Dispose();
                throw;
            }

            return ws;
        }

        /// <summary>
        ///     Copies a file that may be in use (open by the service on a live system)
        /// </summary>
        private static void CopyFile(string source, string destination)
        {
            using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var dst = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            src.CopyTo(dst);
        }

        private void Start(string logDirectory, string baseName, bool recovery)
        {
            _instance = new Instance($"{typeof(EseWorkspace).Namespace}-{Guid.NewGuid():N}");
            var p = _instance.Parameters;
            p.CreatePathIfNotExist = true;
            p.BaseName = baseName;
            p.LogFileDirectory = logDirectory;
            p.SystemDirectory = logDirectory;
            p.TempDirectory = WorkDirectory;
            //where recovery finds the databases (the logs have the paths they were at)
            p.AlternateDatabaseRecoveryDirectory = WorkDirectory;
            p.Recovery = recovery;
            p.NoInformationEvent = true;
            //secondary indexes can go stale after OS and NLS updates
            p.EnableIndexCleanup = true;

            _instance.Init(recovery ? ReplayIgnoreMissingDb : InitGrbit.None);

            Session = new Session(_instance);
        }

        private void Stop()
        {
            Session?.Dispose();
            Session = null;
            _instance?.Dispose();
            _instance = null;
        }

        /// <summary>
        ///     Attaches and opens the copy of a database (a full path in <see cref="WorkDirectory" />). A copy that is
        ///     still dirty is repaired once with esentutl /p
        /// </summary>
        public JET_DBID OpenDatabase(string copy)
        {
            try
            {
                Attach(copy);
            }
            catch (EsentErrorException e) when (!_repaired.Contains(copy))
            {
                Log.Debug("Attaching '{Copy}' failed ({Message}); repairing the copy", copy, e.Message);

                Repair(copy, e.Message);
                Attach(copy);
            }

            Api.JetOpenDatabase(Session, copy, null, out var dbid, OpenDatabaseGrbit.ReadOnly);
            return dbid;
        }

        private void Attach(string copy)
        {
            //read-write on the copy, so stale secondary indexes can be deleted
            Api.JetAttachDatabase(Session, copy, AttachDatabaseGrbit.DeleteCorruptIndexes);
        }

        public void CloseDatabase(string copy, JET_DBID dbid)
        {
            try
            {
                Api.JetCloseDatabase(Session, dbid, CloseDatabaseGrbit.None);
                Api.JetDetachDatabase(Session, copy);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Closing '{Copy}': {Message}", copy, e.Message);
            }
        }

        private void Repair(string copy, string why)
        {
            _repaired.Add(copy);

            var esentutl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "esentutl.exe");
            if (!File.Exists(esentutl))
            {
                throw new FileNotFoundException($"'{Path.GetFileName(copy)}' can't be opened ({why}) and esentutl.exe, which could repair a copy, was not found", esentutl);
            }

            var psi = new ProcessStartInfo(esentutl,
                $"/p \"{copy}\" /o /t\"{Path.Combine(WorkDirectory, "repair.tmp")}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = WorkDirectory
            };

            using var p = Process.Start(psi);
            p.StandardInput.Close();
            var output = p.StandardOutput.ReadToEndAsync();
            p.StandardError.ReadToEndAsync();

            if (!p.WaitForExit((int) TimeSpan.FromMinutes(30).TotalMilliseconds))
            {
                try
                {
                    p.Kill();
                }
                catch
                {
                    //gone already
                }

                throw new TimeoutException($"esentutl /p on a copy of '{Path.GetFileName(copy)}' did not finish in 30 minutes");
            }

            Log.Verbose("esentutl /p: {Output}", output.Result);

            if (p.ExitCode != 0)
            {
                var last = output.Result.Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                throw new InvalidOperationException(
                    $"'{Path.GetFileName(copy)}' can't be opened ({why}), and esentutl /p could not repair a copy: {last}");
            }

            _warning(
                $"'{Path.GetFileName(copy)}' was not shut down cleanly and its transaction logs could not bring it up to date, so a copy was repaired with esentutl /p (what only the missing logs had is lost)");
        }

        public void Dispose()
        {
            Stop();

            //ESE may hold its files for a moment after JetTerm
            for (var i = 0; i < 5 && Directory.Exists(WorkDirectory); i++)
            {
                try
                {
                    Directory.Delete(WorkDirectory, true);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }
}
