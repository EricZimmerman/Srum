using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Isam.Esent.Interop;
using Registry;
using Serilog;
using ServiceStack;
using ServiceStack.Text;

namespace SrumData;

/// <summary>
///     FILETIMEs that can be out of range in damaged databases
/// </summary>
internal static class SrumTime
{
    //largest FILETIME a DateTimeOffset can hold
    internal static readonly long MaxFileTime = DateTime.MaxValue.ToFileTimeUtc();

    internal static bool IsValid(long fileTime)
    {
        return fileTime >= 0 && fileTime <= MaxFileTime;
    }

    /// <summary>
    ///     As UTC; <see cref="DateTimeOffset.MinValue" /> when out of range (DateTimeOffset.FromFileTime threw)
    /// </summary>
    internal static DateTimeOffset FromFileTime(long fileTime)
    {
        return IsValid(fileTime) ? new DateTimeOffset(DateTime.FromFileTimeUtc(fileTime)) : DateTimeOffset.MinValue;
    }
}

public class AppInfo
{
    public AppInfo(int index, MapType mapType, string exeInfo)
    {
        Index = index;
        MapType = mapType;
        ExeInfo = exeInfo ?? string.Empty;

        if (!ExeInfo.StartsWith("!!"))
        {
            return;
        }

        var tempVal = ExeInfo.Substring(2); //strip !!
        var segs = tempVal.Split('!');

        if (segs.Length < 4)
        {
            return;
        }

        // Incase the filename starts with "!" return it, otherwise get original value.
        ExeInfo = segs[0].IsNullOrEmpty() ? "!" : segs[0];

        int segsLength = segs.Length;

        // Incase the filename had "!" in it, append everything back up untill the file type extension.
        for (int i = 1; i < segsLength - 3; i++)
        {
            // If the string is empty it means there were several "!", else append "!<originalValue>"
            if (segs[i].IsNullOrEmpty())
            {
                ExeInfo += "!";
            }
            else
            {
                ExeInfo += "!" + segs[i];
            }
        }

        Unknown = segs[segsLength - 2];
        ExeInfoDescription = segs[segsLength - 1];

        //(ParseExact threw for anything else, and that lost the whole id map: nothing could be read)
        if (DateTimeOffset.TryParseExact(segs[segsLength - 3], "yyyy/MM/dd:HH:mm:ss", null,
                DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            Timestamp = timestamp;
        }
    }

    public int Index { get; }
    public MapType MapType { get; }

    public string ExeInfo { get; }
    public string ExeInfoDescription { get; }

    public DateTimeOffset? Timestamp { get; }

    public string Unknown { get; }
}

public class UserInfo
{
    public UserInfo(int index, string sid, string userName)
    {
        Index = index;
        Sid = sid;
        UserName = userName;

        SidType = Srum.GetSidTypeFromSidString(sid ?? string.Empty);
    }

    public int Index { get; }
    public MapType MapType => MapType.Sid;
    public SidTypeEnum SidType { get; }
    public string Sid { get; }
    public string UserName { get; }
}

public enum MapType
{
    NormalApp = 0,
    Service = 1,
    ModernApp = 2,
    Sid = 3,

    /// <summary>
    ///     What the newer providers measure (Issue #15): a volume ("{guid}, tab, drive letter, ..."), "Total", a network
    ///     adapter ("{guid}\name\speed"). They were skipped
    /// </summary>
    Object = 4
}

public class NetworkUsage
{
    public NetworkUsage(int id, DateTime timestamp, int appId, int userId, long bytesReceived, long bytesSent,
        long interfaceLuid, int l2ProfileFlags, int l2ProfileId, string profileName)
    {
        Id = id;
        var tsUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        Timestamp = new DateTimeOffset(tsUtc);
        AppId = appId;
        UserId = userId;
        BytesReceived = bytesReceived;
        BytesSent = bytesSent;
        InterfaceLuid = interfaceLuid;
        L2ProfileFlags = l2ProfileFlags;
        L2ProfileId = l2ProfileId;
        ProfileName = profileName;
        InterfaceType = Srum.GetInterfaceTypeFromLuid(interfaceLuid);
    }

    public int Id { get; }
    public DateTimeOffset Timestamp { get; }

    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }

    public int UserId { get; }
    public int AppId { get; }

    public long BytesReceived { get; }
    public long BytesSent { get; }

    public long InterfaceLuid { get; }
    public InterfaceType InterfaceType { get; }
    public int L2ProfileFlags { get; }
    public int L2ProfileId { get; }
    public string ProfileName { get; }


    public static string TableName => "{973F5D5C-1D90-4944-BE8E-24B94231A174}";

    public override string ToString()
    {
        return $"{Timestamp}, User id: {UserId} profile: {ProfileName}, appId: {AppId}, sent: {BytesSent} rec: {BytesReceived}";
    }
}

public class PushNotification
{
    public PushNotification(int id, DateTime timestamp, int networkType, int notificationType, int payloadSize, int userId, int appId)
    {
        Id = id;
        var tsUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        Timestamp = new DateTimeOffset(tsUtc);

        NetworkType = networkType;
        NotificationType = notificationType;
        PayloadSize = payloadSize;

        UserId = userId;
        AppId = appId;
    }

    public int Id { get; }
    public DateTimeOffset Timestamp { get; }

    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }

    public int UserId { get; }
    public int AppId { get; }

    public int NetworkType { get; }
    public int NotificationType { get; }
    public int PayloadSize { get; }


    public static string TableName => "{D10CA2FE-6FCF-4F6D-848E-B2E99266FA86}";
}

public class NetworkConnection
{
    public NetworkConnection(int id, DateTime timestamp, int connectedTime, DateTimeOffset connectStartTime, long interfaceLuid, int l2ProfileFlags, int l2ProfileId, int userId, int appId, string profileName)
    {
        Id = id;
        var tsUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        Timestamp = new DateTimeOffset(tsUtc);
        ConnectedTime = connectedTime;
        ConnectStartTime = connectStartTime;
        InterfaceLuid = interfaceLuid;
        L2ProfileFlags = l2ProfileFlags;
        L2ProfileId = l2ProfileId;
        UserId = userId;
        AppId = appId;
        ProfileName = profileName;

        InterfaceType = Srum.GetInterfaceTypeFromLuid(interfaceLuid);
    }

    public int Id { get; }
    public DateTimeOffset Timestamp { get; }

    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }

    public int UserId { get; }
    public int AppId { get; }

    public int ConnectedTime { get; }
    public DateTimeOffset ConnectStartTime { get; }

    public long InterfaceLuid { get; }

    public InterfaceType InterfaceType { get; }
    public int L2ProfileFlags { get; }
    public int L2ProfileId { get; }
    public string ProfileName { get; }


    public static string TableName => "{DD6636C4-8929-4683-974E-22C046A43763}";
}

public class AppResourceUseInfo
{
    public AppResourceUseInfo(int id, DateTime timestamp, int appId, int userId, long backgroundBytesRead, long backgroundBytesWritten, long backgroundCycleTime, long faceTime, long foregroundBytesRead,
        long foregroundBytesWritten, long foregroundCycleTime, int backgroundContextSwitches, int backgroundNumberOfFlushes, int backgroundNumReadOperations, int backgroundNumWriteOperations, int foregroundContextSwitches,
        int foregroundNumberOfFlushes, int foregroundNumReadOperations, int foregroundNumWriteOperations)
    {
        Id = id;
        var tsUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        Timestamp = new DateTimeOffset(tsUtc);
        AppId = appId;
        UserId = userId;
        BackgroundBytesRead = backgroundBytesRead;
        BackgroundBytesWritten = backgroundBytesWritten;
        BackgroundCycleTime = backgroundCycleTime;
        FaceTime = faceTime;
        ForegroundBytesRead = foregroundBytesRead;
        ForegroundBytesWritten = foregroundBytesWritten;
        ForegroundCycleTime = foregroundCycleTime;
        BackgroundContextSwitches = backgroundContextSwitches;
        BackgroundNumberOfFlushes = backgroundNumberOfFlushes;
        BackgroundNumReadOperations = backgroundNumReadOperations;
        BackgroundNumWriteOperations = backgroundNumWriteOperations;
        ForegroundContextSwitches = foregroundContextSwitches;
        ForegroundNumberOfFlushes = foregroundNumberOfFlushes;
        ForegroundNumReadOperations = foregroundNumReadOperations;
        ForegroundNumWriteOperations = foregroundNumWriteOperations;
    }

    public int Id { get; }
    public DateTimeOffset Timestamp { get; }

    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }

    public int UserId { get; }
    public int AppId { get; }

    public long BackgroundBytesRead { get; }
    public long BackgroundBytesWritten { get; }

    public int BackgroundContextSwitches { get; }
    public long BackgroundCycleTime { get; }
    public int BackgroundNumberOfFlushes { get; }
    public int BackgroundNumReadOperations { get; }
    public int BackgroundNumWriteOperations { get; }
    public long FaceTime { get; }
    public long ForegroundBytesRead { get; }
    public long ForegroundBytesWritten { get; }
    public int ForegroundContextSwitches { get; }
    public long ForegroundCycleTime { get; }
    public int ForegroundNumberOfFlushes { get; }
    public int ForegroundNumReadOperations { get; }

    public int ForegroundNumWriteOperations { get; }


    public static string TableName => "{D10CA2FE-6FCF-4F6D-848E-B2E99266FA89}";
}

public class Vfuprov
{
    public Vfuprov(int id, DateTime timestamp, int userId, int appId, int flags, long startTime, long endTime)
    {
        Id = id;
        var tsUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        Timestamp = new DateTimeOffset(tsUtc);
        UserId = userId;
        AppId = appId;
        Flags = flags;

        StartTime = SrumTime.FromFileTime(startTime);
        EndTime = SrumTime.FromFileTime(endTime);
    }

    public static string TableName => "{7ACBBAA3-D029-4BE4-9A7A-0885927F1D8F}";


    public int Id { get; }
    public DateTimeOffset Timestamp { get; }
    public int UserId { get; }
    public int AppId { get; }

    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }


    public DateTimeOffset StartTime { get; }

    public DateTimeOffset EndTime { get; }

    public int Flags { get; }

    public TimeSpan Duration => EndTime.Subtract(StartTime);
}

public class TimelineProvider
{
    public TimelineProvider(int id, DateTime timestamp, int userId, int appId, int durationMs, long endTime)
    {
        Id = id;
        UserId = userId;
        AppId = appId;
        DurationMs = durationMs;
        var tsUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        Timestamp = new DateTimeOffset(tsUtc);

        EndTime = SrumTime.FromFileTime(endTime);
    }
    // TABLE: {5C8CF1C7-7257-4F13-B223-970EF5939312}
    // AppId: Long
    // AudioInS: Long
    // AudioInTimeline: 15
    // AudioOutS: Long
    // AudioOutTimeline: 15
    // AutoIncId: Long
    // CompDirtiedS: Long
    // CompDirtiedTimeline: 15
    // CompPropagatedS: Long
    // CompPropagatedTimeline: 15
    // CompRenderedS: Long
    // CompRenderedTimeline: 15
    // CpuTimeline: 15
    // Cycles: 15
    // CyclesAttr: 15
    // CyclesAttrBreakdown: 15
    // CyclesBreakdown: 15
    // CyclesWOB: 15
    // CyclesWOBBreakdown: 15
    // DiskRaw: 15
    // DiskTimeline: 15
    // DisplayRequiredS: Long
    // DisplayRequiredTimeline: 15
    // DurationMS: Long
    // EndTime: 15
    // Flags: Long
    // InFocusS: Long
    // InFocusTimeline: 15
    // KeyboardInputS: Long
    // KeyboardInputTimeline: 15
    // MBBBytesRaw: 15
    // MBBTailRaw: 15
    // MBBTimeline: 15
    // MouseInputS: Long
    // NetworkBytesRaw: 15
    // NetworkTailRaw: 15
    // NetworkTimeline: 15
    // PSMForegroundS: Long
    // SpanMS: Long
    // TimelineEnd: Long
    //     TimeStamp: DateTime
    //     UserId: Long
    // UserInputS: Long
    // UserInputTimeline: 15

    public static string TableName => "{5C8CF1C7-7257-4F13-B223-970EF5939312}";

    public int Id { get; }
    public DateTimeOffset Timestamp { get; }

    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }

    public int UserId { get; }
    public int AppId { get; }

    public DateTimeOffset EndTime { get; }

    public int DurationMs { get; }

    //seconds in each state during the span (Issue #9; after the columns above, so the CSV keeps its column order).
    //Empty when the table has no such column (older Windows versions)

    public int? InFocusS { get; internal set; }
    public int? PsmForegroundS { get; internal set; }
    public int? UserInputS { get; internal set; }
    public int? KeyboardInputS { get; internal set; }
    public int? MouseInputS { get; internal set; }
    public int? DisplayRequiredS { get; internal set; }
    public int? AudioInS { get; internal set; }
    public int? AudioOutS { get; internal set; }
}

public class EnergyUsage
{
    public EnergyUsage(int id, bool isLt, DateTimeOffset timestamp, int userId, int appId, long configurationHash, long eventTimestamp, long stateTransition, int chargeLevel, int cycleCount, int designedCapacity, int fullChargedCapacity, int activeAcTime, int activeDcTime, int activeDischargeTime, int activeEnergy, int csAcTime, int csDcTime, int csDischargeTime, int csEnergy)
    {
        Id = id;
        IsLt = isLt;
        Timestamp = timestamp;
        UserId = userId;
        AppId = appId;
        ConfigurationHash = configurationHash;

        if (eventTimestamp > 0)
        {
            EventTimestamp = SrumTime.FromFileTime(eventTimestamp);
        }


        StateTransition = stateTransition;
        ChargeLevel = chargeLevel;
        CycleCount = cycleCount;
        DesignedCapacity = designedCapacity;
        FullChargedCapacity = fullChargedCapacity;
        ActiveAcTime = activeAcTime;
        ActiveDcTime = activeDcTime;
        ActiveDischargeTime = activeDischargeTime;
        ActiveEnergy = activeEnergy;
        CsAcTime = csAcTime;
        CsDcTime = csDcTime;
        CsDischargeTime = csDischargeTime;
        CsEnergy = csEnergy;
    }

    public static string TableName => "{FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}_{FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}LT";


    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; }
    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }

    public int UserId { get; }
    public int AppId { get; }
    public bool IsLt { get; }


    public long ConfigurationHash { get; }
    public DateTimeOffset? EventTimestamp { get; }
    public long StateTransition { get; }

    public int ChargeLevel { get; }
    public int CycleCount { get; }
    public int DesignedCapacity { get; }
    public int FullChargedCapacity { get; }
    public int ActiveAcTime { get; }
    public int ActiveDcTime { get; }
    public int ActiveDischargeTime { get; }
    public int ActiveEnergy { get; }
    public int CsAcTime { get; }
    public int CsDcTime { get; }
    public int CsDischargeTime { get; }
    public int CsEnergy { get; }
}

/// <summary>
///     The columns every table of the newer providers has (Issue #15: seen on Server 2022). What the providers measure is
///     not documented; the classes are named after their columns
/// </summary>
public abstract class SrumRecord
{
    protected SrumRecord(int id, DateTime timestamp, int userId, int appId)
    {
        Id = id;
        Timestamp = new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
        UserId = userId;
        AppId = appId;
    }

    public int Id { get; }
    public DateTimeOffset Timestamp { get; }

    public string ExeInfo { get; internal set; }
    public string ExeInfoDescription { get; internal set; }

    public DateTimeOffset? ExeTimestamp { get; internal set; }

    public SidTypeEnum SidType { get; internal set; }
    public string Sid { get; internal set; }
    public string UserName { get; internal set; }

    public int UserId { get; }
    public int AppId { get; }
}

/// <summary>
///     {DC3D3B50-BB90-5066-FA4E-A5F90DD8B677}: ProcessorTime
/// </summary>
public class ProcessorTimeInfo : SrumRecord
{
    public ProcessorTimeInfo(int id, DateTime timestamp, int userId, int appId, long processorTime) : base(id, timestamp, userId, appId)
    {
        ProcessorTime = processorTime;
    }

    public long ProcessorTime { get; }

    public static string TableName => "{DC3D3B50-BB90-5066-FA4E-A5F90DD8B677}";
}

/// <summary>
///     {841A7317-3805-518B-C2EA-AD224CB4AF84}: SizeInBytes
/// </summary>
public class SizeInBytesInfo : SrumRecord
{
    public SizeInBytesInfo(int id, DateTime timestamp, int userId, int appId, long sizeInBytes) : base(id, timestamp, userId, appId)
    {
        SizeInBytes = sizeInBytes;
    }

    public long SizeInBytes { get; }

    public static string TableName => "{841A7317-3805-518B-C2EA-AD224CB4AF84}";
}

/// <summary>
///     {17F4D97B-F26A-5E79-3A82-90040A47D13D}: Total and Used
/// </summary>
public class TotalUsedInfo : SrumRecord
{
    public TotalUsedInfo(int id, DateTime timestamp, int userId, int appId, long total, long used) : base(id, timestamp, userId, appId)
    {
        Total = total;
        Used = used;
    }

    public long Total { get; }
    public long Used { get; }

    public static string TableName => "{17F4D97B-F26A-5E79-3A82-90040A47D13D}";
}

/// <summary>
///     {EEE2F477-0659-5C47-EF03-6D6BEFD441B3}: BytesInBound, BytesOutBound and BytesTotal
/// </summary>
public class NetworkBytesInfo : SrumRecord
{
    public NetworkBytesInfo(int id, DateTime timestamp, int userId, int appId, long bytesInBound, long bytesOutBound, long bytesTotal) : base(id, timestamp, userId, appId)
    {
        BytesInBound = bytesInBound;
        BytesOutBound = bytesOutBound;
        BytesTotal = bytesTotal;
    }

    public long BytesInBound { get; }
    public long BytesOutBound { get; }
    public long BytesTotal { get; }

    public static string TableName => "{EEE2F477-0659-5C47-EF03-6D6BEFD441B3}";
}

public enum InterfaceType
{
    IF_TYPE_CES = 133,
    IF_TYPE_COFFEE = 132,
    IF_TYPE_TUNNEL = 131,
    IF_TYPE_A12MPPSWITCH = 130,
    IF_TYPE_L3_IPXVLAN = 137,
    IF_TYPE_L3_IPVLAN = 136,
    IF_TYPE_L2_VLAN = 135,
    IF_TYPE_ATM_SUBINTERFACE = 134,
    IF_TYPE_MEDIAMAILOVERIP = 139,
    IF_TYPE_DIGITALPOWERLINE = 138,
    IF_TYPE_SOFTWARE_LOOPBACK = 24,
    IF_TYPE_EON = 25,
    IF_TYPE_ETHERNET_3MBIT = 26,
    IF_TYPE_NSIP = 27,
    IF_TYPE_BASIC_ISDN = 20,
    IF_TYPE_PRIMARY_ISDN = 21,
    IF_TYPE_PROP_POINT2POINT_SERIAL = 22,
    IF_TYPE_PPP = 23,
    IF_TYPE_SLIP = 28,
    IF_TYPE_ULTRA = 29,
    IF_TYPE_DDN_X25 = 4,
    IF_TYPE_ISO88024_TOKENBUS = 8,
    IF_TYPE_LAP_F = 119,
    IF_TYPE_V37 = 120,
    IF_TYPE_X25_MLP = 121,
    IF_TYPE_X25_HUNTGROUP = 122,
    IF_TYPE_TRANSPHDLC = 123,
    IF_TYPE_INTERLEAVE = 124,
    IF_TYPE_FAST = 125,
    IF_TYPE_IP = 126,
    IF_TYPE_DOCSCABLE_MACLAYER = 127,
    IF_TYPE_DOCSCABLE_DOWNSTREAM = 128,
    IF_TYPE_DOCSCABLE_UPSTREAM = 129,
    IF_TYPE_HDLC = 118,
    IF_TYPE_AFLANE_8023 = 59,
    IF_TYPE_FRAMERELAY_INTERCONNECT = 58,
    IF_TYPE_IEEE80212 = 55,
    IF_TYPE_PROP_MULTIPLEXOR = 54,
    IF_TYPE_HIPPIINTERFACE = 57,
    IF_TYPE_FIBRECHANNEL = 56,
    IF_TYPE_SONET_VT = 51,
    IF_TYPE_SONET_PATH = 50,
    IF_TYPE_PROP_VIRTUAL = 53,
    IF_TYPE_SMDS_ICIP = 52,
    IF_TYPE_ISO88025_FIBER = 115,
    IF_TYPE_IPOVER_ATM = 114,
    IF_TYPE_ARAP = 88,
    IF_TYPE_PROP_CNLS = 89,
    IF_TYPE_STACKTOSTACK = 111,
    IF_TYPE_IPOVER_CLAW = 110,
    IF_TYPE_MPC = 113,
    IF_TYPE_VIRTUALIPADDRESS = 112,
    IF_TYPE_DS0_BUNDLE = 82,
    IF_TYPE_BSC = 83,
    IF_TYPE_ATM_LOGICAL = 80,
    IF_TYPE_DS0 = 81,
    IF_TYPE_ISO88025R_DTR = 86,
    IF_TYPE_EPLRS = 87,
    IF_TYPE_ASYNC = 84,
    IF_TYPE_CNR = 85,
    IF_TYPE_HDH_1822 = 3,
    IF_TYPE_IS088023_CSMACD = 7,
    IF_TYPE_PPPMULTILINKBUNDLE = 108,
    IF_TYPE_IPOVER_CDLC = 109,
    IF_TYPE_VOICE_FXS = 102,
    IF_TYPE_VOICE_ENCAP = 103,
    IF_TYPE_VOICE_EM = 100,
    IF_TYPE_VOICE_FXO = 101,
    IF_TYPE_ATM_FUNI = 106,
    IF_TYPE_ATM_IMA = 107,
    IF_TYPE_VOICE_OVERIP = 104,
    IF_TYPE_ATM_DXI = 105,
    IF_TYPE_SONET = 39,
    IF_TYPE_MIO_X25 = 38,
    IF_TYPE_RS232 = 33,
    IF_TYPE_FRAMERELAY = 32,
    IF_TYPE_SIP = 31,
    IF_TYPE_DS3 = 30,
    IF_TYPE_ATM = 37,
    IF_TYPE_ARCNET_PLUS = 36,
    IF_TYPE_ARCNET = 35,
    IF_TYPE_PARA = 34,
    IF_TYPE_AFLANE_8025 = 60,
    IF_TYPE_CCTEMUL = 61,
    IF_TYPE_FASTETHER = 62,
    IF_TYPE_ISDN = 63,
    IF_TYPE_V11 = 64,
    IF_TYPE_V36 = 65,
    IF_TYPE_G703_64K = 66,
    IF_TYPE_G703_2MB = 67,
    IF_TYPE_QLLC = 68,
    IF_TYPE_FASTETHER_FX = 69,
    IF_TYPE_REGULAR_1822 = 2,
    IF_TYPE_ETHERNET_CSMACD = 6,
    IF_TYPE_MYRINET = 99,
    IF_TYPE_ISO88025_CRFPRINT = 98,
    IF_TYPE_TERMPAD = 91,
    IF_TYPE_HOSTPAD = 90,
    IF_TYPE_X213 = 93,
    IF_TYPE_FRAMERELAY_MPI = 92,
    IF_TYPE_RADSL = 95,
    IF_TYPE_ADSL = 94,
    IF_TYPE_VDSL = 97,
    IF_TYPE_SDSL = 96,
    IF_TYPE_STARLAN = 11,
    IF_TYPE_ISO88026_MAN = 10,
    IF_TYPE_PROTEON_80MBIT = 13,
    IF_TYPE_PROTEON_10MBIT = 12,
    IF_TYPE_FDDI = 15,
    IF_TYPE_HYPERCHANNEL = 14,
    IF_TYPE_SDLC = 17,
    IF_TYPE_LAP_B = 16,
    IF_TYPE_E1 = 19,
    IF_TYPE_DS1 = 18,
    IF_TYPE_GIGABITETHERNET = 117,
    IF_TYPE_TDLC = 116,
    IF_TYPE_MODEM = 48,
    IF_TYPE_AAL5 = 49,
    IF_TYPE_HSSI = 46,
    IF_TYPE_HIPPI = 47,
    IF_TYPE_FRAMERELAY_SERVICE = 44,
    IF_TYPE_V35 = 45,
    IF_TYPE_LOCALTALK = 42,
    IF_TYPE_SMDS_DXI = 43,
    IF_TYPE_X25_PLE = 40,
    IF_TYPE_ISO88022_LLC = 41,
    IF_TYPE_OTHER = 1,
    IF_TYPE_RFC877_X25 = 5,
    IF_TYPE_ISO88025_TOKENRING = 9,
    IF_TYPE_IEEE1394 = 144,
    IF_TYPE_RECEIVE_ONLY = 145,
    IF_TYPE_IPFORWARD = 142,
    IF_TYPE_MSDSL = 143,
    IF_TYPE_DTM = 140,
    IF_TYPE_DCN = 141,
    IF_TYPE_LAP_D = 77,
    IF_TYPE_ISDN_U = 76,
    IF_TYPE_ISDN_S = 75,
    IF_TYPE_DLSW = 74,
    IF_TYPE_ESCON = 73,
    IF_TYPE_IBM370PARCHAN = 72,
    IF_TYPE_IEEE80211 = 71,
    IF_TYPE_CHANNEL = 70,
    IF_TYPE_RSRB = 79,
    IF_TYPE_IPSWITCH = 78
}

public enum SidTypeEnum
{
    [Description("SID does not map to a common SID or this is a user SID")]
    UnknownOrUserSid,

    [Description("S-1-0-0: No Security principal.")]
    Null,

    [Description("S-1-1-0: A group that includes all users.")]
    Everyone,

    [Description("S-1-2-0: A group that includes all users who have logged on locally.")]
    Local,

    [Description(
        "S-1-2-1: A group that includes users who are logged on to the physical console. This SID can be used to implement security policies that grant different rights based on whether a user has been granted physical access to the console."
    )]
    ConsoleLogon,

    [Description(
        "S-1-3-0: A placeholder in an inheritable access control entry (ACE). When the ACE is inherited, the system replaces this SID with the SID for the object's creator."
    )]
    CreatorOwner,

    [Description(
        "S-1-3-1: A placeholder in an inheritable ACE. When the ACE is inherited, the system replaces this SID with the SID for the primary group of the object's creator."
    )]
    CreatorGroup,

    [Description(
        "S-1-3-2: A placeholder in an inheritable ACE. When the ACE is inherited, the system replaces this SID with the SID for the object's owner server."
    )]
    OwnerServer,

    [Description(
        "S-1-3-3: A placeholder in an inheritable ACE. When the ACE is inherited, the system replaces this SID with the SID for the object's group server."
    )]
    GroupServer,

    [Description(
        "S-1-3-4: A group that represents the current owner of the object. When an ACE that carries this SID is applied to an object, the system ignores the implicit READ_CONTROL and WRITE_DAC permissions for the object owner."
    )]
    OwnerRights,

    [Description("S-1-5: A SID containing only the SECURITY_NT_AUTHORITY identifier authority.")]
    NtAuthority,

    [Description(
        "S-1-5-1: A group that includes all users who have logged on through a dial-up connection.")]
    Dialup,

    [Description(
        "S-1-5-2: A group that includes all users who have logged on through a network connection.")]
    Network,

    [Description(
        "S-1-5-3: A group that includes all users who have logged on through a batch queue facility.")]
    Batch,

    [Description("S-1-5-4: A group that includes all users who have logged on interactively.")]
    Interactive,

    [Description(
        "S-1-5-5-x-y: A logon session. The X and Y values for these SIDs are different for each logon session and are recycled when the operating system is restarted."
    )]
    LogonId,

    [Description(
        "S-1-5-6: A group that includes all security principals that have logged on as a service.")]
    Service,

    [Description("S-1-5-7: A group that represents an anonymous logon.")]
    Anonymous,

    [Description("S-1-5-8: Identifies a SECURITY_NT_AUTHORITY Proxy.")]
    Proxy,

    [Description(
        "S-1-5-9: A group that includes all domain controllers in a forest that uses an Active Directory directory service."
    )]
    EnterpriseDomainControllers,

    [Description(
        "S-1-5-10: A placeholder in an inheritable ACE on an account object or group object in Active Directory. When the ACE is inherited, the system replaces this SID with the SID for the security principal that holds the account."
    )]
    PrincipalSelf,

    [Description(
        "S-1-5-11: A group that includes all users whose identities were authenticated when they logged on.")]
    AuthenticatedUsers,

    [Description(
        "S-1-5-12: This SID is used to control access by untrusted code. ACL validation against tokens with RC consists of two checks, one against the token's normal list of SIDs and one against a second list (typically containing RC - the RESTRICTED_CODE token - and a subset of the original token SIDs). Access is granted only if a token passes both tests. Any ACL that specifies RC must also specify WD - the EVERYONE token. When RC is paired with WD in an ACL, a superset of EVERYONE, including untrusted code, is described."
    )]
    RestrictedCode,

    [Description(
        "S-1-5-13: A group that includes all users who have logged on to a Terminal Services server.")]
    TerminalServerUser,

    [Description(
        "S-1-5-14: A group that includes all users who have logged on through a terminal services logon.")]
    RemoteInteractiveLogon,

    [Description("S-1-5-15: A group that includes all users from the same organization.")]
    ThisOrganization,

    [Description(
        "S-1-5-1000: A group that includes all users and computers from another organization. ")]
    OtherOrganization,

    [Description(
        "S-1-5-17: An account that is used by the default Internet Information Services (IIS) user.")]
    Iusr,

    [Description("S-1-5-18: An account that is used by the operating system.")]
    LocalSystem,

    [Description("S-1-5-19: A local service account.")]
    LocalService,

    [Description("S-1-5-20: A network service account.")]
    NetworkService,

    [Description(
        "S-1-5-21-<root domain>-498: A universal group containing all read-only domain controllers in a forest."
    )]
    EnterpriseReadonlyDomainControllers,

    [Description(
        "S-1-5-21-0-0-0-496: Device identity is included in the Kerberos service ticket. If a forest boundary was crossed, then claims transformation occurred."
    )]
    CompoundedAuthentication,

    [Description(
        "S-1-5-21-0-0-0-497: Claims were queried for in the account's domain, and if a forest boundary was crossed, then claims transformation occurred."
    )]
    ClaimsValid,

    [Description(
        "S-1-5-21-<machine>-500: A user account for the system administrator. By default, it is the only user account that is given full control over the system."
    )]
    Administrator,

    [Description(
        "S-1-5-21-<machine>-501: A user account for people who do not have individual accounts. This user account does not require a password. By default, the Guest account is disabled."
    )]
    Guest,

    [Description(
        "S-1-5-21-<domain>-512: A global group whose members are authorized to administer the domain. By default, the DOMAIN_ADMINS group is a member of the Administrators group on all computers that have joined a domain, including the domain controllers. DOMAIN_ADMINS is the default owner of any object that is created by any member of the group."
    )]
    DomainAdmins,

    [Description(
        "S-1-5-21-<domain>-513: A global group that includes all user accounts in a domain.")]
    DomainUsers,

    [Description(
        "S-1-5-21-<domain>-514: A global group that has only one member, which is the built-in Guest account of the domain."
    )]
    DomainGuests,

    [Description(
        "S-1-5-21-<domain>-515: A global group that includes all clients and servers that have joined the domain."
    )]
    DomainComputers,

    [Description(
        "S-1-5-21-<domain>-516: A global group that includes all domain controllers in the domain.")]
    DomainDomainControllers,

    [Description(
        "S-1-5-21-<domain>-517: A global group that includes all computers that are running an enterprise certification authority. Cert Publishers are authorized to publish certificates for User objects in Active Directory."
    )]
    CertPublishers,

    [Description(
        "S-1-5-21-<root-domain>-518: A universal group in a native-mode domain, or a global group in a mixed-mode domain. The group is authorized to make schema changes in Active Directory."
    )]
    SchemaAdministrators,

    [Description(
        "S-1-5-21-<root-domain>-519: A universal group in a native-mode domain, or a global group in a mixed-mode domain. The group is authorized to make forestwide changes in Active Directory, such as adding child domains."
    )]
    EnterpriseAdmins,

    [Description(
        "S-1-5-21-<domain>-520: A global group that is authorized to create new Group Policy Objects in Active Directory."
    )]
    GroupPolicyCreatorOwners,

    [Description(
        "S-1-5-21-<domain>-521: A global group that includes all read-only domain controllers.")]
    ReadonlyDomainControllers,

    [Description(
        "S-1-5-21-<domain>-522: A global group that includes all domain controllers in the domain that may be cloned."
    )]
    CloneableControllers,

    [Description(
        "S-1-5-21-<domain>-525: A global group that are afforded additional protections against authentication security threats. For more information, see [MS-APDS] and [MS-KILE]."
    )]
    ProtectedUsers,

    [Description(
        "S-1-5-21-<domain>-553: A domain local group for Remote Access Services (RAS) servers. Servers in this group have Read Account Restrictions and Read Logon Information access to User objects in the Active Directory domain local group."
    )]
    RasServers,

    [Description(
        "S-1-5-32-544: A built-in group. After the initial installation of the operating system, the only member of the group is the Administrator account. When a computer joins a domain, the Domain Administrators group is added to the Administrators group. When a server becomes a domain controller, the Enterprise Administrators group also is added to the Administrators group."
    )]
    BuiltinAdministrators,

    [Description(
        "S-1-5-32-545: A built-in group. After the initial installation of the operating system, the only member is the Authenticated Users group. When a computer joins a domain, the Domain Users group is added to the Users group on the computer."
    )]
    BuiltinUsers,

    [Description(
        "S-1-5-32-546: A built-in group. The Guests group allows users to log on with limited privileges to a computer's built-in Guest account."
    )]
    BuiltinGuests,

    [Description(
        "S-1-5-32-547: A built-in group. Power users can perform the following actions: Create local users and groups, Modify and delete accounts that they have created, Remove users from the Power Users, Users, and Guests groups, Install programs, Create, manage, and delete local printers, Create and delete file shares."
    )]
    PowerUsers,

    [Description(
        "S-1-5-32-548: A built-in group that exists only on domain controllers. Account Operators have permission to create, modify, and delete accounts for users, groups, and computers in all containers and organizational units of Active Directory except the Built-in container and the Domain Controllers OU. Account Operators do not have permission to modify the Administrators and Domain Administrators groups, nor do they have permission to modify the accounts for members of those groups."
    )]
    AccountOperators,

    [Description(
        "S-1-5-32-549: A built-in group that exists only on domain controllers. Server Operators can perform the following actions: Log on to a server interactively, Create and delete network shares, Start and stop services, Back up and restore files, Format the hard disk of a computer, Shut down the computer"
    )]
    ServerOperators,

    [Description(
        "S-1-5-32-550: A built-in group that exists only on domain controllers. Print Operators can manage printers and document queues."
    )]
    PrinterOperators,

    [Description(
        "S-1-5-32-551: A built-in group. Backup Operators can back up and restore all files on a computer, regardless of the permissions that protect those files."
    )]
    BackupOperators,

    [Description(
        "S-1-5-32-552: A built-in group that is used by the File Replication Service (FRS) on domain controllers."
    )]
    Replicator,

    [Description(
        "S-1-5-32-554: A backward compatibility group that allows read access on all users and groups in the domain."
    )]
    AliasPrew2Kcompacc,

    [Description(
        "S-1-5-32-555: An alias. Members of this group are granted the right to log on remotely.")]
    RemoteDesktop,

    [Description(
        "S-1-5-32-556: An alias. Members of this group can have some administrative privileges to manage configuration of networking features."
    )]
    NetworkConfigurationOps,

    [Description(
        "S-1-5-32-557: An alias. Members of this group can create incoming, one-way trusts to this forest.")]
    IncomingForestTrustBuilders,

    [Description(
        "S-1-5-32-558: An alias. Members of this group have remote access to monitor this computer.")]
    PerfmonUsers,

    [Description(
        "S-1-5-32-559: An alias. Members of this group have remote access to schedule the logging of performance counters on this computer."
    )]
    PerflogUsers,

    [Description(
        "S-1-5-32-560: An alias. Members of this group have access to the computed tokenGroupsGlobalAndUniversal attribute on User objects."
    )]
    WindowsAuthorizationAccessGroup,

    [Description(
        "S-1-5-32-561: An alias. A group for Terminal Server License Servers.")]
    TerminalServerLicenseServers,

    [Description(
        "S-1-5-32-562: An alias. A group for COM to provide computer-wide access controls that govern access to all call, activation, or launch requests on the computer."
    )]
    DistributedComUsers,

    [Description("S-1-5-32-568: A built-in group account for IIS users.")]
    IisIusrs,

    [Description("S-1-5-32-569: A built-in group account for cryptographic operators.")]
    CryptographicOperators,

    [Description(
        "S-1-5-32-573: A built-in local group. Members of this group can read event logs from the local machine."
    )]
    EventLogReaders,

    [Description(
        "S-1-5-32-574: A built-in local group. Members of this group are allowed to connect to Certification Authorities in the enterprise."
    )]
    CertificateServiceDcomAccess,

    [Description(
        "S-1-5-32-575: A group that allows members use of Remote Application Services resources.")]
    RdsRemoteAccessServers,

    [Description("S-1-5-32-576: A group that enables member servers to run virtual machines and host sessions.")
    ]
    RdsEndpointServers,

    [Description(
        "S-1-5-32-577: A group that allows members to access WMI resources over management protocols (such as WS-Management via the Windows Remote Management service)."
    )]
    RdsManagementServers,

    [Description(
        "S-1-5-32-578: A group that gives members access to all administrative features of Hyper-V.")]
    HyperVAdmins,

    [Description(
        "S-1-5-32-579: A local group that allows members to remotely query authorization attributes and permissions for resources on the local computer."
    )]
    AccessControlAssistanceOps,

    [Description(
        "S-1-5-32-580: Members of this group can access Windows Management Instrumentation (WMI) resources over management protocols (such as WS-Management [DMTF-DSP0226]). This applies only to WMI namespaces that grant access to the user."
    )]
    RemoteManagementUsers,

    [Description(
        "S-1-5-33: A SID that allows objects to have an ACL that lets any service process with a write-restricted token to write to the object."
    )]
    WriteRestrictedCode,

    [Description(
        "S-1-5-64-10: A SID that is used when the NTLM authentication package authenticated the client.")]
    NtlmAuthentication,

    [Description(
        "S-1-5-64-14: A SID that is used when the SChannel authentication package authenticated the client.")]
    SchannelAuthentication,

    [Description(
        "S-1-5-64-21: A SID that is used when the Digest authentication package authenticated the client.")]
    DigestAuthentication,

    [Description(
        "S-1-5-65-1: A SID that indicates that the client's Kerberos service ticket's PAC contained a NTLM_SUPPLEMENTAL_CREDENTIAL structure (as specified in [MS-PAC] section 2.6.4)."
    )]
    ThisOrganizationCertificate,

    [Description("S-1-5-80: An NT Service account prefix.")]
    NtService,

    [Description("S-1-5-84-0-0-0-0-0: Identifies a user-mode driver process.")]
    UserModeDrivers,

    [Description("S-1-5-113: A group that includes all users who are local accounts.")]
    LocalAccount,

    [Description(
        "S-1-5-114: A group that includes all users who are local accounts and members of the administrators group."
    )]
    LocalAccountAndMemberOfAdministratorsGroup,

    [Description("S-1-15-2-1: All applications running in an app package context.")]
    AllAppPackages,

    [Description("S-1-16-0: An untrusted integrity level.")]
    MlUntrusted,

    [Description("S-1-16-4096: A low integrity level.")]
    MlLow,

    [Description("S-1-16-8192: A medium integrity level.")]
    MlMedium,

    [Description("S-1-16-8448: A medium-plus integrity level.")]
    MlMediumPlus,

    [Description("S-1-16-12288: A high integrity level.")]
    MlHigh,

    [Description("S-1-16-16384: A system integrity level.")]
    MlSystem,

    [Description("S-1-16-20480: A protected-process integrity level.")]
    MlProtectedProcess,

    [Description(
        "S-1-18-1: A SID that means the client's identity is asserted by an authentication authority based on proof of possession of client credentials."
    )]
    AuthenticationAuthorityAssertedIdentity,

    [Description(
        "S-1-18-2: A SID that means the client's identity is asserted by a service.")]
    ServiceAssertedIdentity
}

//https://docs.microsoft.com/en-us/windows/win32/extensible-storage-engine/jet-coltyp

public class Srum
{
    public readonly Dictionary<int, AppInfo> AppMaps;


    public readonly Dictionary<int, AppResourceUseInfo> AppResourceUseInfos;
    public readonly Dictionary<int, EnergyUsage> EnergyUsages;
    public readonly Dictionary<int, NetworkConnection> NetworkConnections;
    public readonly Dictionary<int, NetworkUsage> NetworkUsages;

    /// <summary>
    ///     Maps L2ProfileId to Network Profile Names (SSIDs)
    /// </summary>
    public readonly Dictionary<int, string> ProfileIndexToSsid;

    public readonly Dictionary<int, PushNotification> PushNotifications;

    /// <summary>
    ///     Maps SIDs to usernames
    /// </summary>
    public readonly Dictionary<string, string> SidToUser;

    public readonly Dictionary<int, TimelineProvider> TimelineProviders;
    public readonly Dictionary<int, Vfuprov> Vfuprovs;
    public readonly Dictionary<int, UserInfo> UserMaps;

    //the newer providers' tables (Issue #15)
    public readonly Dictionary<int, ProcessorTimeInfo> ProcessorTimes;
    public readonly Dictionary<int, SizeInBytesInfo> SizeInBytesInfos;
    public readonly Dictionary<int, TotalUsedInfo> TotalUsedInfos;
    public readonly Dictionary<int, NetworkBytesInfo> NetworkBytesInfos;

    /// <summary>
    ///     Tables, rows and values that could not be read (everything else was kept)
    /// </summary>
    public List<string> ParseErrors { get; } = new();

    /// <summary>
    ///     What was done to read the database (logs not replayed, a repaired copy) and rows whose app or user id is not in
    ///     SruDbIdMapTable
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    ///     Reads a SRUM database (and, optionally, user names and Wi-Fi profile names from a SOFTWARE hive). The database
    ///     is read from a copy in a temporary folder, with its SRU*.log files replayed (see <see cref="EseWorkspace" />),
    ///     so nothing next to <paramref name="fileName" /> changes. A table or row that can't be read is reported in
    ///     <see cref="ParseErrors" /> and the rest is kept
    /// </summary>
    /// <exception cref="FileNotFoundException">A file does not exist</exception>
    /// <exception cref="Exception">The database can't be opened, even from a repaired copy</exception>
    public Srum(string fileName, string softwareHive) : this(fileName, softwareHive, null)
    {
    }

    /// <summary>
    ///     As <see cref="Srum(string, string)" />, with the copy made in a new folder in <paramref name="tempDirectory" />
    ///     (which must exist; null: the system's temporary folder), deleted when done
    /// </summary>
    /// <exception cref="FileNotFoundException">A file does not exist</exception>
    /// <exception cref="Exception">The database can't be opened, even from a repaired copy</exception>
    public Srum(string fileName, string softwareHive, string tempDirectory)
    {
        if (File.Exists(fileName) == false)
        {
            throw new FileNotFoundException($"{fileName} does not exist!");
        }

        SidToUser = new Dictionary<string, string>();
        ProfileIndexToSsid = new Dictionary<int, string>();

        AppMaps = new Dictionary<int, AppInfo>();
        UserMaps = new Dictionary<int, UserInfo>();
        NetworkUsages = new Dictionary<int, NetworkUsage>();
        AppResourceUseInfos = new Dictionary<int, AppResourceUseInfo>();
        NetworkConnections = new Dictionary<int, NetworkConnection>();
        PushNotifications = new Dictionary<int, PushNotification>();
        EnergyUsages = new Dictionary<int, EnergyUsage>();
        Vfuprovs = new Dictionary<int, Vfuprov>();
        TimelineProviders = new Dictionary<int, TimelineProvider>();
        ProcessorTimes = new Dictionary<int, ProcessorTimeInfo>();
        SizeInBytesInfos = new Dictionary<int, SizeInBytesInfo>();
        TotalUsedInfos = new Dictionary<int, TotalUsedInfo>();
        NetworkBytesInfos = new Dictionary<int, NetworkBytesInfo>();

        if (softwareHive != null)
        {
            if (File.Exists(softwareHive) == false)
            {
                throw new FileNotFoundException($"{softwareHive} does not exist!");
            }

            Log.Debug("Processing {Hive} Registry hive", "SOFTWARE");

            //(any problem in the hive ended everything)
            try
            {
                ReadSoftwareHive(softwareHive);
            }
            catch (Exception e)
            {
                AddError($"SOFTWARE hive '{softwareHive}': {e.Message}. User and network profile names may be missing");
            }

            Log.Debug("{SidToUser}", SidToUser.Dump());
            Log.Debug("{ProfileIndexToSsid}", ProfileIndexToSsid.Dump());
        }

        var full = Path.GetFullPath(fileName);

        Log.Debug("Copying '{FileName}' and its logs to a temporary folder", full);
        using var ws = EseWorkspace.Create(Path.GetDirectoryName(full), new[] {full}, "SRU", AddWarning, tempDirectory);

        var copy = Path.Combine(ws.WorkDirectory, Path.GetFileName(full));
        var dbid = ws.OpenDatabase(copy);
        var session = ws.Session;

        Log.Debug("Building ID Maps");
        BuildIdMap(session, dbid);

        Log.Debug("Getting {Thing}", "NetworkUsageInfo");
        GetNetworkUsageInfo(session, dbid);

        Log.Debug("Getting {Thing}", "ApplicationResourceUsage");
        GetApplicationResourceUsage(session, dbid);

        Log.Debug("Getting {Thing}", "NetworkConnections");
        GetNetworkConnections(session, dbid);

        Log.Debug("Getting {Thing}", "PushNotifications");
        GetPushNotifications(session, dbid);

        Log.Debug("Getting {Thing}", "EnergyUsages (and LT)");
        GetEnergyUsages(session, dbid);

        Log.Debug("Getting {Thing}", "vfuprov");
        GetVfuprovs(session, dbid);

        Log.Debug("Getting {Thing}", "Timeline Provider");
        GetAppTimelines(session, dbid);

        Log.Debug("Getting {Thing}", "Server 2022 providers");
        GetNewerProviders(session, dbid);

        ws.CloseDatabase(copy, dbid);
    }

    private void AddError(string message)
    {
        ParseErrors.Add(message);
        Log.Warning("{Message}", message);
    }

    private void AddWarning(string message)
    {
        Warnings.Add(message);
        Log.Warning("{Message}", message);
    }

    private void ReadSoftwareHive(string softwareHive)
    {
        var reg = new RegistryHiveOnDemand(softwareHive);
        var k = reg.GetKey(@"Microsoft\Windows NT\CurrentVersion\ProfileList");

        if (k != null)
        {
            foreach (var registryKey in k.SubKeys)
            {
                try
                {
                    var kd = reg.GetKey(registryKey.KeyPath);

                    var v = kd?.Values.FirstOrDefault(t => t.ValueName == "ProfileImagePath");
                    if (v != null)
                    {
                        SidToUser[registryKey.KeyName] = Path.GetFileName(v.ValueData);
                    }
                }
                catch (Exception e)
                {
                    AddError($"SOFTWARE hive: profile '{registryKey.KeyName}': {e.Message}");
                }
            }
        }

        //get network IDs and profileIndexes

        k = reg.GetKey(@"Microsoft\WlanSvc\Interfaces");

        if (k == null)
        {
            return;
        }

        foreach (var registryKey in k.SubKeys)
        {
            var sk = reg.GetKey(registryKey.KeyPath);
            if (sk == null || sk.SubKeys.All(t => t.KeyName != "Profiles"))
            {
                continue;
            }

            var profileKey = reg.GetKey($"{sk.KeyPath}\\Profiles");

            if (profileKey == null)
            {
                continue;
            }

            foreach (var pk in profileKey.SubKeys)
            {
                //(one profile without a ProfileIndex, or with a short Channel Hints value, ended everything)
                try
                {
                    var pkKey = reg.GetKey(pk.KeyPath);
                    var profileIndex = pkKey?.Values.FirstOrDefault(t => t.ValueName == "ProfileIndex");
                    if (profileIndex == null || pkKey.SubKeys.All(t => t.KeyName != "MetaData"))
                    {
                        continue;
                    }

                    var meta = reg.GetKey($"{pk.KeyPath}\\MetaData");
                    var chanHints = meta?.Values.FirstOrDefault(t => t.ValueName == "Channel Hints");

                    var raw = chanHints?.ValueDataRaw;
                    if (raw == null || raw.Length < 4)
                    {
                        continue;
                    }

                    var len = BitConverter.ToInt32(raw, 0);
                    if (len < 0 || len > raw.Length - 4)
                    {
                        throw new InvalidDataException($"Channel Hints says the name has {len} bytes, but the value has {raw.Length}");
                    }

                    var nwName = Encoding.ASCII.GetString(raw, 4, len);

                    if (!int.TryParse(profileIndex.ValueData, out var index))
                    {
                        throw new InvalidDataException($"ProfileIndex '{profileIndex.ValueData}' is not a number");
                    }

                    if (!ProfileIndexToSsid.ContainsKey(index))
                    {
                        ProfileIndexToSsid.Add(index, nwName);
                    }
                }
                catch (Exception e)
                {
                    AddError($"SOFTWARE hive: Wi-Fi profile '{pk.KeyPath}': {e.Message}");
                }
            }
        }
    }

    /// <summary>
    ///     The values of the current row of a table, by column name (looked up once per table: they were looked up for
    ///     every value). A missing column or a null value gives 0
    /// </summary>
    private sealed class Row
    {
        private readonly IDictionary<string, JET_COLUMNID> _cols;
        private readonly Srum _owner;
        private readonly JET_SESID _session;
        private readonly JET_TABLEID _table;
        private readonly string _tableName;

        public Row(Srum owner, JET_SESID session, JET_TABLEID table, IDictionary<string, JET_COLUMNID> cols, string tableName)
        {
            _owner = owner;
            _session = session;
            _table = table;
            _cols = cols;
            _tableName = tableName;
        }

        public bool Has(string name) => _cols.ContainsKey(name);

        public int Int(string name) => IntOrNull(name) ?? 0;

        public int? IntOrNull(string name)
        {
            return _cols.TryGetValue(name, out var c) ? Api.RetrieveColumnAsInt32(_session, _table, c) : null;
        }

        public long Long(string name)
        {
            return _cols.TryGetValue(name, out var c) ? Api.RetrieveColumnAsInt64(_session, _table, c) ?? 0 : 0;
        }

        public byte? Byte(string name)
        {
            return _cols.TryGetValue(name, out var c) ? Api.RetrieveColumnAsByte(_session, _table, c) : null;
        }

        public byte[] Bytes(string name)
        {
            return _cols.TryGetValue(name, out var c) ? Api.RetrieveColumn(_session, _table, c) : null;
        }

        /// <summary>
        ///     An ESE DateTime (OLE date) as UTC; DateTime.MinValue (reported) when missing or invalid
        /// </summary>
        public DateTime Time(string name)
        {
            if (!_cols.TryGetValue(name, out var c))
            {
                return DateTime.MinValue;
            }

            try
            {
                return Api.RetrieveColumnAsDateTime(_session, _table, c) ?? DateTime.MinValue;
            }
            catch (Exception e)
            {
                _owner.AddError($"{_tableName}: {name} is not a valid time ({e.Message})");
                return DateTime.MinValue;
            }
        }

        /// <summary>
        ///     A FILETIME, as stored; an out of range one is reported (it becomes DateTimeOffset.MinValue)
        /// </summary>
        public long FileTime(string name)
        {
            var raw = Long(name);
            if (!SrumTime.IsValid(raw))
            {
                _owner.AddError($"{_tableName}: {name} 0x{raw:X} is not a valid time");
            }

            return raw;
        }
    }

    /// <summary>
    ///     Every row of a table, each one on its own: a row that fails is reported and the next one is read. A table that
    ///     doesn't exist (they vary by Windows version) is skipped
    /// </summary>
    private void ReadTable(Session session, JET_DBID dbid, string tableName, Action<Row> readRow)
    {
        Table table;
        try
        {
            table = new Table(session, dbid, tableName, OpenTableGrbit.ReadOnly);
        }
        catch (EsentObjectNotFoundException)
        {
            Log.Debug("No table {TableName}", tableName);
            return;
        }
        catch (Exception e)
        {
            AddError($"{tableName}: the table could not be opened: {e.Message}");
            return;
        }

        using (table)
        {
            var rowNumber = 0;
            _missingIds = 0;
            try
            {
                var row = new Row(this, session, table, Api.GetColumnDictionary(session, table), tableName);

                Api.JetSetTableSequential(session, table, SetTableSequentialGrbit.None);
                Api.MoveBeforeFirst(session, table);

                while (Api.TryMoveNext(session, table))
                {
                    rowNumber += 1;
                    try
                    {
                        readRow(row);
                    }
                    catch (Exception e)
                    {
                        AddError($"{tableName} row {rowNumber}: {e.Message}");
                    }
                }

                Api.JetResetTableSequential(session, table, ResetTableSequentialGrbit.None);
            }
            catch (Exception e)
            {
                AddError($"{tableName}: reading stopped after {rowNumber:N0} rows: {e.Message}");
            }

            var missingIds = _missingIds;
            if (missingIds > 0)
            {
                AddWarning($"{tableName}: {missingIds:N0} app or user id(s) are not in SruDbIdMapTable; those app or user columns are empty");
            }
        }
    }

    //rows of the current table whose ids are not in the id map (they lost the whole table: KeyNotFoundException)
    private int _missingIds;

    private AppInfo App(int appId)
    {
        if (AppMaps.TryGetValue(appId, out var app))
        {
            return app;
        }

        _missingIds += 1;
        return null;
    }

    private UserInfo User(int userId)
    {
        if (UserMaps.TryGetValue(userId, out var user))
        {
            return user;
        }

        _missingIds += 1;
        return null;
    }

    private static string ProfileName(Dictionary<int, string> map, int profileId)
    {
        return map.TryGetValue(profileId, out var p) ? p : string.Empty;
    }

    /// <summary>
    ///     {7ACBBAA3-D029-4BE4-9A7A-0885927F1D8F}
    /// </summary>
    private void GetVfuprovs(Session session, JET_DBID dbid)
    {
        ReadTable(session, dbid, Vfuprov.TableName, r =>
        {
            var appId = r.Int("AppId");
            var userId = r.Int("UserId");

            var pu = new Vfuprov(r.Int("AutoIncId"), r.Time("TimeStamp"), userId, appId, r.Int("Flags"), r.FileTime("StartTime"), r.FileTime("EndTime"));

            var app = App(appId);
            var user = User(userId);
            pu.ExeInfo = app?.ExeInfo;
            pu.ExeInfoDescription = app?.ExeInfoDescription;
            pu.ExeTimestamp = app?.Timestamp;
            pu.Sid = user?.Sid;
            pu.SidType = user?.SidType ?? SidTypeEnum.UnknownOrUserSid;
            pu.UserName = user?.UserName;

            Vfuprovs.Add(pu.Id, pu);
        });
    }

    /// <summary>
    ///     {5C8CF1C7-7257-4F13-B223-970EF5939312}
    /// </summary>
    private void GetAppTimelines(Session session, JET_DBID dbid)
    {
        ReadTable(session, dbid, TimelineProvider.TableName, r =>
        {
            var appId = r.Int("AppId");
            var userId = r.Int("UserId");

            var pu = new TimelineProvider(r.Int("AutoIncId"), r.Time("TimeStamp"), userId, appId, r.Int("DurationMS"), r.FileTime("EndTime"))
            {
                InFocusS = r.IntOrNull("InFocusS"),
                PsmForegroundS = r.IntOrNull("PSMForegroundS"),
                UserInputS = r.IntOrNull("UserInputS"),
                KeyboardInputS = r.IntOrNull("KeyboardInputS"),
                MouseInputS = r.IntOrNull("MouseInputS"),
                DisplayRequiredS = r.IntOrNull("DisplayRequiredS"),
                AudioInS = r.IntOrNull("AudioInS"),
                AudioOutS = r.IntOrNull("AudioOutS")
            };

            var app = App(appId);
            var user = User(userId);
            pu.ExeInfo = app?.ExeInfo;
            pu.ExeInfoDescription = app?.ExeInfoDescription;
            pu.ExeTimestamp = app?.Timestamp;
            pu.Sid = user?.Sid;
            pu.SidType = user?.SidType ?? SidTypeEnum.UnknownOrUserSid;
            pu.UserName = user?.UserName;

            TimelineProviders.Add(pu.Id, pu);
        });
    }

    /// <summary>
    ///     The tables of the providers seen on Server 2022 (Issue #15)
    /// </summary>
    private void GetNewerProviders(Session session, JET_DBID dbid)
    {
        void Fill(SrumRecord rec)
        {
            var app = App(rec.AppId);
            var user = User(rec.UserId);
            rec.ExeInfo = app?.ExeInfo;
            rec.ExeInfoDescription = app?.ExeInfoDescription;
            rec.ExeTimestamp = app?.Timestamp;
            rec.Sid = user?.Sid;
            rec.SidType = user?.SidType ?? SidTypeEnum.UnknownOrUserSid;
            rec.UserName = user?.UserName;
        }

        ReadTable(session, dbid, ProcessorTimeInfo.TableName, r =>
        {
            var rec = new ProcessorTimeInfo(r.Int("AutoIncId"), r.Time("TimeStamp"), r.Int("UserId"), r.Int("AppId"), r.Long("ProcessorTime"));
            Fill(rec);
            ProcessorTimes.Add(rec.Id, rec);
        });

        ReadTable(session, dbid, SizeInBytesInfo.TableName, r =>
        {
            var rec = new SizeInBytesInfo(r.Int("AutoIncId"), r.Time("TimeStamp"), r.Int("UserId"), r.Int("AppId"), r.Long("SizeInBytes"));
            Fill(rec);
            SizeInBytesInfos.Add(rec.Id, rec);
        });

        ReadTable(session, dbid, TotalUsedInfo.TableName, r =>
        {
            var rec = new TotalUsedInfo(r.Int("AutoIncId"), r.Time("TimeStamp"), r.Int("UserId"), r.Int("AppId"), r.Long("Total"), r.Long("Used"));
            Fill(rec);
            TotalUsedInfos.Add(rec.Id, rec);
        });

        ReadTable(session, dbid, NetworkBytesInfo.TableName, r =>
        {
            var rec = new NetworkBytesInfo(r.Int("AutoIncId"), r.Time("TimeStamp"), r.Int("UserId"), r.Int("AppId"), r.Long("BytesInBound"), r.Long("BytesOutBound"), r.Long("BytesTotal"));
            Fill(rec);
            NetworkBytesInfos.Add(rec.Id, rec);
        });
    }

    /// <summary>
    ///     Writes the tables and columns of an ESE database to the log (read from a copy)
    /// </summary>
    public static void DumpTableInfo(string fileName) => DumpTableInfo(fileName, null);

    /// <summary>
    ///     As <see cref="DumpTableInfo(string)" />, with the copy made in a new folder in <paramref name="tempDirectory" />
    ///     (null: the system's temporary folder)
    /// </summary>
    public static void DumpTableInfo(string fileName, string tempDirectory)
    {
        if (File.Exists(fileName) == false)
        {
            throw new FileNotFoundException($"{fileName} does not exist!");
        }

        var full = Path.GetFullPath(fileName);
        using var ws = EseWorkspace.Create(Path.GetDirectoryName(full), new[] {full}, "SRU", m => Log.Warning("{Message}", m), tempDirectory);
        var copy = Path.Combine(ws.WorkDirectory, Path.GetFileName(full));
        var dbid = ws.OpenDatabase(copy);

        foreach (var table in Api.GetTableNames(ws.Session, dbid))
        {
            Log.Information("TABLE: {Table}", table);

            foreach (var column in Api.GetTableColumns(ws.Session, dbid, table))
            {
                Log.Information("\t{Name}: {ColumnType}", column.Name, column.Coltyp);
            }

            Log.Information("------------------------------------");
        }

        ws.CloseDatabase(copy, dbid);
    }

    /// <summary>
    ///     {FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}LT and {FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}
    /// </summary>
    private void GetEnergyUsages(Session session, JET_DBID dbid)
    {
        var lastSeen = 0;

        // TABLE: {FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}LT
        // ActiveAcTime, ActiveDcTime, ActiveDischargeTime, ActiveEnergy, AppId, AutoIncId: Long; ConfigurationHash: LongLong;
        // CsAcTime, CsDcTime, CsDischargeTime, CsEnergy, CycleCount, DesignedCapacity, FullChargedCapacity: Long;
        // TimeStamp: DateTime; UserId: Long
        ReadTable(session, dbid, "{FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}LT", r =>
        {
            var pu = new EnergyUsage(r.Int("AutoIncId"), true, r.Time("TimeStamp"), r.Int("UserId"), r.Int("AppId"), r.Long("ConfigurationHash"), -1, -1, -1,
                r.Int("CycleCount"), r.Int("DesignedCapacity"), r.Int("FullChargedCapacity"), r.Int("ActiveAcTime"), r.Int("ActiveDcTime"),
                r.Int("ActiveDischargeTime"), r.Int("ActiveEnergy"), r.Int("CsAcTime"), r.Int("CsDcTime"), r.Int("CsDischargeTime"), r.Int("CsEnergy"));

            EnergyUsages.Add(pu.Id, pu);

            lastSeen = pu.Id;
        });

        // TABLE: {FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}
        // AppId, AutoIncId, ChargeLevel: Long; ConfigurationHash: LongLong; CycleCount, DesignedCapacity: Long;
        // EventTimestamp: LongLong; FullChargedCapacity, StateTransition: Long; TimeStamp: DateTime; UserId: Long
        ReadTable(session, dbid, "{FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}", r =>
        {
            var eventTimestamp = r.Long("EventTimestamp");
            if (eventTimestamp > 0 && !SrumTime.IsValid(eventTimestamp))
            {
                AddError($"{{FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}}: EventTimestamp 0x{eventTimestamp:X} is not a valid time");
            }

            var pu = new EnergyUsage(r.Int("AutoIncId"), false, r.Time("TimeStamp"), r.Int("UserId"), r.Int("AppId"), r.Long("ConfigurationHash"), eventTimestamp,
                r.Int("StateTransition"), r.Int("ChargeLevel"), r.Int("CycleCount"), r.Int("DesignedCapacity"), r.Int("FullChargedCapacity"), -1, -1, -1, -1, -1, -1, -1, -1);

            if (EnergyUsages.ContainsKey(pu.Id))
            {
                while (EnergyUsages.ContainsKey(lastSeen))
                {
                    lastSeen += 1;
                }

                Log.Warning("Id {Id} in use from LT table. Incrementing by {LastSeen}", pu.Id, lastSeen);
                pu.Id += lastSeen;
            }

            EnergyUsages.Add(pu.Id, pu);
        });
    }

    /// <summary>
    ///     {D10CA2FE-6FCF-4F6D-848E-B2E99266FA86}
    /// </summary>
    private void GetPushNotifications(Session session, JET_DBID dbid)
    {
        ReadTable(session, dbid, PushNotification.TableName, r =>
        {
            var appId = r.Int("AppId");
            var userId = r.Int("UserId");

            var pu = new PushNotification(r.Int("AutoIncId"), r.Time("TimeStamp"), r.Int("NetworkType"), r.Int("NotificationType"), r.Int("PayloadSize"), userId, appId);

            var app = App(appId);
            var user = User(userId);
            pu.ExeInfo = app?.ExeInfo;
            pu.ExeInfoDescription = app?.ExeInfoDescription;
            pu.ExeTimestamp = app?.Timestamp;
            pu.Sid = user?.Sid;
            pu.SidType = user?.SidType ?? SidTypeEnum.UnknownOrUserSid;
            pu.UserName = user?.UserName;

            PushNotifications.Add(pu.Id, pu);
        });
    }

    /// <summary>
    ///     {DD6636C4-8929-4683-974E-22C046A43763}
    /// </summary>
    private void GetNetworkConnections(Session session, JET_DBID dbid)
    {
        ReadTable(session, dbid, NetworkConnection.TableName, r =>
        {
            var appId = r.Int("AppId");
            var userId = r.Int("UserId");
            var pId = r.Int("L2ProfileId");

            var cstV = SrumTime.FromFileTime(r.FileTime("ConnectStartTime"));

            var nu = new NetworkConnection(r.Int("AutoIncId"), r.Time("TimeStamp"), r.Int("ConnectedTime"), cstV, r.Long("InterfaceLuid"), r.Int("L2ProfileFlags"), pId, userId, appId,
                ProfileName(ProfileIndexToSsid, pId));

            var app = App(appId);
            var user = User(userId);
            nu.ExeInfo = app?.ExeInfo;
            nu.ExeInfoDescription = app?.ExeInfoDescription;
            nu.ExeTimestamp = app?.Timestamp;
            nu.Sid = user?.Sid;
            nu.SidType = user?.SidType ?? SidTypeEnum.UnknownOrUserSid;
            nu.UserName = user?.UserName;

            NetworkConnections.Add(nu.Id, nu);
        });
    }


    /// <summary>
    ///     {973F5D5C-1D90-4944-BE8E-24B94231A174}
    /// </summary>
    private void GetNetworkUsageInfo(Session session, JET_DBID dbid)
    {
        ReadTable(session, dbid, NetworkUsage.TableName, r =>
        {
            var appId = r.Int("AppId");
            var userId = r.Int("UserId");
            var pId = r.Int("L2ProfileId");

            var nu = new NetworkUsage(r.Int("AutoIncId"), r.Time("TimeStamp"), appId, userId, r.Long("BytesRecvd"), r.Long("BytesSent"), r.Long("InterfaceLuid"), r.Int("L2ProfileFlags"), pId,
                ProfileName(ProfileIndexToSsid, pId));

            var app = App(appId);
            var user = User(userId);
            nu.ExeInfo = app?.ExeInfo;
            nu.ExeInfoDescription = app?.ExeInfoDescription;
            nu.ExeTimestamp = app?.Timestamp;
            nu.Sid = user?.Sid;
            nu.SidType = user?.SidType ?? SidTypeEnum.UnknownOrUserSid;
            nu.UserName = user?.UserName;

            NetworkUsages.Add(nu.Id, nu);
        });
    }

    /// <summary>
    ///     {D10CA2FE-6FCF-4F6D-848E-B2E99266FA89}
    /// </summary>
    private void GetApplicationResourceUsage(Session session, JET_DBID dbid)
    {
        ReadTable(session, dbid, AppResourceUseInfo.TableName, r =>
        {
            var appId = r.Int("AppId");
            var userId = r.Int("UserId");

            var ari = new AppResourceUseInfo(r.Int("AutoIncId"), r.Time("TimeStamp"), appId, userId,
                r.Long("BackgroundBytesRead"), r.Long("BackgroundBytesWritten"), r.Long("BackgroundCycleTime"), r.Long("FaceTime"),
                r.Long("ForegroundBytesRead"), r.Long("ForegroundBytesWritten"), r.Long("ForegroundCycleTime"),
                r.Int("BackgroundContextSwitches"), r.Int("BackgroundNumberOfFlushes"), r.Int("BackgroundNumReadOperations"), r.Int("BackgroundNumWriteOperations"),
                r.Int("ForegroundContextSwitches"), r.Int("ForegroundNumberOfFlushes"), r.Int("ForegroundNumReadOperations"), r.Int("ForegroundNumWriteOperations"));

            var app = App(appId);
            var user = User(userId);
            ari.ExeInfo = app?.ExeInfo;
            ari.ExeInfoDescription = app?.ExeInfoDescription;
            ari.ExeTimestamp = app?.Timestamp;
            ari.Sid = user?.Sid;
            ari.SidType = user?.SidType ?? SidTypeEnum.UnknownOrUserSid;
            ari.UserName = user?.UserName;

            AppResourceUseInfos.Add(ari.Id, ari);
        });
    }


    public static InterfaceType GetInterfaceTypeFromLuid(long val)
    {
        var b = BitConverter.GetBytes(val);
        var intType = BitConverter.ToInt16(b, 6);

        return (InterfaceType)intType;
    }


    private void BuildIdMap(Session session, JET_DBID dbid)
    {
        //(it was not in a try: a damaged map ended everything)
        ReadTable(session, dbid, "SruDbIdMapTable", r =>
        {
            var idType = r.Byte("IdType");
            var index = r.Int("IdIndex");
            var blob = r.Bytes("IdBlob");

            var outVal = string.Empty;
            var userName = string.Empty;

            switch (idType)
            {
                case null:
                    throw new InvalidDataException($"IdIndex {index} has no IdType");

                //apps, services and modern apps; objects (4) and anything newer are kept the same way (text)
                default:
                    if (blob != null)
                    {
                        outVal = Encoding.Unicode.GetString(blob).Trim('\0');
                    }


                    var app = new AppInfo(index, (MapType)idType, outVal);

                    AppMaps.Add(app.Index, app);

                    break;

                case 3:
                    outVal = ConvertHexStringToSidString(blob);

                    if (SidToUser.ContainsKey(outVal))
                    {
                        userName = SidToUser[outVal];
                    }


                    var user = new UserInfo(index, outVal, userName);

                    UserMaps.Add(user.Index, user);

                    break;
            }
        });
    }


    public static SidTypeEnum GetSidTypeFromSidString(string sid)
    {
        var sidType = SidTypeEnum.UnknownOrUserSid;

        switch (sid)
        {
            case "S-1-0-0":
                sidType = SidTypeEnum.Null;
                break;

            case "S-1-1-0":
                sidType = SidTypeEnum.Everyone;
                break;

            case "S-1-2-0":
                sidType = SidTypeEnum.Local;
                break;

            case "S-1-2-1":
                sidType = SidTypeEnum.ConsoleLogon;
                break;

            case "S-1-3-0":
                sidType = SidTypeEnum.CreatorOwner;
                break;

            case "S-1-3-1":
                sidType = SidTypeEnum.CreatorGroup;
                break;

            case "S-1-3-2":
                sidType = SidTypeEnum.OwnerServer;
                break;

            case "S-1-3-3":
                sidType = SidTypeEnum.GroupServer;
                break;

            case "S-1-3-4":
                sidType = SidTypeEnum.OwnerServer;
                break;

            case "S-1-5-1":
                sidType = SidTypeEnum.Dialup;
                break;

            case "S-1-5-2":
                sidType = SidTypeEnum.Network;
                break;

            case "S-1-5-3":
                sidType = SidTypeEnum.Batch;
                break;

            case "S-1-5-4":
                sidType = SidTypeEnum.Interactive;
                break;

            case "S-1-5-6":
                sidType = SidTypeEnum.Service;
                break;

            case "S-1-5-7":
                sidType = SidTypeEnum.Anonymous;
                break;

            case "S-1-5-8":
                sidType = SidTypeEnum.Proxy;
                break;

            case "S-1-5-9":
                sidType = SidTypeEnum.EnterpriseDomainControllers;
                break;

            case "S-1-5-10":
                sidType = SidTypeEnum.PrincipalSelf;
                break;

            case "S-1-5-11":
                sidType = SidTypeEnum.AuthenticatedUsers;
                break;

            case "S-1-5-12":
                sidType = SidTypeEnum.RestrictedCode;
                break;

            case "S-1-5-13":
                sidType = SidTypeEnum.TerminalServerUser;
                break;

            case "S-1-5-14":
                sidType = SidTypeEnum.RemoteInteractiveLogon;
                break;

            case "S-1-5-15":
                sidType = SidTypeEnum.ThisOrganization;
                break;

            case "S-1-5-17":
                sidType = SidTypeEnum.Iusr;
                break;

            case "S-1-5-18":
                sidType = SidTypeEnum.LocalSystem;
                break;

            case "S-1-5-19":
                sidType = SidTypeEnum.LocalService;
                break;

            case "S-1-5-20":
                sidType = SidTypeEnum.NetworkService;
                break;

            case "S-1-5-21-0-0-0-496":
                sidType = SidTypeEnum.CompoundedAuthentication;
                break;

            case "S-1-5-21-0-0-0-497":
                sidType = SidTypeEnum.ClaimsValid;
                break;

            case "S-1-5-32-544":
                sidType = SidTypeEnum.BuiltinAdministrators;
                break;

            case "S-1-5-32-545":
                sidType = SidTypeEnum.BuiltinUsers;
                break;

            case "S-1-5-32-546":
                sidType = SidTypeEnum.BuiltinGuests;
                break;

            case "S-1-5-32-547":
                sidType = SidTypeEnum.PowerUsers;
                break;

            case "S-1-5-32-548":
                sidType = SidTypeEnum.AccountOperators;
                break;

            case "S-1-5-32-549":
                sidType = SidTypeEnum.ServerOperators;
                break;

            case "S-1-5-32-550":
                sidType = SidTypeEnum.PrinterOperators;
                break;

            case "S-1-5-32-551":
                sidType = SidTypeEnum.BackupOperators;
                break;

            case "S-1-5-32-552":
                sidType = SidTypeEnum.Replicator;
                break;

            case "S-1-5-32-554":
                sidType = SidTypeEnum.AliasPrew2Kcompacc;
                break;

            case "S-1-5-32-555":
                sidType = SidTypeEnum.RemoteDesktop;
                break;

            case "S-1-5-32-556":
                sidType = SidTypeEnum.NetworkConfigurationOps;
                break;

            case "S-1-5-32-557":
                sidType = SidTypeEnum.IncomingForestTrustBuilders;
                break;

            case "S-1-5-32-558":
                sidType = SidTypeEnum.PerfmonUsers;
                break;

            case "S-1-5-32-559":
                sidType = SidTypeEnum.PerflogUsers;
                break;

            case "S-1-5-32-560":
                sidType = SidTypeEnum.WindowsAuthorizationAccessGroup;
                break;

            case "S-1-5-32-561":
                sidType = SidTypeEnum.TerminalServerLicenseServers;
                break;

            case "S-1-5-32-562":
                sidType = SidTypeEnum.DistributedComUsers;
                break;

            case "S-1-5-32-568":
                sidType = SidTypeEnum.IisIusrs;
                break;

            case "S-1-5-32-569":
                sidType = SidTypeEnum.CryptographicOperators;
                break;

            case "S-1-5-32-573":
                sidType = SidTypeEnum.EventLogReaders;
                break;

            case "S-1-5-32-574":
                sidType = SidTypeEnum.CertificateServiceDcomAccess;
                break;

            case "S-1-5-32-575":
                sidType = SidTypeEnum.RdsRemoteAccessServers;
                break;

            case "S-1-5-32-576":
                sidType = SidTypeEnum.RdsEndpointServers;
                break;

            case "S-1-5-32-577":
                sidType = SidTypeEnum.RdsManagementServers;
                break;

            case "S-1-5-32-578":
                sidType = SidTypeEnum.HyperVAdmins;
                break;

            case "S-1-5-32-579":
                sidType = SidTypeEnum.AccessControlAssistanceOps;
                break;

            case "S-1-5-32-580":
                sidType = SidTypeEnum.RemoteManagementUsers;
                break;

            case "S-1-5-33":
                sidType = SidTypeEnum.WriteRestrictedCode;
                break;

            case "S-1-5-64-10":
                sidType = SidTypeEnum.NtlmAuthentication;
                break;

            case "S-1-5-64-14":
                sidType = SidTypeEnum.SchannelAuthentication;
                break;

            case "S-1-5-64-21":
                sidType = SidTypeEnum.DigestAuthentication;
                break;

            case "S-1-5-65-1":
                sidType = SidTypeEnum.ThisOrganizationCertificate;
                break;

            case "S-1-5-80":
                sidType = SidTypeEnum.NtService;
                break;

            case "S-1-5-84-0-0-0-0-0":
                sidType = SidTypeEnum.UserModeDrivers;
                break;

            case "S-1-5-113":
                sidType = SidTypeEnum.LocalAccount;
                break;

            case "S-1-5-114":
                sidType = SidTypeEnum.LocalAccountAndMemberOfAdministratorsGroup;
                break;

            case "S-1-5-1000":
                sidType = SidTypeEnum.OtherOrganization;
                break;

            case "S-1-15-2-1":
                sidType = SidTypeEnum.AllAppPackages;
                break;

            case "S-1-16-0":
                sidType = SidTypeEnum.MlUntrusted;
                break;

            case "S-1-16-4096":
                sidType = SidTypeEnum.MlLow;
                break;

            case "S-1-16-8192":
                sidType = SidTypeEnum.MlMedium;
                break;

            case "S-1-16-8448":
                sidType = SidTypeEnum.MlMediumPlus;
                break;

            case "S-1-16-12288":
                sidType = SidTypeEnum.MlHigh;
                break;

            case "S-1-16-16384":
                sidType = SidTypeEnum.MlSystem;
                break;

            case "S-1-16-20480":
                sidType = SidTypeEnum.MlProtectedProcess;
                break;

            case "S-1-18-1":
                sidType = SidTypeEnum.AuthenticationAuthorityAssertedIdentity;
                break;

            case "S-1-18-2":
                sidType = SidTypeEnum.ServiceAssertedIdentity;
                break;

            default:
                sidType = SidTypeEnum.UnknownOrUserSid;
                break;
        }

        if (sidType == SidTypeEnum.UnknownOrUserSid)
        {
            if (sid.StartsWith("S-1-5-5-"))
            {
                sidType = SidTypeEnum.LogonId;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-498"))
            {
                sidType = SidTypeEnum.EnterpriseDomainControllers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-500"))
            {
                sidType = SidTypeEnum.Administrator;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-501"))
            {
                sidType = SidTypeEnum.Guest;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-512"))
            {
                sidType = SidTypeEnum.DomainAdmins;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-513"))
            {
                sidType = SidTypeEnum.DomainUsers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-514"))
            {
                sidType = SidTypeEnum.DomainGuests;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-515"))
            {
                sidType = SidTypeEnum.DomainComputers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-516"))
            {
                sidType = SidTypeEnum.DomainDomainControllers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-517"))
            {
                sidType = SidTypeEnum.CertPublishers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-518"))
            {
                sidType = SidTypeEnum.SchemaAdministrators;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-519"))
            {
                sidType = SidTypeEnum.EnterpriseAdmins;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-520"))
            {
                sidType = SidTypeEnum.GroupPolicyCreatorOwners;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-521"))
            {
                sidType = SidTypeEnum.ReadonlyDomainControllers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-522"))
            {
                sidType = SidTypeEnum.CloneableControllers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-525"))
            {
                sidType = SidTypeEnum.ProtectedUsers;
            }

            if (sid.StartsWith("S-1-5-21-") && sid.EndsWith("-553"))
            {
                sidType = SidTypeEnum.RasServers;
            }
        }


        return sidType;
    }

    public static string ConvertHexStringToSidString(byte[] hex)
    {
        //If your SID is S-1-5-21-2127521184-1604012920-1887927527-72713, then your raw hex SID is 01 05 00 00 00 00 00 05 15000000 A065CF7E 784B9B5F E77C8770 091C0100

        //This breaks down as follows:
        //01 S-1
        //05 (seven dashes, seven minus two = 5)
        //000000000005 (5 = 0x000000000005, big-endian)
        //15000000 (21 = 0x00000015, little-endian)
        //A065CF7E (2127521184 = 0x7ECF65A0, little-endian)
        //784B9B5F (1604012920 = 0x5F9B4B78, little-endian)
        //E77C8770 (1887927527 = 0X70877CE7, little-endian)
        //091C0100 (72713 = 0x00011c09, little-endian)

        //page 191 http://amnesia.gtisc.gatech.edu/~moyix/suzibandit.ltd.uk/MSc/Registry%20Structure%20-%20Appendices%20V4.pdf

        //"01-01-00-00-00-00-00-05-12-00-00-00" == S-1-5-18  Local System
        //"01-02-00-00-00-00-00-05-20-00-00-00-20-02-00-00" == S-1-5-32-544 Administrators
        //"01-01-00-00-00-00-00-05-0C-00-00-00" = S-1-5-12  Restricted Code

        const string header = "S";

        if (hex == null || hex.Length <= 8)
        {
            return string.Empty;
        }

        var sidVersion = hex[0].ToString();

        var authId = BitConverter.ToInt32(hex.Skip(4).Take(4).Reverse().ToArray(), 0);

        var sid = new StringBuilder($"{header}-{sidVersion}-{authId}");

        //whole sub authorities only (a blob that wasn't a multiple of 4 threw, and that lost the id map)
        for (var index = 8; index + 4 <= hex.Length; index += 4)
        {
            sid.Append('-').Append(BitConverter.ToUInt32(hex, index));
        }

        return sid.ToString();
    }
}
