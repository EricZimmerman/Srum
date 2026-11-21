using System;
using System.Globalization;
using CsvHelper;
using SrumData;

namespace SrumECmd;

/// <summary>
///     Writes the columns of a SrumData type (a library type, which has no CsvHelper attributes; its properties are
///     mostly get-only, which CsvHelper 34 record types don't write): a name and a writer per column, in order
/// </summary>
internal sealed class ColumnsCsvWriter<T> : CsvRecordWriter<T>
{
    private readonly (string Name, Action<CsvWriter, T> Write)[] _columns;

    public ColumnsCsvWriter(params (string Name, Action<CsvWriter, T> Write)[] columns)
    {
        _columns = columns;
    }

    public override int FieldCount => _columns.Length;

    public override void WriteHeader(CsvWriter writer)
    {
        foreach (var column in _columns)
        {
            WriteColumnName(writer, column.Name);
        }
    }

    public override void Write(CsvWriter writer, T record)
    {
        foreach (var column in _columns)
        {
            column.Write(writer, record);
        }
    }
}

/// <summary>
///     The columns of each CSV, as CsvHelper 33 wrote them: SrumRecord's columns (Id, Timestamp, ExeInfo, ...) first, then
///     the table's own; the time stamps of each table in the --dt format (current culture), ExeTimestamp as CsvHelper 33's
///     default (invariant culture, MM/dd/yyyy HH:mm:ss zzz), enums by name, empty for null
/// </summary>
internal static class SrumCsv
{
    private static string Invariant(DateTimeOffset? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(TimeSpan value) => value.ToString("c", CultureInfo.InvariantCulture);

    public static CsvRecordWriter<EnergyUsage> EnergyUsage(string dt) => new ColumnsCsvWriter<EnergyUsage>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("IsLt", (w, r) => w.WriteField(r.IsLt)),
        ("ConfigurationHash", (w, r) => w.WriteField(r.ConfigurationHash)),
        ("EventTimestamp", (w, r) => w.WriteField(r.EventTimestamp?.ToString(dt))),
        ("StateTransition", (w, r) => w.WriteField(r.StateTransition)),
        ("ChargeLevel", (w, r) => w.WriteField(r.ChargeLevel)),
        ("CycleCount", (w, r) => w.WriteField(r.CycleCount)),
        ("DesignedCapacity", (w, r) => w.WriteField(r.DesignedCapacity)),
        ("FullChargedCapacity", (w, r) => w.WriteField(r.FullChargedCapacity)),
        ("ActiveAcTime", (w, r) => w.WriteField(r.ActiveAcTime)),
        ("ActiveDcTime", (w, r) => w.WriteField(r.ActiveDcTime)),
        ("ActiveDischargeTime", (w, r) => w.WriteField(r.ActiveDischargeTime)),
        ("ActiveEnergy", (w, r) => w.WriteField(r.ActiveEnergy)),
        ("CsAcTime", (w, r) => w.WriteField(r.CsAcTime)),
        ("CsDcTime", (w, r) => w.WriteField(r.CsDcTime)),
        ("CsDischargeTime", (w, r) => w.WriteField(r.CsDischargeTime)),
        ("CsEnergy", (w, r) => w.WriteField(r.CsEnergy)));

    public static CsvRecordWriter<TimelineProvider> TimelineProvider(string dt) => new ColumnsCsvWriter<TimelineProvider>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("EndTime", (w, r) => w.WriteField(r.EndTime.ToString(dt))),
        ("DurationMs", (w, r) => w.WriteField(r.DurationMs)),
        ("InFocusS", (w, r) => w.WriteField(Invariant(r.InFocusS))),
        ("PsmForegroundS", (w, r) => w.WriteField(Invariant(r.PsmForegroundS))),
        ("UserInputS", (w, r) => w.WriteField(Invariant(r.UserInputS))),
        ("KeyboardInputS", (w, r) => w.WriteField(Invariant(r.KeyboardInputS))),
        ("MouseInputS", (w, r) => w.WriteField(Invariant(r.MouseInputS))),
        ("DisplayRequiredS", (w, r) => w.WriteField(Invariant(r.DisplayRequiredS))),
        ("AudioInS", (w, r) => w.WriteField(Invariant(r.AudioInS))),
        ("AudioOutS", (w, r) => w.WriteField(Invariant(r.AudioOutS))));

    public static CsvRecordWriter<Vfuprov> Vfuprov(string dt) => new ColumnsCsvWriter<Vfuprov>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("StartTime", (w, r) => w.WriteField(r.StartTime.ToString(dt))),
        ("EndTime", (w, r) => w.WriteField(r.EndTime.ToString(dt))),
        ("Flags", (w, r) => w.WriteField(r.Flags)),
        ("Duration", (w, r) => w.WriteField(Invariant(r.Duration))));

    public static CsvRecordWriter<AppResourceUseInfo> AppResourceUseInfo(string dt) => new ColumnsCsvWriter<AppResourceUseInfo>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("BackgroundBytesRead", (w, r) => w.WriteField(r.BackgroundBytesRead)),
        ("BackgroundBytesWritten", (w, r) => w.WriteField(r.BackgroundBytesWritten)),
        ("BackgroundContextSwitches", (w, r) => w.WriteField(r.BackgroundContextSwitches)),
        ("BackgroundCycleTime", (w, r) => w.WriteField(r.BackgroundCycleTime)),
        ("BackgroundNumberOfFlushes", (w, r) => w.WriteField(r.BackgroundNumberOfFlushes)),
        ("BackgroundNumReadOperations", (w, r) => w.WriteField(r.BackgroundNumReadOperations)),
        ("BackgroundNumWriteOperations", (w, r) => w.WriteField(r.BackgroundNumWriteOperations)),
        ("FaceTime", (w, r) => w.WriteField(r.FaceTime)),
        ("ForegroundBytesRead", (w, r) => w.WriteField(r.ForegroundBytesRead)),
        ("ForegroundBytesWritten", (w, r) => w.WriteField(r.ForegroundBytesWritten)),
        ("ForegroundContextSwitches", (w, r) => w.WriteField(r.ForegroundContextSwitches)),
        ("ForegroundCycleTime", (w, r) => w.WriteField(r.ForegroundCycleTime)),
        ("ForegroundNumberOfFlushes", (w, r) => w.WriteField(r.ForegroundNumberOfFlushes)),
        ("ForegroundNumReadOperations", (w, r) => w.WriteField(r.ForegroundNumReadOperations)),
        ("ForegroundNumWriteOperations", (w, r) => w.WriteField(r.ForegroundNumWriteOperations)));

    public static CsvRecordWriter<NetworkConnection> NetworkConnection(string dt) => new ColumnsCsvWriter<NetworkConnection>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("ConnectedTime", (w, r) => w.WriteField(r.ConnectedTime)),
        ("ConnectStartTime", (w, r) => w.WriteField(r.ConnectStartTime.ToString(dt))),
        ("InterfaceLuid", (w, r) => w.WriteField(r.InterfaceLuid)),
        ("InterfaceType", (w, r) => w.WriteField(r.InterfaceType.ToString())),
        ("L2ProfileFlags", (w, r) => w.WriteField(r.L2ProfileFlags)),
        ("L2ProfileId", (w, r) => w.WriteField(r.L2ProfileId)),
        ("ProfileName", (w, r) => w.WriteField(r.ProfileName)));

    public static CsvRecordWriter<NetworkUsage> NetworkUsage(string dt) => new ColumnsCsvWriter<NetworkUsage>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("BytesReceived", (w, r) => w.WriteField(r.BytesReceived)),
        ("BytesSent", (w, r) => w.WriteField(r.BytesSent)),
        ("InterfaceLuid", (w, r) => w.WriteField(r.InterfaceLuid)),
        ("InterfaceType", (w, r) => w.WriteField(r.InterfaceType.ToString())),
        ("L2ProfileFlags", (w, r) => w.WriteField(r.L2ProfileFlags)),
        ("L2ProfileId", (w, r) => w.WriteField(r.L2ProfileId)),
        ("ProfileName", (w, r) => w.WriteField(r.ProfileName)));

    public static CsvRecordWriter<PushNotification> PushNotification(string dt) => new ColumnsCsvWriter<PushNotification>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("NetworkType", (w, r) => w.WriteField(r.NetworkType)),
        ("NotificationType", (w, r) => w.WriteField(r.NotificationType)),
        ("PayloadSize", (w, r) => w.WriteField(r.PayloadSize)));

    public static CsvRecordWriter<ProcessorTimeInfo> ProcessorTimeInfo(string dt) => new ColumnsCsvWriter<ProcessorTimeInfo>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("ProcessorTime", (w, r) => w.WriteField(r.ProcessorTime)));

    public static CsvRecordWriter<SizeInBytesInfo> SizeInBytesInfo(string dt) => new ColumnsCsvWriter<SizeInBytesInfo>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("SizeInBytes", (w, r) => w.WriteField(r.SizeInBytes)));

    public static CsvRecordWriter<TotalUsedInfo> TotalUsedInfo(string dt) => new ColumnsCsvWriter<TotalUsedInfo>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("Total", (w, r) => w.WriteField(r.Total)),
        ("Used", (w, r) => w.WriteField(r.Used)));

    public static CsvRecordWriter<NetworkBytesInfo> NetworkBytesInfo(string dt) => new ColumnsCsvWriter<NetworkBytesInfo>(
        ("Id", (w, r) => w.WriteField(r.Id)),
        ("Timestamp", (w, r) => w.WriteField(r.Timestamp.ToString(dt))),
        ("ExeInfo", (w, r) => w.WriteField(r.ExeInfo)),
        ("ExeInfoDescription", (w, r) => w.WriteField(r.ExeInfoDescription)),
        ("ExeTimestamp", (w, r) => w.WriteField(Invariant(r.ExeTimestamp))),
        ("SidType", (w, r) => w.WriteField(r.SidType.ToString())),
        ("Sid", (w, r) => w.WriteField(r.Sid)),
        ("UserName", (w, r) => w.WriteField(r.UserName)),
        ("UserId", (w, r) => w.WriteField(r.UserId)),
        ("AppId", (w, r) => w.WriteField(r.AppId)),
        ("BytesInBound", (w, r) => w.WriteField(r.BytesInBound)),
        ("BytesOutBound", (w, r) => w.WriteField(r.BytesOutBound)),
        ("BytesTotal", (w, r) => w.WriteField(r.BytesTotal)));
}
