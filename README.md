# SrumECmd

Parses the System Resource Usage Monitor database (`SRUDB.dat`, in `C:\Windows\System32\sru`): network usage and
connections, app resource use, energy usage, push notifications and the app timeline, per app and user. With the
SOFTWARE hive (`-r`), user names and Wi-Fi profile names are filled in. Dirty databases are handled without repairing
them by hand. Results are exported to CSV, one file per table.

| | |
|---|---|
| **Version** | 2026.10.0 |
| **Download** | [.NET 10](https://download.ericzimmermanstools.com/net/SrumECmd.zip) (recommended) &middot; [.NET Framework 4.7.2](https://download.ericzimmermanstools.com/SrumECmd.zip) |
| **All tools** | [ericzimmerman.github.io](https://ericzimmerman.github.io) or [Get-ZimmermanTools](https://download.ericzimmermanstools.com/Get-ZimmermanTools.zip), which downloads them all and keeps them current |
| **Changes** | [ChangeLog.txt](https://download.ericzimmermanstools.com/ChangeLog.txt) |
| **Issues** | [EricZimmerman/Srum](https://github.com/EricZimmerman/Srum/issues) or [EricZimmerman/Issues](https://github.com/EricZimmerman/Issues/issues) |

## Requirements

- **.NET 10 build**: the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Run `SrumECmd.exe` on
  Windows (Windows only: the database is read with Windows' ESE engine, `esent.dll`).
- **.NET Framework build**: .NET Framework 4.7.2 or later (Windows only).

## Command line

```text
Usage:
  SrumECmd [options]

Options:
  -f <f>          SRUDB.dat file to parse
  -r <r>          SOFTWARE hive to process. This is optional, but recommended
  -d <d>          Directory to recursively process, looking for SRUDB.dat and SOFTWARE hive. This mode is primarily used with KAPE so both SRUDB.dat and SOFTWARE hive can be located
  --csv <csv>     Directory to save CSV formatted results to. Be sure to include the full path in double quotes
  --dt <dt>       The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options
                   [default: yyyy-MM-dd HH:mm:ss]
  --temp <temp>   Folder for the temporary copy of the SRUM database and its transaction logs, which is opened instead of the evidence and deleted when done. Default: a Temp folder next to SrumECmd. A folder that isn't there is created, and removed again when done. If that folder can't be written to, SrumECmd stops: give a writable folder with --temp
  --debug         Show debug information during processing
  --trace         Show trace information during processing
  -?, -h, --help  Show help and usage information
  --version       Show version information
```

Short options (a single letter) start with one dash, long options with two.

## Examples

```text
SrumECmd.exe -f "C:\Temp\SRUDB.dat" -r "C:\Temp\SOFTWARE" --csv "C:\Temp\"
SrumECmd.exe -f "C:\Temp\SRUDB.dat" --csv "c:\temp"
SrumECmd.exe -d "C:\Temp" --csv "c:\temp"
```

## Output

- **CSV** (`--csv`): one file per provider table, `<timestamp>_SrumECmd_<table>_Output.csv`. `AppResourceUseInfo`,
  `AppTimelineProvider`, `EnergyUsage`, `NetworkConnections`, `NetworkUsages`, `PushNotifications` and `vfuprov` are
  always written.
- `AppTimelineProvider` has the seconds in focus, in the foreground, with user, keyboard and mouse input, with the
  display required and with audio in and out (`InFocusS`, `PsmForegroundS`, `UserInputS`, `KeyboardInputS`,
  `MouseInputS`, `DisplayRequiredS`, `AudioInS`, `AudioOutS`), after the other columns.
- The tables seen on Server 2022 are exported when they have rows: `ProcessorTime`, `SizeInBytes`, `TotalUsed` and
  `NetworkBytes`. What they measure is in `ExeInfo` (for example `Total`, a volume, a network adapter).
- `--dt` applies to every time stamp, the `Timestamp` columns included.

A bad value, an app or user id missing from the id map, or an invalid time no longer loses a whole table, and a damaged
id map entry or SOFTWARE hive profile no longer loses everything: the problems are listed at the end.

Examine the output in Timeline Explorer!

![SrumECmdCSVOutput](https://raw.githubusercontent.com/EricZimmerman/Srum/master/Pictures/SrumECmdCSVOutput.jpg)

## Dirty databases

`SRUDB.dat` is never opened where it is: SrumECmd copies it, with its transaction logs (`SRU*.log`, `SRU.chk`,
`*.jrs`), to a temporary folder and works on the copy. Nothing next to `SRUDB.dat` changes, so it can be read-only. The
temporary folder is `--temp`, or by default a `Temp` folder next to SrumECmd (never the user's `%TEMP%`); the copy is
deleted when done. If that folder can't be written to, SrumECmd stops and asks for `--temp`.

A `SRUDB.dat` collected from a running system is usually dirty. SrumECmd handles this by itself, on the copy:

1. The transaction logs, when they were collected, are replayed into the copy (what `esentutl.exe /r sru /i` did by
   hand; the old way could also fail with "Access denied", as the logs name the live database's path).
2. A copy that is still dirty (its logs are missing) is repaired with `esentutl.exe /p`. What only the missing logs had
   is lost; SrumECmd says when this was done.

There is no need to repair anything by hand any more. `esentutl.exe /mh SRUDB.dat` shows a database's state.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. A damaged database or SOFTWARE hive is reported but is not a failure, and neither is a `-d` folder without `SRUDB.dat` |
| `1` | The tool failed: invalid arguments, a missing input path, a database or `-r` SOFTWARE hive it can't read (access denied, locked, IO errors), a `--temp` folder that can't be written to, or an export that failed. Folders under `-d` that can't be read are reported (`Unable to read folder <path>: <reason>`) and the rest is still processed |

## What's new in 2026.10

- `SRUDB.dat` is read from a copy in a temporary folder: nothing next to it changes (it wrote log and checkpoint files
  there and replayed the logs into the database in place). It can be read-only.
- Dirty databases are handled automatically: the transaction logs are replayed into the copy, and a copy that is still
  dirty is repaired with `esentutl /p`. 4 of the 9 DFIR Artifact Museum samples failed with "Access denied" and
  exported nothing.
- A bad value, a missing app or user id, or an invalid time no longer loses a whole table; the problems are listed at
  the end.
- New CSVs for the Server 2022 tables, when present: `ProcessorTime`, `SizeInBytes`, `TotalUsed` and `NetworkBytes`
  (Srum#15).
- New `AppTimelineProvider` columns at the end: seconds in focus, in the foreground, with input, with the display
  required and with audio (Srum#9).
- `--dt` also applies to the `Timestamp` columns. New `--temp` for the temporary copy (a `Temp` folder next to
  SrumECmd by default, instead of the user's `%TEMP%`).
- Exit code 1 when the tool fails (it was always 0). Error reports include source file and line numbers.

The full list is in the [ChangeLog](https://download.ericzimmermanstools.com/ChangeLog.txt).

## Building

```text
dotnet build Srum.slnx -c Release
```

Builds `net10.0` and `net472` (Windows only). SRUM parsing is in the `SrumData` library in this repository
([NuGet](https://www.nuget.org/packages/SrumData)).

## Special thanks

Open source development funding and support provided by the [SANS Institute](https://www.sans.org/) and
[SANS DFIR](https://www.sans.org/digital-forensics-incident-response/).

## License

[MIT](LICENSE), Copyright (c) Eric R. Zimmerman 2027.
