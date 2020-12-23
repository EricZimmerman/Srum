using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Isam.Esent.Interop;
using Registry;

namespace SrumData
{
    public class IdMapInfo
    {
        public string ExeInfo;
        public string ExeInfoDescription;
        public int Index;
        public MapType MapType;
        public string RawValue;
        public Srum.SidTypeEnum? SidType;
        public DateTimeOffset? Timestamp;
        public string Unknown;

        public IdMapInfo(string rawValue, MapType type, int index)
        {
            RawValue = rawValue;
            Index = index;
            MapType = type;

            ExeInfo = rawValue;
            if (rawValue.StartsWith("!!"))
            {
                var tempVal = RawValue.Substring(2); //strip !!
                var segs = tempVal.Split('!');

                ExeInfo = segs[0];

                Unknown = segs[2];
                ExeInfoDescription = segs[3];

                Timestamp = DateTimeOffset.ParseExact(segs[1], "yyyy/MM/dd:HH:mm:ss", null,
                    DateTimeStyles.AssumeUniversal);
            }

            if (type == MapType.Sid)
            {
                SidType = Srum.GetSidTypeFromSidString(rawValue);
            }
        }
    }

    public enum MapType
    {
        NormalApp = 0,
        Service = 1,
        ModernApp = 2,
        Sid = 3
    }

    public class NetworkUsage
    {
        public IdMapInfo AppIdMapInfo;
        public long BytesReceived;
        public long BytesSent;
        public int Id;
        public long InterfaceLuid;
        public InterfaceType InterfaceType;
        public int L2ProfileFlags;
        public int L2ProfileId;
        public DateTimeOffset Timestamp;
        public IdMapInfo UserIdMapInfo;

        public NetworkUsage(int id, DateTime timestamp, IdMapInfo appId, IdMapInfo userId, long bytesReceived, long bytesSent,
            long interfaceLuid, int l2ProfileFlags, int l2ProfileId)
        {
            Id = id;
            var tsUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
            Timestamp = new DateTimeOffset(tsUtc);
            AppIdMapInfo = appId;
            UserIdMapInfo = userId;
            BytesReceived = bytesReceived;
            BytesSent = bytesSent;
            InterfaceLuid = interfaceLuid;
            L2ProfileFlags = l2ProfileFlags;
            L2ProfileId = l2ProfileId;
            InterfaceType = Srum.GetInterfaceTypeFromLuid(interfaceLuid);
        }
    }
    
    public class NetworkConnection
    {
        public int Id;
        public DateTimeOffset Timestamp;
        public int ConnectedTime;
        public DateTimeOffset ConnectStartTime;
        public long InterfaceLuid;
    
        public InterfaceType InterfaceType;
        public int L2ProfileFlags;
        public int L2ProfileId;
        public IdMapInfo UserIdMapInfo;
        public IdMapInfo AppIdMapInfo;

        public NetworkConnection(int id, DateTime timestamp, int connectedTime, DateTimeOffset connectStartTime, long interfaceLuid, int l2ProfileFlags, int l2ProfileId, IdMapInfo userIdMapInfo, IdMapInfo appIdMapInfo)
        {
            Id = id;
         
            ConnectedTime = connectedTime;
            ConnectStartTime = connectStartTime;
            InterfaceLuid = interfaceLuid;
            L2ProfileFlags = l2ProfileFlags;
            L2ProfileId = l2ProfileId;
            UserIdMapInfo = userIdMapInfo;
            AppIdMapInfo = appIdMapInfo;

            InterfaceType = Srum.GetInterfaceTypeFromLuid(interfaceLuid);
        }
    }

    public class AppResourceUseInfo
    {
        public IdMapInfo AppIdMapInfo;

        public long BackgroundBytesRead;
        public long BackgroundBytesWritten;

        public int BackgroundContextSwitches;
        public long BackgroundCycleTime;
        public int BackgroundNumberOfFlushes;
        public int BackgroundNumReadOperations;
        public int BackgroundNumWriteOperations;
        public long FaceTime;
        public long ForegroundBytesRead;
        public long ForegroundBytesWritten;
        public int ForegroundContextSwitches;
        public long ForegroundCycleTime;
        public int ForegroundNumberOfFlushes;
        public int ForegroundNumReadOperations;

        public int ForegroundNumWriteOperations;

        public int Id;
        public DateTimeOffset Timestamp;
        public IdMapInfo UserIdMapInfo;

        public AppResourceUseInfo(int id, DateTimeOffset timestamp, IdMapInfo appIdMapInfo, IdMapInfo userIdMapInfo, long backgroundBytesRead, long backgroundBytesWritten, long backgroundCycleTime, long faceTime, long foregroundBytesRead,
            long foregroundBytesWritten, long foregroundCycleTime, int backgroundContextSwitches, int backgroundNumberOfFlushes, int backgroundNumReadOperations, int backgroundNumWriteOperations, int foregroundContextSwitches,
            int foregroundNumberOfFlushes, int foregroundNumReadOperations, int foregroundNumWriteOperations)
        {
            Id = id;
            Timestamp = timestamp;
            AppIdMapInfo = appIdMapInfo;
            UserIdMapInfo = userIdMapInfo;
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

    //https://docs.microsoft.com/en-us/windows/win32/extensible-storage-engine/jet-coltyp

    public class Srum
    {
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

        public readonly Dictionary<int, AppResourceUseInfo> AppResourceUseInfos;

        public readonly Dictionary<int, IdMapInfo> IdMap;
        public readonly Dictionary<int, NetworkUsage> NetworkUsages;
        public readonly Dictionary<int, NetworkConnection> NetworkConnections;

        public readonly Dictionary<string, string> SidToUser;

        public Srum(string fileName, string softwareHive)
        {
            if (File.Exists(fileName) == false)
            {
                throw new FileNotFoundException($"'{fileName}' does not exist!");
            }

            SidToUser = new Dictionary<string, string>();

            if (softwareHive != null)
            {
                if (File.Exists(softwareHive) == false)
                {
                    throw new FileNotFoundException($"'{softwareHive}' does not exist!");
                }

                var reg = new RegistryHiveOnDemand(softwareHive);
                var k = reg.GetKey(@"Microsoft\Windows NT\CurrentVersion\ProfileList");

                foreach (var registryKey in k.SubKeys)
                {
                    var kd = reg.GetKey(registryKey.KeyPath);

                    var v = kd.Values.SingleOrDefault(t => t.ValueName == "ProfileImagePath");
                    if (v != null)
                    {
                        SidToUser.Add(registryKey.KeyName, Path.GetFileName(v.ValueData));
                    }
                }
            }

            using var instance = new Instance("pulldata");
            instance.Parameters.Recovery = false;
            instance.Init();

            using var session = new Session(instance);
            Api.JetAttachDatabase(session, fileName, AttachDatabaseGrbit.ReadOnly);
            Api.JetOpenDatabase(session, fileName, null, out var dbid, OpenDatabaseGrbit.ReadOnly);

            IdMap = new Dictionary<int, IdMapInfo>();
            NetworkUsages = new Dictionary<int, NetworkUsage>();
            AppResourceUseInfos = new Dictionary<int, AppResourceUseInfo>();
            NetworkConnections = new Dictionary<int, NetworkConnection>();

            BuildIdMap(session, dbid);
            GetNetworkUsageInfo(session, dbid);
            GetApplicationResourceUsage(session, dbid);
            GetNetworkConnections(session, dbid);


        

            // 478  TABLE: {D10CA2FE-6FCF-4F6D-848E-B2E99266FA86} Push Notification Data
            // 799  TABLE: {FEE4E14F-02A9-4550-B5CE-5FA2DA202E37} Energy Usage
            // 902  TABLE: {FEE4E14F-02A9-4550-B5CE-5FA2DA202E37}LT Energy Usage LT
            // 1  TABLE: {5C8CF1C7-7257-4F13-B223-970EF5939312} unknown
            // 302  TABLE: {7ACBBAA3-D029-4BE4-9A7A-0885927F1D8F} unknown

            // foreach (string table in Api.GetTableNames(session, dbid))
            // {
            //     Console.WriteLine($"TABLE: {table}");
            //
            //     foreach (ColumnInfo column in Api.GetTableColumns(session, dbid, table))
            //     {
            //         Console.WriteLine("\t{0}", column.Name);
            //         Console.WriteLine("\t\tColtyp:     {0}", column.Coltyp);
            //         Console.WriteLine("\t\tColumnid:   {0:N0}", column.Columnid);
            //         if (JET_coltyp.LongText == column.Coltyp || JET_coltyp.Text == column.Coltyp)
            //         {
            //             Console.WriteLine("\t\tCode page:  {0}", column.Cp);
            //         }
            //
            //         Console.WriteLine("\t\tMax length: {0}", column.MaxLength);
            //         Console.WriteLine("\t\tGrbit:      {0}", column.Grbit);
            //
            //         Console.WriteLine("------------------------------------");
            //     }
            //
            //
            // }

            // foreach (string table in Api.GetTableNames(session, dbid))
            // {
            //     Console.WriteLine($"TABLE: {table}");
            //
            //     foreach (ColumnInfo column in Api.GetTableColumns(session, dbid, table))
            //     {
            //         Console.WriteLine("\t{0}: {1}", column.Name,column.Coltyp);
            //         
            //        
            //     }
            //     Console.WriteLine("------------------------------------");
            // }
            //
            //
        }

        
          /// <summary>
        ///     {DD6636C4-8929-4683-974E-22C046A43763}
        /// </summary>
        /// <param name="session"></param>
        /// <param name="dbid"></param>
        private void GetNetworkConnections(Session session, JET_DBID dbid)
        {
            using var networkUsageTable = new Table(session, dbid, "{DD6636C4-8929-4683-974E-22C046A43763}", OpenTableGrbit.ReadOnly);

            Api.JetSetTableSequential(session, networkUsageTable, SetTableSequentialGrbit.None);

            Api.MoveBeforeFirst(session, networkUsageTable);

            while (Api.TryMoveNext(session, networkUsageTable))
            {
        
                var id = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "AutoIncId"));
                var appId = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "AppId"));
                var userId = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "UserId"));
                var iL = Api.RetrieveColumnAsInt64(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "InterfaceLuid"));
                var pf = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "L2ProfileFlags"));
                var pId = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "L2ProfileId"));
                var dt = Api.RetrieveColumnAsDateTime(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "TimeStamp"));

                var ct = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "ConnectedTime"));
                var cst = Api.RetrieveColumnAsInt64(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "ConnectStartTime"));
                
                var app = IdMap[appId.Value];
                var user = IdMap[userId.Value];

                var cstV = DateTimeOffset.FromFileTime(cst.Value).ToUniversalTime();
                
                var nu = new NetworkConnection(id.Value, dt.Value,ct.Value,cstV,iL.Value,pf.Value,pId.Value,user,app);

                NetworkConnections.Add(nu.Id, nu);
            }

            Api.JetResetTableSequential(session, networkUsageTable
                , ResetTableSequentialGrbit.None);
        }
          
          
        /// <summary>
        ///     {973F5D5C-1D90-4944-BE8E-24B94231A174}
        /// </summary>
        /// <param name="session"></param>
        /// <param name="dbid"></param>
        private void GetNetworkUsageInfo(Session session, JET_DBID dbid)
        {
            using var networkUsageTable = new Table(session, dbid, "{973F5D5C-1D90-4944-BE8E-24B94231A174}", OpenTableGrbit.ReadOnly);

            Api.JetSetTableSequential(session, networkUsageTable, SetTableSequentialGrbit.None);

            Api.MoveBeforeFirst(session, networkUsageTable);

            while (Api.TryMoveNext(session, networkUsageTable))
            {
                var id = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "AutoIncId"));
                var appId = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "AppId"));
                var userId = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "UserId"));
                var br = Api.RetrieveColumnAsInt64(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "BytesRecvd"));
                var bs = Api.RetrieveColumnAsInt64(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "BytesSent"));
                var iL = Api.RetrieveColumnAsInt64(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "InterfaceLuid"));
                var pf = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "L2ProfileFlags"));
                var pId = Api.RetrieveColumnAsInt32(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "L2ProfileId"));
                var dt = Api.RetrieveColumnAsDateTime(session, networkUsageTable, Api.GetTableColumnid(session, networkUsageTable, "TimeStamp"));

                var app = IdMap[appId.Value];
                var user = IdMap[userId.Value];

                var nu = new NetworkUsage(id.Value, dt.Value, app, user, br.Value, bs.Value, iL.Value, pf.Value, pId.Value);

                NetworkUsages.Add(nu.Id, nu);
            }

            Api.JetResetTableSequential(session, networkUsageTable
                , ResetTableSequentialGrbit.None);
        }

        /// <summary>
        ///     {D10CA2FE-6FCF-4F6D-848E-B2E99266FA89}
        /// </summary>
        /// <param name="session"></param>
        /// <param name="dbid"></param>
        private void GetApplicationResourceUsage(Session session, JET_DBID dbid)
        {
            using var appResourceUsage
                = new Table(session, dbid, "{D10CA2FE-6FCF-4F6D-848E-B2E99266FA89}", OpenTableGrbit.ReadOnly);

            Api.JetSetTableSequential(session, appResourceUsage
                , SetTableSequentialGrbit.None);

            Api.MoveBeforeFirst(session, appResourceUsage
            );


            while (Api.TryMoveNext(session, appResourceUsage
            ))
            {
                var id = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "AutoIncId"));

                var appId = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "AppId"));

                var userId = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "UserId"));

                var dt = Api.RetrieveColumnAsDateTime(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "TimeStamp"));

                var bbr = Api.RetrieveColumnAsInt64(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "BackgroundBytesRead"));
                var bbw = Api.RetrieveColumnAsInt64(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "BackgroundBytesWritten"));
                var bct = Api.RetrieveColumnAsInt64(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "BackgroundCycleTime"));
                var fbr = Api.RetrieveColumnAsInt64(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "ForegroundBytesRead"));
                var fbw = Api.RetrieveColumnAsInt64(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "ForegroundBytesWritten"));
                var fct = Api.RetrieveColumnAsInt64(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "ForegroundCycleTime"));
                var ft = Api.RetrieveColumnAsInt64(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "FaceTime"));


                var bcs = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "BackgroundContextSwitches"));
                var bnf = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "BackgroundNumberOfFlushes"));
                var bro = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "BackgroundNumReadOperations"));
                var bwo = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "BackgroundNumWriteOperations"));
                
                var fcs = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "ForegroundContextSwitches"));
                var fnf = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "ForegroundNumberOfFlushes"));
                var fro = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "ForegroundNumReadOperations"));
                var fwo = Api.RetrieveColumnAsInt32(session, appResourceUsage, Api.GetTableColumnid(session, appResourceUsage, "ForegroundNumWriteOperations"));


                var app = IdMap[appId.Value];
                var user = IdMap[userId.Value];

                var ari = new AppResourceUseInfo(id.Value, dt.Value, app, user, bbr.Value, bbw.Value, bct.Value, ft.Value, fbr.Value, fbw.Value, fct.Value, bcs.Value, bnf.Value, bro.Value, bwo.Value, fcs.Value, fnf.Value, fro.Value,
                    fwo.Value);

                AppResourceUseInfos.Add(ari.Id, ari);
            }

            Api.JetResetTableSequential(session, appResourceUsage
                , ResetTableSequentialGrbit.None);
        }


        public static InterfaceType GetInterfaceTypeFromLuid(long val)
        {
            var b = BitConverter.GetBytes(val);
            var intType = BitConverter.ToInt16(b, 6);

            return (InterfaceType) intType;
        }


        private void BuildIdMap(Session session, JET_DBID dbid)
        {
            using var idMapTable = new Table(session, dbid, "SruDbIdMapTable", OpenTableGrbit.ReadOnly);

            Api.JetSetTableSequential(session, idMapTable, SetTableSequentialGrbit.None);

            Api.MoveBeforeFirst(session, idMapTable);

            while (Api.TryMoveNext(session, idMapTable))
            {
                var idType = Api.RetrieveColumnAsByte(session, idMapTable, Api.GetTableColumnid(session, idMapTable, "IdType"));
                var index = Api.RetrieveColumnAsInt32(session, idMapTable, Api.GetTableColumnid(session, idMapTable, "IdIndex"));
                var blob = Api.RetrieveColumn(session, idMapTable, Api.GetTableColumnid(session, idMapTable, "IdBlob"));

                var outVal = string.Empty;

                switch (idType)
                {
                    case 0:
                    case 1:
                        if (blob != null)
                        {
                            outVal = Encoding.Unicode.GetString(blob).Trim('\0');
                        }

                        break;

                    case 2:
                        if (blob != null)
                        {
                            outVal = Encoding.Unicode.GetString(blob).Trim('\0');
                        }

                        break;

                    case 3:
                        outVal = ConvertHexStringToSidString(blob);
                        break;
                }


                var mInfo = new IdMapInfo(outVal, (MapType) idType, index.Value);

                IdMap.Add(mInfo.Index, mInfo);
            }

            Api.JetResetTableSequential(session, idMapTable
                , ResetTableSequentialGrbit.None);
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

            //"01- 05- 00-00-00-00-00-05- 15-00-00-00- 82-F6-13-90- 30-42-81-99- 23-04-C3-8F- 51-04-00-00"
            //"01-01-00-00-00-00-00-05-12-00-00-00" == S-1-5-18  Local System 
            //"01-02-00-00-00-00-00-05-20-00-00-00-20-02-00-00" == S-1-5-32-544 Administrators
            //"01-01-00-00-00-00-00-05-0C-00-00-00" = S-1-5-12  Restricted Code 
            //"01-02-00-00-00-00-00-0F-02-00-00-00-01-00-00-00"

            const string header = "S";

            if (hex == null)
            {
                return string.Empty;
            }

            var sidVersion = hex[0].ToString();

            var authId = BitConverter.ToInt32(hex.Skip(4).Take(4).Reverse().ToArray(), 0);

            var index = 8;


            var sid = $"{header}-{sidVersion}-{authId}";

            do
            {
                var tempAuthHex = hex.Skip(index).Take(4).ToArray();

                var tempAuth = BitConverter.ToUInt32(tempAuthHex, 0);

                index += 4;

                sid = $"{sid}-{tempAuth}";
            } while (index < hex.Length);

            //some tests
            //var hexStr = BitConverter.ToString(hex);

            //switch (hexStr)
            //{
            //    case "01-01-00-00-00-00-00-05-12-00-00-00":

            //        Check.That(sid).IsEqualTo("S-1-5-18");

            //        break;

            //    case "01-02-00-00-00-00-00-05-20-00-00-00-20-02-00-00":

            //        Check.That(sid).IsEqualTo("S-1-5-32-544");

            //        break;

            //    case "01-01-00-00-00-00-00-05-0C-00-00-00":
            //        Check.That(sid).IsEqualTo("S-1-5-12");

            //        break;
            //    default:

            //        break;
            //}


            return sid;
        }
    }
}