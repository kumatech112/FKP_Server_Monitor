# Delivery verification

Date: 2026-10-09 (Asia/Bangkok). Initial delivery in the requested UNC workspace.

## Before and after review

The workspace was inspected before implementation: it contained no files and no Git repository. The implementation is isolated to this project. After implementation, all application source files were reviewed for data flow, validation, persistence boundaries, asynchronous UI updates, cancellation, network-resource cleanup and generated-file locations. No unfinished implementation placeholders or shell-based ping invocation were found.

The release was compiled with compiler warnings treated as errors. The verification runner loads a byte-identical copy of the final release executable rather than a separately compiled version of the application.

## Verified behavior

32 verification assertions passed:

- First launch stays empty without creating settings; Thai JSON round-trip works.
- Atomic save retains a previous-version backup; invalid duplicate IDs cannot overwrite data; corrupt JSON remains untouched; no save temporary files remain.
- URL input and invalid ports are rejected; IPv6 literals and valid port boundaries are accepted.
- CSV has Thai UTF-8 BOM text, correct quoting, formula-input protection and explicit untested status.
- A stalled operation times out; cancellation interrupts a pending wait; late failures are observed.
- Actual loopback ICMP and open TCP succeed. A closed local TCP port fails independently of successful ICMP. Hostname resolution and TCP-only mode work.
- The GUI starts checking without blocking, displays independent Thai statuses including DNS failure, and selected-only checks preserve unselected timestamps.
- Real dialogs add and edit Thai entries; bulk entry skips duplicate lines and existing host/port/mode combinations.
- Thai search filters and restores rows; setting changes persist outside the executable directory.
- Automatic refresh performs a subsequent check cycle; Stop restores controls; closing during an active cycle is safe.
- The real distributable executable launches through its entry point and closes with exit code zero.

The main GUI and single-server editor were rendered and visually inspected for Thai text, readable controls, table columns and button layout. The final GUI checks run under a real `Application.Run` message loop with illegal cross-thread access detection enabled. An early harness used only `Show`/message pumping and produced intermittent timer results; that harness was corrected before final verification.

## Evidence and build

Executable SHA-256:

```text
6547538333B6C366990CB4599D192D77227CFCB71A7AC4BBAC6788182BB4E464
```

The build and runner scripts print the current evidence directory and verified hash. Reports, GUI screenshots, fixture settings and verification binaries are kept under `%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-*`, outside the source project and outside Git. Build staging is removed after compilation. Only the EXE, runtime config and release guide remain in `release`.

## Practical verification limits

Networking was verified using controlled local endpoints and an invalid DNS hostname. No list of production servers was supplied, so their actual firewall rules, DNS entries, service ports and connectivity have not been verified. IPv6 input was validated; an actual IPv6 network connection was not part of this delivery check.

This machine has .NET Framework 4.8.1. The release targets .NET Framework 4.8 and uses APIs supported there, but a separate clean Windows machine was not available for testing. GUI rendering was inspected at the current workstation DPI; other DPI settings were not visually tested. The native deletion confirmation and Save As dialog were reviewed in source; CSV writing and editor workflows were exercised independently.

Git stores complete source, docs and build/verification tools. Runtime settings, generated EXEs and evidence are excluded. This is a local repository; no remote backup or push is implied by a local commit.

Short commit message: `feat: add Thai ICMP/TCP server checker with clean project layout`
