# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Part of Eric Zimmerman's DFIR tool suite: the System Resource Usage Monitor database (`C:\Windows\System32\sru\SRUDB.dat`).

- **`SrumData`** (`netstandard2.0`, LangVersion 10): the library, on NuGet as `SrumData` (no other repository uses it). ManagedEsent calls Windows' `esent.dll`, so it only runs on Windows. Uses the Registry package for the optional SOFTWARE hive (user names from `ProfileList`, Wi-Fi profile names from `WlanSvc\Interfaces`).
  - `new Srum(fileName, softwareHive)`: `SruDbIdMapTable` (`AppMaps`, `UserMaps`), then one dictionary per provider table (keyed by `AutoIncId`): network usage, app resource use, network connections, push notifications, energy usage (and LT), vfuprov, app timeline, and the four tables seen on Server 2022 (Issue #15: `ProcessorTimes`, `SizeInBytesInfos`, `TotalUsedInfos`, `NetworkBytesInfos`, classes derived from `SrumRecord`, named after their columns as what they measure is undocumented). Throws for a missing file or a database that can't be opened even from a repaired copy.
  - **`EseWorkspace`** (internal; the same file as in the Sum repository): the database is read from a copy in `<temp>\SrumData-<guid>`, never in place. `<temp>` is the system's temp folder for `new Srum(file, hive)` (the 2026.10 API, unchanged) or the folder given to `new Srum(file, hive, tempDirectory)`; SrumECmd passes `--temp`, or by default `Temp` next to SrumECmd (`TempFolder.cs`, the same file as in AmcacheParser, WxTCmd, SQLECmd and SumECmd; Eric, 2026-10-20: nothing in the user's %TEMP%), created when needed and removed when empty, and stops with exit code 1 if it can't be written to (no fallback: the user gives `--temp`). The `SRU*` `.log`/`.chk`/`.jrs` files are copied and replayed into the copy (`AlternateDatabaseRecoveryDirectory` = the copy's folder: the logs name `C:\Windows\System32\sru\SRUDB.dat`, and replaying in place failed with "Access denied" on a machine whose own SRUDB.dat is locked); a copy that still won't attach is repaired once with `esentutl.exe /p`, reported in `Warnings`. The folder is deleted at the end.
  - Id map types: 0-2 apps (text, `!!name!yyyy/MM/dd:HH:mm:ss!x!description` parsed by `AppInfo`), 3 SIDs, 4 objects of the newer providers (`MapType.Object`: volumes, `Total`, network adapters; they were skipped), anything else kept as text.
  - Every row is read on its own (`ReadTable` + `Row`: column ids once per table, a missing column or null gives 0); a row that fails is in `ParseErrors`, the rest is kept. A missing table is not an error (they vary by Windows version). App/user ids not in the map leave those columns empty and are counted in `Warnings` (a missing id lost the whole table). FILETIMEs out of range are `DateTimeOffset.MinValue` plus a `ParseErrors` entry (`SrumTime`); ESE DateTime columns that can't be converted likewise. The SOFTWARE hive is read profile by profile, with errors in `ParseErrors`.
  - The CSV column order comes from the classes' property order: new properties go at the end (`TimelineProvider`'s `InFocusS` ... `AudioOutS`, Issue #9). Timeline Explorer's plugins match the headers.
- **`SrumECmd`** (`net472;net10.0`): `-f` or `-d` (the first `SRUDB.dat` and `SOFTWARE` under it; net472 enumerates with AlphaFS), `-r`, `--csv` (required), `--dt` (all times, `Timestamp` included), `--temp` (where the copy is made, see `EseWorkspace`). `ExportCsv` writes one CSV per table (`<timestamp>_SrumECmd_<name>_Output.csv`; the seven original ones always, the Server 2022 ones only with rows) and puts a base class's columns first. `Main` returns the exit code (Eric's rule, 2026-10-22: 1 only when the tool fails): 1 for invalid arguments, a missing input, a database that can't be read (access denied, locked, IO errors), a `-d` folder that can't be searched completely, or an export that failed; a damaged database (reported) or a `-d` folder without `SRUDB.dat` gives 0.

Input may be corrupt or hostile: keep what can be read and report the rest.

## Commands

The SDK is pinned in `global.json` (.NET 10). Tests use NUnit 5 + FluentAssertions 7 (pinned to `[7.2.2,8.0.0)`, since 8.x requires a commercial license). Windows only.

```bash
dotnet build -c Release                       # Srum.slnx
dotnet test -c Release                        # all tests (CI: windows-latest)
dotnet pack -c Release                        # SrumData package
```

## Tests (`SrumTest`, net10.0)

- `SrumBuilder` creates SRUM databases with ManagedEsent (the real tables' column names and types), including bad values (a "!!" entry with a bad time, a SID blob that isn't whole sub authorities, ids not in the map, a FILETIME out of range), an IdType 4 object, and dirty databases (`JET_bitTermDirty`, 0x8) with `SRU*.log` next to them.
- `SrumTests`: those cases, a folder that must not change, replay from logs, repair without logs, SIDs, `AppInfo`.
- `SampleTests`: the DFIR Artifact Museum's `Windows/SRUM` (MIT; Windows 10, 11 and Server 2022, clean and dirty), from `SRUM_SAMPLES` or `Corpus/SRUM` in a parent folder (`D:\Corpus\SRUM` here); skipped without it. The counts are the tables' row counts (and what 2026.5.0 exported from copies recovered with `esentutl /r sru /d /i`). Never copy sample or corpus files into the repo.
- `CliTests`: `SrumECmd.dll` as a process: headers, `--dt`, exit codes.
- Validation of a change: old vs new SrumECmd on copies of every sample (the old one after `esentutl /r sru /d /i`), CSVs compared per sample as multisets of rows on the old columns.

## Versioning and release

- **Library:** MinVer sets the version from the git tag (bare tags like `2026.10.0`); there is no `<Version>` in its csproj. To release: push to `master`, then `git tag <ver> && git push origin <ver> && gh release create <ver> --verify-tag --generate-notes`. `.github/workflows/publish.yml` (GuidMapping's, with `run_test` on `windows-latest`) pushes to NuGet with trusted publishing (`NUGET_USER` repo variable).
- **CLI:** `<Version>` in `SrumECmd/SrumECmd.csproj`, bumped by hand; the user builds, signs and releases it.
- Release builds are staged, signed and zipped by `Build-ZimmermanTools.ps1` in the get-zimmermantools repository (cloned next to this one). Its program table says what goes in this tool's download zip (the files taken from the build, folders copied from the repositories, files kept from the current release zip); a new output file, a content folder or a change in how the tool is built (single file, another framework) needs that entry updated.
- pdbs are embedded in the assemblies (`DebugType embedded`, `EmbedUntrackedSources`) and Release builds set `ContinuousIntegrationBuild` (source paths `/_/...`), both in `Directory.Build.props`: stack traces and Exceptionless reports have file and line numbers, and no .pdb file ships. Don't add `DebugType` (or `DebugSymbols`, `IncludeSymbols`, snupkg) to project files.
