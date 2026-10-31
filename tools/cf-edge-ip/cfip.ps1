# ============================================================
#  CF 边缘 IP 优选 -> 生成 v2rayN 节点链接 -> 写入剪贴板
#
#  放在 v2rayN 目录里运行（和 v2rayN.exe 同级），
#  脚本用相对路径找配置，换电脑、换盘符、搬目录都不用改。
#
#  用法：双击同目录下的 cfip.bat
# ============================================================

# ---- 可调参数 ----
$Tries      = 3     # 每个 IP 测几次
$TimeoutSec = 5     # 单次超时（秒），线路差可以调到 8
$Top        = 3     # 生成几个节点
# ------------------

$ErrorActionPreference = 'Continue'   # curl 超时会写 stderr，Stop 会让脚本直接退出
$TotalStep = 5

# ==================== 小工具 ====================

function Write-Step([int]$n, [string]$msg) {
    Write-Host ""
    Write-Host ("[{0}/{1}] {2}" -f $n, $TotalStep, $msg) -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host ("      OK  " + $msg) -ForegroundColor Green
}

function Write-Note([string]$msg) {
    Write-Host ("          " + $msg) -ForegroundColor DarkGray
}

function Write-Warn([string]$msg) {
    Write-Host ("      !   " + $msg) -ForegroundColor Yellow
}

function Stop-WithError([string]$what, [string]$fix) {
    Write-Host ""
    Write-Host ("      !!  " + $what) -ForegroundColor Red
    if ($fix) { Write-Host ("      怎么办：" + $fix) -ForegroundColor Yellow }
    Wait-Exit
    exit 0
}

function Wait-Exit {
    Write-Host ""
    try { Read-Host "按回车键关闭窗口" | Out-Null } catch { }
}

function Enc([string]$s) { if ($s) { [uri]::EscapeDataString($s) } else { '' } }

# ==================== 开始 ====================

Write-Host ""
Write-Host "==============================================" -ForegroundColor White
Write-Host "  CF 优选 IP  ->  v2rayN 节点生成器" -ForegroundColor White
Write-Host "==============================================" -ForegroundColor White
Write-Host "  说明：开不开系统代理都不影响本脚本。" -ForegroundColor DarkGray
Write-Host "        它用 curl --resolve + --noproxy 直连 CF 边缘测速，" -ForegroundColor DarkGray
Write-Host "        刻意绕开代理——走代理测出来的是代理自己，不是这条线的质量。" -ForegroundColor DarkGray
Write-Host "  注意：运行结束会覆盖当前剪贴板内容。" -ForegroundColor DarkGray

if ($Tries -lt 1) { $Tries = 1 }
if ($Top -lt 1) { $Top = 1 }

# ---------- 步骤 1：定位 v2rayN ----------
Write-Step 1 "正在定位 v2rayN 目录..."

$v2rayDir = $null
$foundBy = ''

# 1) 脚本自己所在的目录（推荐：脚本就放在 v2rayN 里）
if ($PSScriptRoot) {
    foreach ($f in @('v2rayN.exe', 'binConfigs\config.json', 'guiConfigs\guiNConfig.json')) {
        if (Test-Path (Join-Path $PSScriptRoot $f)) { $v2rayDir = $PSScriptRoot; $foundBy = '脚本所在目录'; break }
    }
    # 2) 脚本上一级目录（脚本放在子文件夹里也能用）
    if (-not $v2rayDir) {
        $up = Split-Path $PSScriptRoot -Parent
        if ($up -and (Test-Path (Join-Path $up 'v2rayN.exe'))) { $v2rayDir = $up; $foundBy = '脚本的上一级目录' }
    }
}

# 3) 正在运行的 v2rayN 进程（脚本被挪到别处时兜底）
if (-not $v2rayDir) {
    $proc = Get-Process v2rayN -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc -and $proc.Path) { $v2rayDir = Split-Path $proc.Path; $foundBy = '运行中的 v2rayN 进程' }
}

if (-not $v2rayDir) {
    Stop-WithError "找不到 v2rayN 目录" "把这个脚本放进 v2rayN 文件夹（和 v2rayN.exe 放一起）再运行"
}
Write-Ok ("v2rayN 目录：" + $v2rayDir)
Write-Note ("（来源：" + $foundBy + "）")

# ---------- 步骤 2：读取当前节点参数 ----------
Write-Step 2 "正在读取当前生效的节点参数..."

if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) {
    Stop-WithError "系统里没有 curl.exe" "Windows 10 1803 以上自带；没有的话装一个，或换台机器"
}

$cfgPath = $null
foreach ($rel in @('binConfigs\config.json', 'guiConfigs\config.json', 'config.json')) {
    $cand = Join-Path $v2rayDir $rel
    if (Test-Path $cand) { $cfgPath = $cand; break }
}
if (-not $cfgPath) {
    Stop-WithError "找不到 xray 配置文件（binConfigs\config.json）" "先启动一次 v2rayN，让它生成配置，再运行本脚本"
}
Write-Ok ("配置文件：" + (Split-Path $cfgPath -Leaf))
Write-Note ("配置时间：" + (Get-Item $cfgPath).LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') + "（v2rayN 每次切换节点都会重写它，时间太旧说明读的是老配置）")

# TUN 模式会把 curl 的流量也塞进 xray，测出来的是代理链的耗时，不是直连 CF 边缘的耗时
$guiCfg = Join-Path $v2rayDir 'guiConfigs\guiNConfig.json'
if (Test-Path $guiCfg) {
    try {
        $gui = Get-Content $guiCfg -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($gui.TunModeItem -and $gui.TunModeItem.EnableTun) {
            Write-Warn "检测到 v2rayN 正开着 TUN 模式"
            Write-Note "TUN 会接管全部流量，测速会绕进代理，结果偏慢甚至失真"
            Write-Note "建议：先关掉 TUN（或用系统代理模式）再跑本脚本"
        }
    } catch { }
}

try {
    $cfg = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
} catch {
    Stop-WithError "配置文件读不出来（可能正在被写入）" "等 5 秒重试；还不行就重启一次 v2rayN"
}

# 找真正的代理出站：优先 tag=fragment-proxy，否则找任意带 TLS 的出站
$ob = $cfg.outbounds | Where-Object { $_.tag -eq 'fragment-proxy' } | Select-Object -First 1
if (-not $ob) {
    $ob = $cfg.outbounds | Where-Object {
        $_.streamSettings -and ($_.streamSettings.tlsSettings -or $_.streamSettings.realitySettings)
    } | Select-Object -First 1
}
if (-not $ob) {
    Stop-WithError "当前节点不是 TLS 节点（没有 SNI）" "先在 v2rayN 里选中一个走 TLS 的节点，再运行本脚本"
}

$proto = [string]$ob.protocol
$ss    = $ob.streamSettings
$s     = $ob.settings

# --- 服务器地址 / 端口 / 凭据：兼容三种写法 ---
#   1) vnext 数组   {"vnext":[{"address":..,"port":..,"users":[{"id":..}]}]}
#   2) servers 数组 {"servers":[{"address":..,"port":..,"method":..,"password":..}]}
#   3) 扁平写法     {"address":..,"port":..,"id":..}
$ep = $null
$usr = $null
if ($s.vnext) {
    $ep = @($s.vnext)[0]
    if ($ep -and $ep.users) { $usr = @($ep.users)[0] }
} elseif ($s.servers) {
    $ep = @($s.servers)[0]
    $usr = $ep
} else {
    $ep = $s
    $usr = $s
}
if (-not $ep) { Stop-WithError "节点配置里读不到服务器信息" "重新导入一次这个节点，或换一个节点" }

$serverAddr = [string]$ep.address
if (-not $serverAddr) { $serverAddr = [string]$s.address }

$serverPort = 0
$tmpPort = 0
foreach ($p in @($ep.port, $s.port)) {
    if ($p -and [int]::TryParse([string]$p, [ref]$tmpPort) -and $tmpPort -gt 0 -and $tmpPort -le 65535) { $serverPort = $tmpPort; break }
}
if (-not $serverPort) { $serverPort = 443 }

$user = $null
foreach ($k in @('password', 'id', 'uuid', 'user')) {
    if ($usr -and $usr.$k) { $user = [string]$usr.$k; break }
}

# --- TLS / Reality ---
$tls     = $ss.tlsSettings
$reality = $ss.realitySettings
$security = ''
$sni = ''; $fp = ''; $ech = ''; $alpn = ''
$pbk = ''; $sid = ''; $spx = ''
if ($reality) {
    $security = 'reality'
    $sni = [string]$reality.serverName
    $fp  = [string]$reality.fingerprint
    $pbk = [string]$reality.publicKey
    $sid = [string]$reality.shortId
    $spx = [string]$reality.spiderX
} elseif ($tls) {
    $security = 'tls'
    $sni = [string]$tls.serverName
    $fp  = [string]$tls.fingerprint
    $ech = [string]$tls.echConfigList
    if ($tls.alpn) { $alpn = (@($tls.alpn) -join ',') }
}
if (-not $sni) { $sni = [string]$ss.serverName }
if (-not $sni) {
    Stop-WithError "当前节点没有 SNI，没法做 IP 优选" "选中一个带 SNI 的 TLS 节点（CF Worker 节点都有）"
}

# --- 传输方式：把存在的字段原样带过去，不猜结构 ---
$network = [string]$ss.network
if (-not $network) { $network = 'tcp' }
$netAlias = if ($network -eq 'raw') { 'tcp' } else { $network }

$tHost = ''; $tPath = ''; $tSvc = ''; $tMode = ''; $tHeader = ''
foreach ($k in @('wsSettings', 'httpupgradeSettings', 'xhttpSettings', 'grpcSettings', 'rawSettings', 'tcpSettings', 'kcpSettings')) {
    $ts = $ss.$k
    if (-not $ts) { continue }
    if (-not $tHost   -and $ts.host)        { $tHost = [string]$ts.host }
    if (-not $tPath   -and $ts.path)        { $tPath = [string]$ts.path }
    if (-not $tSvc    -and $ts.serviceName) { $tSvc = [string]$ts.serviceName }
    if (-not $tMode   -and $ts.mode)        { $tMode = [string]$ts.mode }
    if (-not $tHeader -and $ts.header -and $ts.header.type) { $tHeader = [string]$ts.header.type }
    if (-not $tMode -and $ts.multiMode -eq $true) { $tMode = 'multi' }   # grpc 的 multiMode 是 bool，链接里要写 mode=multi
}

$flow = ''
if ($s.flow) { $flow = [string]$s.flow }
elseif ($usr -and $usr.flow) { $flow = [string]$usr.flow }

# --- vmess 专用字段 ---
$alterId = 0
$tmpId = 0
if ($usr -and $usr.alterId -and [int]::TryParse([string]$usr.alterId, [ref]$tmpId)) { $alterId = $tmpId }
elseif ($s.alterId -and [int]::TryParse([string]$s.alterId, [ref]$tmpId)) { $alterId = $tmpId }

$scy = ''
if ($usr -and $usr.security) { $scy = [string]$usr.security }
if (-not $scy -and $s.security) { $scy = [string]$s.security }
if (-not $scy) { $scy = 'auto' }

$vlessEnc = ''
if ($usr -and $usr.encryption) { $vlessEnc = [string]$usr.encryption }
if (-not $vlessEnc -and $s.encryption) { $vlessEnc = [string]$s.encryption }
if (-not $vlessEnc) { $vlessEnc = 'none' }

# --- 协议支持检查 ---
if ($proto -notin @('trojan', 'vless', 'vmess')) {
    Stop-WithError ("这个节点的协议是「" + $proto + "」，本脚本暂不支持") `
        "只支持 trojan / vless / vmess（CF Worker 最常用的三种）。换一个这类节点再运行"
}
if (-not $user) {
    Stop-WithError ("读到了节点但没读到密码 / UUID（协议 " + $proto + "）") "重新导入一次这个节点，或换一个节点"
}

Write-Ok ("协议 " + $proto + "    服务器 " + $serverAddr + ":" + $serverPort)
Write-Ok ("SNI  " + $sni)
Write-Note ("传输 " + $netAlias + $(if ($tHost) { "    Host " + $tHost } else { '' }) + $(if ($tPath) { "    Path " + $tPath } else { '' }))
Write-Note ("加密 " + $(if ($security) { $security } else { '(无)' }) + "    ECH " + $(if ($ech) { $ech } else { '(未启用)' }))
if ($security -eq 'reality') {
    Write-Warn "这是 Reality 节点，本脚本的测速方式对它不准，结果仅供参考"
}

# ---------- 步骤 3：收集候选 IP ----------
Write-Step 3 "正在收集候选 IP..."

# 从 CIDR 里随机取一个 IP（只支持 IPv4）
function Get-RandomIpFromCidr([string]$cidr) {
    $parts = $cidr.Split('/')
    if ($parts.Count -ne 2) { return $null }
    $n = 0
    if (-not [int]::TryParse($parts[1], [ref]$n)) { return $null }
    if ($n -lt 8 -or $n -gt 32) { return $null }
    $addr = [System.Net.IPAddress]::Any
    if (-not [System.Net.IPAddress]::TryParse($parts[0], [ref]$addr)) { return $null }
    $o = $addr.GetAddressBytes()
    if ($o.Length -ne 4) { return $null }
    $base = [long]$o[0] * 16777216 + [long]$o[1] * 65536 + [long]$o[2] * 256 + [long]$o[3]
    $size = [long][Math]::Pow(2, 32 - $n)
    if ($size -lt 3) { return $null }
    $v = $base + [long](Get-Random -Minimum 1 -Maximum ($size - 1))
    return ('{0}.{1}.{2}.{3}' -f [int](($v -shr 24) -band 255), [int](($v -shr 16) -band 255), [int](($v -shr 8) -band 255), [int]($v -band 255))
}

$cands = New-Object System.Collections.Generic.List[object]
function Add-Cand([string]$ip, [bool]$fast) {
    if (-not $ip) { return }
    if ($cands | Where-Object { $_.Ip -eq $ip }) { return }
    $cands.Add([pscustomobject]@{ Ip = $ip; Fast = $fast })
}

# ---- B：内置兜底列表（写死）----
# 2026-10-30 在本机联通网络实测 3/3 通过的 IP。换运营商/换网络后可能失效，
# 所以它只是兜底，不是唯一来源。
@(
    '104.17.145.150', '104.17.157.215', '104.17.128.76',
    '104.18.119.48',
    '172.64.147.34', '172.65.90.20'
) | ForEach-Object { Add-Cand $_ $false }
$builtinCount = $cands.Count

# ---- B2：从实测最好的 /18 里多采几个 ----
# 104.17.128.0/18 实测：8 个采样全部可达，其中 6 个 3/3，延迟 516-650ms。
# 这是目前找到的命中率最高的段，所以重点采它。
$goldCount = 0
for ($i = 0; $i -lt 6; $i++) {
    $ip = Get-RandomIpFromCidr '104.17.128.0/18'
    if ($ip -and -not ($cands | Where-Object { $_.Ip -eq $ip })) { Add-Cand $ip $false; $goldCount++ }
}

# ---- A：拉官方段列表，每段随机采一个（探索新机会）----
# 间歇性阻断下单次拉取会假失败，所以连拉 3 次，都失败才用内置列表兜底。
$segs = @()
$dynCount = 0
$pullOk = 0
for ($attempt = 1; $attempt -le 3; $attempt++) {
    $raw = & curl.exe -sS --noproxy '*' --max-time 10 https://www.cloudflare.com/ips-v4 2>$null
    if ($raw) {
        $segs = @($raw -split "[\r\n]+" | ForEach-Object { $_.Trim() } |
            Where-Object { $_ -match '^\d{1,3}(\.\d{1,3}){3}/\d{1,2}$' })
    }
    if ($segs.Count -gt 0) { $pullOk = $attempt; break }
    if ($attempt -lt 3) {
        Write-Note ("第 " + $attempt + " 次拉取官方段列表失败，1 秒后重试...")
        Start-Sleep -Seconds 1
    }
}
if ($segs.Count -gt 0) {
    foreach ($seg in $segs) {
        if ($seg -eq '104.16.0.0/13') { continue }   # 已由上面的 /18 重点采过，不重复
        $ip = Get-RandomIpFromCidr $seg
        if ($ip -and -not ($cands | Where-Object { $_.Ip -eq $ip })) { Add-Cand $ip $true; $dynCount++ }
    }
    Write-Ok ("官方段列表：" + $segs.Count + " 个段（第 " + $pullOk + " 次拉到），额外采样 " + $dynCount + " 个")
} else {
    Write-Warn "连拉 3 次都拿不到 Cloudflare 官方段列表，改用内置列表兜底"
    Write-Note "（cloudflare.com 不通或超时；不影响使用，只是少了探索性的候选）"
}

# ---- 域名当前解析出的 ----
$domainCount = 0
foreach ($d in @($sni, $tHost, $serverAddr)) {
    if (-not $d) { continue }
    if ($d -match '^\d+\.\d+\.\d+\.\d+$') {   # 本身已经是 IP，直接当候选
        if (-not ($cands | Where-Object { $_.Ip -eq $d })) { Add-Cand $d $false; $domainCount++ }
        continue
    }
    try {
        Resolve-DnsName $d -Type A -DnsOnly -ErrorAction Stop |
            Where-Object { $_.IPAddress } | ForEach-Object {
                $a = $_.IPAddress
                if ($a -and -not ($cands | Where-Object { $_.Ip -eq $a })) { Add-Cand $a $false; $domainCount++ }
            }
    } catch { }
}
Write-Ok ("共 " + $cands.Count + " 个（内置 " + $builtinCount + " + 优先段 " + $goldCount + " + 动态 " + $dynCount + " + 域名 " + $domainCount + "）")

# ---------- 步骤 4：实测 ----------
Write-Step 4 ("正在实测 " + $cands.Count + " 个 IP，每个 " + $Tries + " 次...")
Write-Note "成功 = 用你的真实 SNI 完成 TLS 握手 + HTTP 响应真的回来（只看握手会漏掉「握手过了数据卡住」）"
Write-Note "只认「全部成功」的，能通但很慢的会被淘汰"
Write-Note "直连测试，不走系统代理（走代理就测不出这条线的真实质量）"
Write-Note "标「探」的是从官方段列表随机采样来的，失败一次就放弃，省时间"
Write-Host ""

$testPath = if ($tPath) { $tPath } else { '/' }
if (-not $testPath.StartsWith('/')) { $testPath = '/' + $testPath }

$results = @()
$idx = 0
foreach ($c in $cands) {
    $idx++
    $ok = 0
    $lats = @()
    for ($i = 0; $i -lt $Tries; $i++) {
        $out = & curl.exe -sS -k --ssl-no-revoke --noproxy '*' --connect-timeout 3 `
            --resolve "${sni}:${serverPort}:$($c.Ip)" `
            --max-time $TimeoutSec -o NUL -w "%{http_code};%{time_appconnect};%{time_total}" `
            "https://${sni}:${serverPort}${testPath}" 2>$null
        # 必须 TLS 握手成功 且 HTTP 响应真的回来（断流的典型表现就是握手过了但数据卡住）
        if ($out -match '^(\d{3});([\d.]+);([\d.]+)' -and $Matches[1] -ne '000' -and [double]$Matches[2] -gt 0) {
            $ok++
            $lats += [double]$Matches[3]
        } elseif ($c.Fast) {
            break   # 探索来的 IP 失败一次就放弃，剩余次数不浪费
        }
    }
    $avg = if ($lats.Count) { [int]((($lats | Measure-Object -Average).Average) * 1000) } else { 0 }
    $results += [pscustomobject]@{ Ip = $c.Ip; Ok = $ok; AvgMs = $avg }

    $src = if ($c.Fast) { '探' } else { '  ' }
    $mark = if ($ok -eq $Tries) { '  GOOD' } elseif ($ok -gt 0) { '  slow' } else { '  FAIL' }
    $color = if ($ok -eq $Tries) { 'Green' } elseif ($ok -gt 0) { 'Yellow' } else { 'DarkGray' }
    Write-Host ("      [{0,2}/{1}] {2} {3,-16} {4}/{5} {6,6}ms{7}" -f $idx, $cands.Count, $src, $c.Ip, $ok, $Tries, $avg, $mark) -ForegroundColor $color
}

$good = @($results | Where-Object { $_.Ok -eq $Tries } | Sort-Object AvgMs)
if (-not $good) {
    Write-Host ""
    Write-Warn "没有 IP 全部通过，放宽到「至少成功一次」"
    $good = @($results | Where-Object { $_.Ok -gt 0 } | Sort-Object @{ Expression = { $_.Ok }; Descending = $true }, AvgMs)
}
if (-not $good) {
    Stop-WithError "全部 IP 都连不上" "最常见的原因是：这个节点不在 Cloudflare 后面（比如自建直连、其他 CDN、订阅里的非 CF 节点），CF 边缘 IP 对它无效。其次才是 SNI / 端口写错，或这条线现在确实不通"
}

$picked = @($good | Select-Object -First $Top)
Write-Ok ("筛出 " + $good.Count + " 个可用，取前 " + $picked.Count + " 个")

# ---------- 步骤 5：生成链接并写入剪贴板 ----------
Write-Step 5 "正在生成分享链接并写入剪贴板..."

$links = @()
foreach ($r in $picked) {
    $remark = "优选IP[$($r.Ip) $($r.AvgMs)ms]"

    if ($proto -eq 'vmess') {
        # vmess 用 base64(JSON)，字段名按 v2rayN 的 VmessQRCode
        $typeField = ''
        $hostField = $tHost
        $pathField = $tPath
        switch ($network) {
            'xhttp' { $typeField = $tMode; $pathField = $tPath }
            'grpc' { $typeField = $tMode; $pathField = $tSvc }
            default {
                if ($network -in @('tcp', 'raw', 'kcp')) {
                    $typeField = if ($tHeader) { $tHeader } else { 'none' }
                }
            }
        }
        $vm = [ordered]@{
            v    = 2
            ps   = $remark
            add  = $r.Ip
            port = $serverPort
            id   = $user
            aid  = $alterId
            scy  = $scy
            net  = $netAlias
            type = $typeField
            host = $hostField
            path = $pathField
            tls  = $security
            sni  = $sni
            alpn = $alpn
            fp   = $fp
        }
        $json = $vm | ConvertTo-Json -Compress
        $links += 'vmess://' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
        continue
    }

    $q = @()
    if ($security)      { $q += 'security=' + $security }
    if ($proto -eq 'vless') { $q += 'encryption=' + (Enc $vlessEnc) }
    if ($flow)          { $q += 'flow=' + (Enc $flow) }
    if ($sni)           { $q += 'sni=' + (Enc $sni) }
    if ($fp)            { $q += 'fp=' + (Enc $fp) }
    if ($alpn)          { $q += 'alpn=' + (Enc $alpn) }
    if ($pbk)           { $q += 'pbk=' + (Enc $pbk) }
    if ($sid)           { $q += 'sid=' + (Enc $sid) }
    if ($spx)           { $q += 'spx=' + (Enc $spx) }
    if ($ech)           { $q += 'ech=' + (Enc $ech) }
    $q += 'type=' + $netAlias
    if ($tHost)         { $q += 'host=' + (Enc $tHost) }
    if ($tPath)         { $q += 'path=' + (Enc $tPath) }
    if ($tSvc)          { $q += 'serviceName=' + (Enc $tSvc) }
    if ($tMode)         { $q += 'mode=' + (Enc $tMode) }
    if ($tHeader)       { $q += 'headerType=' + (Enc $tHeader) }

    $links += ('{0}://{1}@{2}:{3}?{4}#{5}' -f $proto, (Enc $user), $r.Ip, $serverPort, ($q -join '&'), (Enc $remark))
}

if ($proto -eq 'vmess' -and $ech) {
    Write-Warn "vmess 链接格式里没有 ECH 字段，导入后这个节点的 ECH 会丢失"
    Write-Note "这是 vmess 链接格式本身的限制，不是脚本问题；介意的话用 trojan / vless 节点"
}

$clipText = $links -join "`r`n"
$clipOk = $false
try {
    Set-Clipboard -Value $clipText
    $clipOk = ((Get-Clipboard -Raw) -replace "`r`n", "`n").Trim() -eq ($clipText -replace "`r`n", "`n").Trim()
} catch { }
if ($clipOk) {
    Write-Ok ("已生成 " + $links.Count + " 个节点，剪贴板已更新")
} else {
    Write-Warn "剪贴板写入失败（可能有别的程序正占着剪贴板）"
    Write-Note "下面会打印链接原文，手动全选复制即可"
}

# ==================== 结果 ====================

Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host ("  完成！生成了 " + $links.Count + " 个节点") -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
foreach ($r in $picked) {
    Write-Host ("      {0,-16} {1,6}ms   成功 {2}/{3}" -f $r.Ip, $r.AvgMs, $r.Ok, $Tries) -ForegroundColor Green
}

Write-Host ""
Write-Host "  接下来怎么做：" -ForegroundColor Cyan
Write-Host "    1. 切到 v2rayN 窗口" -ForegroundColor White
Write-Host "    2. 点一下节点列表的空白处（别点在输入框里）" -ForegroundColor White
Write-Host "    3. 按 Ctrl+V，节点会自动导入" -ForegroundColor White
Write-Host "    4. 双击其中一个新节点，设为当前使用" -ForegroundColor White
Write-Host ""
Write-Host "  提示：这是「新增」，不会删掉你原来的节点。" -ForegroundColor DarkGray
Write-Host "        跑几次会积累多条，旧的自己删。" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  链接原文（剪贴板丢了就手动复制）：" -ForegroundColor DarkGray
$links | ForEach-Object { Write-Host ("    " + $_) -ForegroundColor DarkGray }

Wait-Exit
