# Application routing for Windows

This feature adds **Settings → Application routing** to the WPF and Avalonia
interfaces. Rules associate an executable's full path or filename with the active
profile through the main SOCKS proxy, a saved v2rayN profile, an explicit SOCKS5
endpoint, or an existing Windows network interface.
The implementation handles TCP streams and UDP datagrams with IPv4 and IPv6
destinations. QUIC is carried as UDP; there is no TLS interception.

**Status:** experimental implementation. Compilation, packet tests and local
SOCKS5 relay tests are available. Privileged WinDivert interception, desktop
interaction and end-to-end routing on real adapters still require validation
in a dedicated Windows test environment. Do not treat a passing build as proof
of working process interception or absence of traffic leaks.

## Use

1. Use a separate installation with its own configuration and proxy cores.
   Start that copy as a Windows administrator when testing interception.
2. Keep this copy's TUN mode disabled. Open **Settings → Application routing**.
3. Browse to an executable, enter a full path, or select **Executable name (any
   location)** to match a filename such as `app.exe`. Paths and names may contain
   spaces; surrounding quotes on pasted paths are accepted. The editor scrolls
   long paths horizontally and the rule list wraps them, with full-path tooltips.
   An exact-path rule takes priority over a filename rule. Filename matching is
   case-insensitive and applies to every executable with that name.
   A saved path does not have to exist: an app update or uninstall leaves that
   rule inactive without preventing other rules from starting. Update the path
   or use filename matching if the app moves between versioned directories.
4. Alternatively, click **Choose** to the left of **Browse** to open a separate
   picker window. Names include the PID in parentheses, and a single **TCP/UDP**
   column displays the two connection counts (for example, `5/2`). The list reads IPv4
   and IPv6 TCP connections/listeners and UDP endpoints, without enabling routing.
   System (PID 4) and executables inside the Windows `System32` and `SysWOW64`
   directories are hidden from this picker. This filter does not change saved
   rules or manual executable selection. Search by name, PID or path; refresh
   the snapshot as needed. Double-click an
   app or choose **Use selected app** to fill the editor. If its path cannot be
   read, the picker uses its executable name. **Active profile** is the default
   destination; it uses v2rayN's main local SOCKS proxy. Choose another destination
   if needed and **Save rule**. New rules are enabled; change that state using the
   **Enabled** checkbox in the table.
   Enable **Include child processes** to route helpers and descendants through
   the same destination, or add separate rules for them.
5. Turn on **Enable application routing**, then fully exit and restart the
   selected apps so they open new connections through their selected route.
   Repeat this after saving rule changes while routing is enabled. Previously
   established TCP connections cannot be migrated. The switch is
   disabled during a transition and whenever v2rayN is not running as
   Administrator. It keeps showing the saved enabled setting even when disabled;
   errors use normal v2rayN notifications.
6. Turning the switch off removes interception and stops only Xray processes created by
   this feature. **Close** closes the editor while routing continues. Exiting
   v2rayN stops the engine while preserving the enabled setting. Enabled routing
   starts automatically after v2rayN's normal startup initialization. Windows
   administrator privileges are still required; no automatic UAC prompt is added.
   If startup fails, the error is reported and the preference is retained for
   the next launch. Turning the switch off also disables automatic startup.
   Configurations created before this setting existed default to off.

Only one enabled rule is allowed per executable match. Click the **Enabled** or
**Children** checkbox directly in the rule table to save that flag immediately,
without saving other unfinished edits in the form. Saving, deleting, or toggling
a rule while routing is enabled reapplies the rules immediately; restart selected
apps afterward to establish new connections. Removing/disabling the last active
rule switches routing off when running as Administrator. Without administrator
rights, rule edits are still saved, but do not start routing or change the saved
global enabled setting. The window combines the administrator requirement and
app-restart guidance in one paragraph.
The window has no log/status display; failures use v2rayN's normal notifications.
Existing v2rayN/proxy-core
executables cannot be selected, to avoid routing loops.

### Child processes

**Include child processes** is off by default and saved per rule. A process
matching that rule and its descendants use the same destination. A child's own
enabled rule takes priority; otherwise the closest ancestor with child routing
enabled supplies its route. v2rayN/proxy cores and their descendants remain
excluded, even when an ancestor matches, to prevent routing loops.

Read-only Windows process/socket snapshots refresh on a background worker,
normally after 100 ms or sooner for unresolved new traffic. Refresh bursts are
coalesced with at least 25 ms between completed reads. Creation times distinguish
reused process IDs. Retained process handles supply exit times, so observed
children can keep their inherited route after the parent exits. The graph keeps
live ancestry and up to 2048 recent exited process records, plus their ancestry.
The implementation uses Microsoft's [Toolhelp process entries](https://learn.microsoft.com/en-us/windows/win32/api/tlhelp32/ns-tlhelp32-processentry32w)
and [process timing API](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes).

An inaccessible parent, a launcher that exited before routing observed it, or
an intermediate helper whose entire lifetime falls between snapshots cannot be
reliably reconstructed. Such children need an explicit executable rule. Restart
the target app after enabling routing or editing rules; observed ancestry is retained when
rules are reapplied. This follows Windows parent-process relationships, not
application/package membership or processes launched indirectly by system brokers.

### Destinations

| Selection | Behavior |
| --- | --- |
| Active profile (default) | Uses the main SOCKS listener on loopback, including the main client's current profile, routing rules and UDP settings. The current configured local SOCKS port is resolved for each new TCP connection or UDP association. No additional core is started. |
| Saved profile | Starts an isolated Xray instance for the selected profile, with an authenticated loopback SOCKS5 listener and UDP enabled. Applications selecting the same profile and blocking option share that instance. The selected profile must be supported by Xray; custom full configuration files are excluded. |
| SOCKS5 | Connects to the supplied host/port using no authentication or username/password. The server must support UDP ASSOCIATE for UDP/QUIC and IPv6 destinations for IPv6 traffic. |
| Network interface | Creates outbound TCP/UDP sockets bound to an address and index of the selected adapter. A missing/down adapter or unavailable address family fails the route; it does not retry using the default adapter. |

The saved-profile route sends its application traffic to that profile instead of
applying the main client's destination-based routing rules. It preserves the
profile's generated outbound/chain settings. **Apply blocking rules**, available
only for saved profiles and off by default, copies the enabled traffic rules
whose outbound is `block` from the currently selected routing rule-set. These
rules run before the selected profile's fallback route. Direct, proxy, other
outbound and DNS-only rules are excluded, so even a direct rule earlier in the
original rule-set does not exempt traffic from the copied blocking rules.

Normal core reload also checks effective saved-profile, chain, transport, DNS and
blocking configuration changes. Replacement cores are prepared and authenticated
before applying changes. Unchanged cores and routes are reused, and failed
preparation leaves the previous runtime intact. The capture engine stays open
during a successful rule update; only changed routes are retired.
As with other routing changes, restart the selected apps to establish fresh flows.
For domain/protocol matching, the isolated listener enables HTTP/TLS/QUIC sniffing
for routing only; it keeps the original destination IP. Names hidden by encryption
or protocols that cannot be sniffed cannot match domain rules. Other rule conditions
are preserved, including inbound tags (the isolated listener uses `socks`). Rules
requiring the original process identity are subject to the SOCKS core's limitations:
it receives the relay's connections, not the app's original sockets.

The main client's active profile,
system proxy settings and core lifecycle are not changed by application routing.
The isolated Xray configuration allows UDP port 443 through XUDP so QUIC is not
rejected by the main client's default multiplexing policy.
SOCKS5 credentials are stored in the application's normal configuration file;
password masking in the editor does not encrypt that file.

## Scope and limitations

- This is outbound TCP/UDP application routing, not a firewall or kill switch.
  Stopping it, exiting v2rayN, or losing the driver returns applications to the
  normal Windows route. Attributed traffic has no direct fallback while its
  configured relay is active and failing.
- Loopback traffic is excluded. Windows DNS requests made by a shared system
  service cannot be attributed to the calling executable and retain their
  normal route. Applications' own non-loopback DNS sockets follow their rules.
- Process attribution uses Windows TCP/UDP owner tables. Shared UDP ports with
  multiple owners are ambiguous and blocked when an owner has a selected rule.
  A new TCP connection refreshes ownership before choosing its route. Initial
  sequence numbers distinguish a reconnect from a retransmitted SYN while the
  previous relay is still closing.
  UDP sessions check the latest ownership index before sending or delivering
  replies and are discarded after an observed ownership change. Failed associations can be retried on the next datagram.
  Missing or stale ownership is held for up to 250 ms on retries, within a
  512-packet/4 MiB budget, then dropped with a throttled notice. It is not treated
  as a proven unselected application; under load or inaccessible ownership, this
  can also drop otherwise unselected traffic.
  Owner-table sampling still has a race with process/socket teardown; this is
  not a security boundary. Native port-reuse stress testing remains necessary.
- SOCKS5 UDP fragments (`FRAG != 0`) are unsupported; IP fragments are handled
  separately. Known unselected traffic bypasses reassembly after its
  first fragment is classified; later parts use a bounded bypass index. Selected
  or unresolved traffic, fragmented SYNs and reflected TCP replies still need
  assembly. Overlapping, incomplete, expired or excessive assemblies are dropped.
  Reassembly is bounded to 256 assemblies, 16 MiB and 15 seconds per assembly.
  Out-of-order fragments without a classifiable first fragment can still wait
  or hit those limits. Passed-through traffic keeps its original fragments.
- A UDP process/local endpoint/rule shares one socket or SOCKS association across
  remote peers, preserving its outbound source port for that session. Replies use
  their actual source address/port. Wildcard sockets using different local addresses
  or IP families can still have multiple sessions; this is not a kernel socket ID.
- Relay connections are bounded to 2048 TCP and 2048 UDP sessions. UDP queues
  hold at most 64 datagrams and 64 KiB of payload per session, and drop excess
  traffic without blocking packet capture; idle UDP sessions expire
  after 60 seconds. These limits protect memory and do not promise zero loss.
  A datagram that exceeds the outbound socket's size limit is dropped and reported
  without closing its UDP association. SOCKS framing reduces the available payload
  size; application datagrams are not split into SOCKS5 fragments.
  Connection resets during TCP accept are recoverable; a fatal capture/listener
  or maintenance-worker failure, or an unexpected isolated Xray exit, stops and
  cleans up the runtime and reports the error. There is no automatic retry loop;
  the saved enabled preference remains intact for the next launch.
- ICMP, raw IP protocols, inbound servers, multicast/broadcast discovery and
  shared-service traffic are outside the supported application-routing scope.
- Both x64 and x86 Windows builds include application routing. The x86 build can
  run on 32-bit Windows or under WOW64 on x64 Windows. ARM64 interception is not
  provided. Linux/macOS retain their existing behavior and hide the menu item.
- A machine-wide capture lease prevents two copies of this feature from owning
  interception simultaneously. A competing start fails without signalling or
  stopping the existing owner. It does not coordinate third-party filters.
- Interactions with other WinDivert/WFP filters, VPNs and security products must
  be tested separately. Do not use the existing production v2rayN installation
  as a native integration-test environment.

## Build and package

Use the .NET 10.0.1xx SDK required by the project and initialize submodules:

```powershell
git submodule update --init --recursive
powershell -File scripts/Get-WinDivert.ps1 -CurlPath C:\Path\To\curl.exe
dotnet build v2rayN/v2rayN.slnx -c Release -p:EnableWindowsTargeting=true --disable-build-servers
dotnet publish v2rayN/v2rayN/v2rayN.csproj -c Release -r win-x64 -p:SelfContained=true -o artifacts/windows-win-x64 --disable-build-servers
dotnet publish v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj -c Release -r win-x64 -p:SelfContained=true -o artifacts/windows-win-x64-desktop --disable-build-servers
dotnet test --project v2rayN/ServiceLib.Tests -c Release -- --report-trx
```

For 32-bit packages, use `-r win-x86` and separate output folders for both UIs.
The Windows CI test matrix runs self-contained x64 and x86 test hosts, including
native process/owner-table tests. Under WOW64, the child-process fixture also
checks that a 32-bit host can identify and match a 64-bit child.

Run these commands sequentially. Building and running the managed tests does not
load WinDivert or require Windows administrator privileges. Activating application
routing loads the driver and requires running v2rayN as Administrator.

`Get-WinDivert.ps1` downloads the pinned official WinDivert **2.2.2-A** archive,
verifies SHA-256
`63CB41763BB4B20F600B6DE04E991A9C2BE73279E317D4D82F237B150C5F3F15`, and verifies
the driver signatures. It copies dependencies into ignored `v2rayN/WinDivert`.
`Directory.Build.targets` includes the correct native files in Windows publishes,
outside the single-file executable. The x64 package requires the x64
`WinDivert.dll`, `WinDivert64.sys` and `WinDivert-LICENSE.txt` beside `v2rayN.exe`.
The x86 package includes the x86 DLL and both `WinDivert32.sys` and
`WinDivert64.sys`; driver selection follows the OS, not the app's bitness.
The shared x64 and dedicated x86 release workflows prepare these dependencies
before publishing either UI variant. Local builds can run the same download script;
`-CurlPath` accepts an alternative HTTPS-capable curl installation when needed.

The download script does not load/install a running driver. Keep the upstream
WinDivert license with distributed binaries and retain the corresponding source
and licensing references. Source builds also need separately packaged Xray/core
assets for saved-profile routing, just like ordinary v2rayN release packaging.
Explicit SOCKS5 and interface routes do not start an Xray process.

## Implementation and validation

For a source-by-source explanation of configuration, process matching, TCP/UDP
traffic flow, resource ownership, and test coverage, see the
[code walkthrough for reviewers](application-routing-code-review.md).

`AppRoutingManager` supervises a staged `RouteRuntime`, which owns one persistent
engine, its exclusive capture lease, and isolated profile instances. Each profile
instance owns its process, lifetime job and temporary configuration. `AppRouteEngine` reflects
selected TCP connections into local listeners, using a complete five-tuple and
independent translated port for each connection. UDP sessions retain their
SOCKS5 control channel, relay datagrams and inject replies into the original
application flow. Interface sockets set `IP_UNICAST_IF`/`IPV6_UNICAST_IF` and bind
the selected interface's address. IPv6 link-local addresses retain their scope.

Automated tests cover the 80-byte WinDivert address ABI, packet bounds and
rewriting, IPv4/IPv6 fragments, TCP tuple collisions/reconnections, owner matching,
mixed packet batches, maximum packet sizes, partial-batch flushing and UDP buffer ownership,
SOCKS5 authentication and split replies (including domain bind addresses),
cancellation during each handshake stage, shutdown overlapping a final connection,
UDP multi-peer framing/association/cleanup, missing-interface failure, staged
replacement/rollback, core/engine supervision, exclusive ownership and indexed
attribution. Socket fixtures are loopback-only and never load
WinDivert. Existing core/config tests remain in the full test suite.

Before declaring native support verified, use a Windows VM or dedicated test
host with two adapters and an IPv6-capable test destination. For each route type,
check TCP, UDP/QUIC and both IP versions with packet captures at the destination
and host. Include simultaneous selected/unselected executables, identical source
ports, helper processes, reconnect/port reuse, adapter loss, proxy failure,
fragmentation, start/stop/exit and both UI variants. Confirm that unselected
traffic and a separate v2rayN installation remain unaffected. No such privileged
test has been run on the developer's production machine.

References:

- [WireShift architecture reference](https://github.com/Farerudesu/WireShift)
- [WinDivert documentation and limitations](https://reqrypt.org/windivert-doc.html)
- [WinDivert source and license](https://github.com/basil00/WinDivert/tree/v2.2.2)
- [SOCKS5, RFC 1928](https://www.rfc-editor.org/rfc/rfc1928)
- [Windows IPv4 socket options](https://learn.microsoft.com/en-us/windows/win32/winsock/ipproto-ip-socket-options)
- [Windows IPv6 socket options](https://learn.microsoft.com/en-us/windows/win32/winsock/ipproto-ipv6-socket-options)
