using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Isam.Esent.Interop;

namespace SrumTest;

/// <summary>
///     Builds SRUM databases with the schema of real ones (SruDbIdMapTable and provider tables, column names and types
///     from Windows 10/11 and Server 2022 SRUDB.dat files), so the reading paths can be tested on any data, including
///     damaged values
/// </summary>
internal static class SrumBuilder
{
    private const JET_coltyp Int64 = (JET_coltyp) 15;

    public const string NetworkUsageTable = "{973F5D5C-1D90-4944-BE8E-24B94231A174}";
    public const string TimelineTable = "{5C8CF1C7-7257-4F13-B223-970EF5939312}";
    public const string VfuprovTable = "{7ACBBAA3-D029-4BE4-9A7A-0885927F1D8F}";
    public const string TotalUsedTable = "{17F4D97B-F26A-5E79-3A82-90040A47D13D}";

    public static long FileTime(int year, int month, int day) => new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();

    /// <summary>
    ///     Creates SRUDB.dat. <paramref name="dirty" />: ESE is ended without a clean shutdown, so the database stays in
    ///     dirty shutdown with its SRU*.log files next to it
    /// </summary>
    public static void Create(string path, Action<JET_SESID, JET_DBID> fill, bool dirty = false)
    {
        var folder = Path.GetDirectoryName(path)!;
        var logs = dirty ? folder : Path.Combine(Path.GetTempPath(), $"srumbuilder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(logs);

        Api.JetCreateInstance(out var instance, $"srumbuilder-{Guid.NewGuid():N}");
        _ = new InstanceParameters(instance)
        {
            BaseName = "SRU",
            LogFileDirectory = logs,
            SystemDirectory = logs,
            TempDirectory = logs,
            CreatePathIfNotExist = true,
            NoInformationEvent = true,
            CircularLog = false
        };
        Api.JetInit(ref instance);

        Api.JetBeginSession(instance, out var sesid, null, null);
        Api.JetCreateDatabase(sesid, path, null, out var dbid, CreateDatabaseGrbit.OverwriteExisting);

        fill(sesid, dbid);

        if (dirty)
        {
            //JET_bitTermDirty: no clean shutdown (TermGrbit.Abrupt still shut down cleanly)
            try
            {
                Api.JetTerm2(instance, (TermGrbit) 0x8);
            }
            catch (EsentDirtyShutdownException)
            {
                //what was asked for
            }

            return;
        }

        Api.JetCloseDatabase(sesid, dbid, CloseDatabaseGrbit.None);
        Api.JetDetachDatabase(sesid, path);
        Api.JetEndSession(sesid, EndSessionGrbit.None);
        Api.JetTerm(instance);

        try
        {
            Directory.Delete(logs, true);
        }
        catch (IOException)
        {
        }
    }

    public static void CreateTable(JET_SESID s, JET_DBID db, string name, params (string Name, JET_coltyp Type)[] columns)
    {
        Api.JetBeginTransaction(s);
        Api.JetCreateTable(s, db, name, 1, 100, out var table);

        foreach (var (colName, type) in columns)
        {
            var def = new JET_COLUMNDEF
            {
                coltyp = type,
                grbit = type == JET_coltyp.LongBinary ? ColumndefGrbit.ColumnTagged : ColumndefGrbit.None
            };
            Api.JetAddColumn(s, table, colName, def, null, 0, out _);
        }

        Api.JetCloseTable(s, table);
        Api.JetCommitTransaction(s, CommitTransactionGrbit.None);
    }

    public static void Insert(JET_SESID s, JET_DBID db, string tableName, Action<JET_TABLEID, IDictionary<string, JET_COLUMNID>> set)
    {
        using var table = new Table(s, db, tableName, OpenTableGrbit.None);
        var cols = Api.GetColumnDictionary(s, table);

        Api.JetBeginTransaction(s);
        using (var update = new Update(s, table, JET_prep.Insert))
        {
            set(table, cols);
            update.Save();
        }

        Api.JetCommitTransaction(s, CommitTransactionGrbit.None);
    }

    public static void IdMapTable(JET_SESID s, JET_DBID db, IEnumerable<(int Index, byte Type, byte[] Blob)> entries)
    {
        CreateTable(s, db, "SruDbIdMapTable", ("IdBlob", JET_coltyp.LongBinary), ("IdIndex", JET_coltyp.Long), ("IdType", JET_coltyp.UnsignedByte));

        foreach (var (index, type, blob) in entries)
        {
            Insert(s, db, "SruDbIdMapTable", (t, c) =>
            {
                Api.SetColumn(s, t, c["IdIndex"], index);
                Api.SetColumn(s, t, c["IdType"], type);
                if (blob != null)
                {
                    Api.SetColumn(s, t, c["IdBlob"], blob);
                }
            });
        }
    }

    public static byte[] Text(string value) => Encoding.Unicode.GetBytes(value + "\0");

    /// <summary>
    ///     S-1-5-21-1-2-3-1001 as stored
    /// </summary>
    public static readonly byte[] UserSid =
    {
        1, 5, 0, 0, 0, 0, 0, 5, 21, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 0xE9, 3, 0, 0
    };

    public static void NetworkUsage(JET_SESID s, JET_DBID db, IEnumerable<(int Id, int AppId, int UserId, long Received, long Sent)> rows)
    {
        CreateTable(s, db, NetworkUsageTable,
            ("AppId", JET_coltyp.Long), ("AutoIncId", JET_coltyp.Long), ("BytesRecvd", Int64), ("BytesSent", Int64),
            ("InterfaceLuid", Int64), ("L2ProfileFlags", JET_coltyp.Long), ("L2ProfileId", JET_coltyp.Long),
            ("TimeStamp", JET_coltyp.DateTime), ("UserId", JET_coltyp.Long));

        foreach (var (id, appId, userId, received, sent) in rows)
        {
            Insert(s, db, NetworkUsageTable, (t, c) =>
            {
                Api.SetColumn(s, t, c["AutoIncId"], id);
                Api.SetColumn(s, t, c["AppId"], appId);
                Api.SetColumn(s, t, c["UserId"], userId);
                Api.SetColumn(s, t, c["BytesRecvd"], received);
                Api.SetColumn(s, t, c["BytesSent"], sent);
                //IF_TYPE_IEEE80211 (71) in the top 16 bits
                Api.SetColumn(s, t, c["InterfaceLuid"], 71L << 48);
                Api.SetColumn(s, t, c["L2ProfileFlags"], 0);
                Api.SetColumn(s, t, c["L2ProfileId"], 268435457);
                Api.SetColumn(s, t, c["TimeStamp"], new DateTime(2024, 5, 6, 7, 8, 0));
            });
        }
    }

    public static void Timeline(JET_SESID s, JET_DBID db, IEnumerable<(int Id, int AppId, int UserId, long EndTime, int? InFocusS)> rows, bool withStateColumns = true)
    {
        var cols = new List<(string, JET_coltyp)>
        {
            ("AppId", JET_coltyp.Long), ("AutoIncId", JET_coltyp.Long), ("DurationMS", JET_coltyp.Long), ("EndTime", Int64),
            ("TimeStamp", JET_coltyp.DateTime), ("UserId", JET_coltyp.Long)
        };
        if (withStateColumns)
        {
            cols.AddRange(new[] {("InFocusS", JET_coltyp.Long), ("UserInputS", JET_coltyp.Long), ("AudioOutS", JET_coltyp.Long)});
        }

        CreateTable(s, db, TimelineTable, cols.ToArray());

        foreach (var (id, appId, userId, endTime, inFocus) in rows)
        {
            Insert(s, db, TimelineTable, (t, c) =>
            {
                Api.SetColumn(s, t, c["AutoIncId"], id);
                Api.SetColumn(s, t, c["AppId"], appId);
                Api.SetColumn(s, t, c["UserId"], userId);
                Api.SetColumn(s, t, c["DurationMS"], 60000);
                Api.SetColumn(s, t, c["EndTime"], endTime);
                Api.SetColumn(s, t, c["TimeStamp"], new DateTime(2024, 5, 6, 7, 8, 0));
                if (inFocus != null && c.ContainsKey("InFocusS"))
                {
                    Api.SetColumn(s, t, c["InFocusS"], inFocus.Value);
                    Api.SetColumn(s, t, c["UserInputS"], 3);
                    Api.SetColumn(s, t, c["AudioOutS"], 4);
                }
            });
        }
    }

    public static void TotalUsed(JET_SESID s, JET_DBID db, int appId)
    {
        CreateTable(s, db, TotalUsedTable,
            ("AppId", JET_coltyp.Long), ("AutoIncId", JET_coltyp.Long), ("TimeStamp", JET_coltyp.DateTime), ("Total", Int64),
            ("Used", Int64), ("UserId", JET_coltyp.Long));

        Insert(s, db, TotalUsedTable, (t, c) =>
        {
            Api.SetColumn(s, t, c["AutoIncId"], 1);
            Api.SetColumn(s, t, c["AppId"], appId);
            Api.SetColumn(s, t, c["UserId"], 20);
            Api.SetColumn(s, t, c["Total"], 64078475264L);
            Api.SetColumn(s, t, c["Used"], 12385419264L);
            Api.SetColumn(s, t, c["TimeStamp"], new DateTime(2023, 10, 18, 3, 30, 0));
        });
    }

    /// <summary>
    ///     An id map with an app (10), a user (20), a "!!" app with a bad time stamp (11), a SID blob that is not a whole
    ///     number of sub authorities (21), an object (30), and the network and timeline tables referring to them
    /// </summary>
    public static void Standard(string path, bool dirty = false)
    {
        Create(path, (s, db) =>
        {
            IdMapTable(s, db, new (int, byte, byte[])[]
            {
                (10, 0, Text(@"\Device\HarddiskVolume3\Windows\System32\svchost.exe")),
                (11, 0, Text("!!app.exe!not a time!0!App description")),
                (12, 2, Text("Microsoft.WindowsCalculator_8wekyb3d8bbwe")),
                (20, 3, UserSid),
                (21, 3, new byte[] {1, 1, 0, 0, 0, 0, 0, 5, 18, 0}),
                (30, 4, Text("Total"))
            });

            NetworkUsage(s, db, new[]
            {
                (1, 10, 20, 1000L, 2000L),
                (2, 11, 21, 3000L, 4000L),
                //ids that are not in the map (the whole table was lost)
                (3, 99, 98, 5000L, 6000L)
            });

            Timeline(s, db, new[]
            {
                (1, 12, 20, FileTime(2024, 5, 6), (int?) 30),
                //a FILETIME DateTimeOffset can't hold (the whole table was lost)
                (2, 10, 20, long.MaxValue, (int?) null)
            });

            TotalUsed(s, db, 30);
        }, dirty);
    }
}
