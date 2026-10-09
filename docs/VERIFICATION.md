# Verification: ping dashboard 1.1

Date: 2026-10-09 (Asia/Bangkok).

The previous implementation and Git state were inspected before changing it. TCP checking, port/method entry fields, partial-pass status and CSV export were removed. The affected model, networking, editor, dashboard, configuration loading and verification code were reviewed again after the change. Build scripts, cancellation, atomic persistence and the existing project separation remain in use.

## Results

**36 assertions passed against the rebuilt release executable.**

- Empty first launch, Thai JSON round-trip, atomic-save backup, invalid-ID rejection, corruption preservation and temporary-file cleanup.
- IP/hostname validation, IPv6 literal acceptance, rejection of host-plus-port input and prevention of a false pass for an incomplete result.
- Previous schema loads all IDs, Thai names and hosts, including duplicate-host entries. Loading leaves the original untouched. A normal save removes obsolete fields and preserves the original backup. Unsupported future schemas are rejected without rewriting.
- Deadline and cancellation handling.
- Real loopback ping replies and response latency; hostname resolution; failed DNS produces a failed result.
- Thai dashboard pass/fail rendering and absence of port, method, TCP and export controls.
- Selected-row checking updates only the selected row.
- Single-server editor accepts only name/host. Add/edit and bulk workflows persist data. Bulk duplicate-host handling and Thai search work.
- Settings persistence, a subsequent automatic timer cycle, Stop, and closing during a check.
- Actual release entry-point launch and normal exit.

GUI workflows run under a real Windows Forms message loop with cross-thread access detection enabled. The dashboard and two-field editor were rendered and visually inspected.

## Release and evidence

Release SHA-256:

```text
835CDFFF4942756FB1451698327F17D9E171ED92E1EC9A4A7CB75D52886FF6B3
```

Evidence from this verification:

`%LOCALAPPDATA%\Kuma\ServerBotTrackerDevelopment\verification-20261009-141417-495fab47`

The runner compares the verified copy's hash with the release binary. Screenshots, fixture data, verification binaries and logs stay outside the project and Git. The user's settings are not edited by these checks.

## Limits

Controlled loopback endpoints and an invalid hostname were tested. The user's actual production-server list was not supplied, so its connectivity is unverified. IPv6 input validation was tested, but a live IPv6 connection was not. This workstation uses .NET Framework 4.8.1; the app targets 4.8. A separate clean Windows machine and other DPI settings were not tested. Native deletion confirmation was reviewed in source.

The app sends one ICMP echo per server per cycle; it does not reproduce the Windows command's default four-request sequence. Pass/fail describes ping reachability, as requested.

Short commit message: `refactor: simplify server dashboard to ping-only checks`
