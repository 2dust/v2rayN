# Native Web self-update 真实 E2E 报告

测试开始：2026-10-05；报告完成：2026-10-06（Asia/Shanghai）。仓库：`Nozilla-X/v2rayN`，分支：`web-api-pr`。

## 结论

**原始提交不能判定通过：真实回滚测试复现了 helper 崩溃。加入本次最小修复后，Linux x64 的正常升级、运行中 Core 恢复、health timeout 自动回滚全部通过，达到本次范围内可提交 PR 的标准。**

测试结束时，修复保留在工作区，尚未提交或推送；当时远端生产分支为 `635e36e84381fb70d09fcc2427c34ac4daeb52bf`。后续按用户要求提交的修复以 Git 历史为准。没有更改 URL、SHA-256、RID、build identity 或 archive boundary 校验。

## 环境与正式打包

- Fedora 44，Linux `7.2.8-200.fc44.x86_64`，x86_64；普通用户可写的临时安装目录。
- 实际部署为 native `linux-x64` single-file；`--foreground --no-open` 启动，API 确认为 `native-writable`。没有使用 container/systemd 部署。
- 管理监听均为 `127.0.0.1`，每轮独立端口、随机 Management Key，安装目录 `.env` 权限 `600`。
- 真实 Core：full ZIP 自带的 **Xray 26.9.30**。使用测试 SOCKS profile 连接已有本地代理，只用于真实 GitHub 网络访问；不修改正在使用的 v2rayN。
- Core stopped 轮先通过 API 选择 profile，再停止真实 Xray；下载期间使用独立 loopback TCP 转发到本地代理。它不是伪造的 Core，更新前后 tracked Core PID 都为空。
- 所有 Release 资产均由现有 `.github/workflows/build-web.yml` 生成：`publish-native.sh` → `fetch-core-bundle.sh` → `package-native.sh` → `web-update-manifest.py write/verify`。
- 每个已发布测试 Release 都上传了 x64/ARM64 full ZIP、app-only update ZIP 和 `web-update.json`。最终两版共 10 个资产逐一匹配 GitHub 的 SHA-256 digest/size。
- x64 update ZIP 为真实正式打包结果，只含 `v2rayN.Web` 和 `v2rayN.Web.build.json`，没有手工拼包。

## 版本与构建身份

| 用途 | 版本/tag | Commit |
| --- | --- | --- |
| 原始代码 E2E，安装版 | `7.25.90` | `635e36e84381fb70d09fcc2427c34ac4daeb52bf` |
| 原始代码 E2E，更新版 | `7.25.91` | `a5596efc12664580d446112d081c0d01aaf18e08` |
| 最终修复版 E2E，安装版 | `7.25.94` | `b552c915e0af05c9325a1d4cd537cf044582b1d3` |
| 最终修复版 E2E，更新版 | `7.25.95` | `ac3a034d98128e66999df6202bc5714aaee18772` |

每一对版本使用相同源码树、不同测试 commit/build identity；不同版本均完整重新构建。另有 `7.25.92`/`7.25.93` 中间修复构建，**未发布 Release、未用于最终 E2E**。

最终两份生产源文件与工作区修复逐 blob 匹配。最后新增的 progress 缓存回归测试在本地另行验证，不影响 native 包源码一致性。

最终两个正式 workflow 的全部 jobs 成功；补充本地回归 **322 passed / 0 failed**。这些不是 E2E 通过的替代证据。

## 实际更新链路

1. 从正式 full ZIP 安装并运行旧版；登录后 `/api/status` 确认旧版本、commit、RID 和 runtime 状态。
2. 第一轮在旧版实际运行后才发布第二版临时 Release。
3. `GET /api/web-updates/check?useProxy=true&preRelease=false`：应用访问真实 GitHub releases index，下载真实 `web-update.json`，识别新版版本和 commit。
4. `POST /api/web-updates/update?useProxy=true&preRelease=false`：公开 API 发起更新。
5. 应用下载真实 GitHub update ZIP；执行 SHA-256、size、RID、identity、archive boundary 校验，生成 candidate。
6. 旧 Web/Core 优雅停止、释放 instance lock；应用自行启动 detached helper，helper 替换两个 app 文件并启动新版。
7. helper 检查 `/api/health`、PID/锁所有权、版本及 runtime intent；客户端重新登录并检查 `/api/status` 和 `GET /api/core-updates/progress`。

没有直接调用 helper，也没有在 E2E 中手工替换 executable、修改 progress 文件或跳过任何校验。

## 最终三轮结果

三轮均从独立的 `7.25.94` 安装目录开始。

| 场景 | Web PID | Core PID | 最终身份 | 最终 progress | 更新耗时 |
| --- | --- | --- | --- | --- | --- |
| Core stopped 升级 | `156817 → 157146` | `[] → []` | `7.25.95 / ac3a034…` | `completed`, `success=true` | 8.869 秒 |
| Core running 升级 | `157317 → 157484` | `157353 → 157518` | `7.25.95 / ac3a034…` | `completed`, `success=true`, `coreWasRunning=true` | 11.104 秒 |
| 新版 health timeout 回滚 | `157385 → 157730` | `157436 → 157763` | 恢复 `7.25.94 / b552c915…` | `failed`, `success=false`, `rollbackSucceeded=true`, `coreWasRunning=true` | 71.580 秒 |

每轮均确认：

- `/api/health` 恢复正常；`/api/status` 的版本、commit、RID、build date 与正式包一致。
- 旧 Web 进程退出码为 `0`；新版/恢复版 Web PID 确实变化。
- running 两轮恢复原 profile 和 running 状态，而不是启动一个无关 Core。新 Core PID 也确实变化。
- instance lock 由当前 health PID 持有，非阻塞 `flock` 无法重复获取；启动第二实例返回 `73`。
- 无 candidate、backup、stage、plan、下载 ZIP、restore 临时文件残留；runtime intent 文件完成后删除。
- 测试进程最终优雅停止，无残留 Web/Core PID。

### 回滚故障注入

仍使用未经修改的正式 `7.25.95` update ZIP。等待新版实际加载 executable 并取得 instance lock 后，对该隔离新版进程 `157622` 发出 `SIGSTOP`，确认状态为 `T (stopped)`，使其无法完成 health check。

helper 自行等待 health timeout、终止失败候选、恢复旧版并重新启动。恢复后的 executable SHA-256 和 build identity 文件字节均与升级前完全一致；旧版 health、登录和真实 Xray runtime 恢复正常。测试脚本没有代替 helper 回滚或写入终态结果。

## 用户数据与 WebUI/Core 保留

三轮均通过同一组验证：

- **36 个文件逐文件 SHA-256 完全相同**：`.env`、`.env.example`、实际 `guiConfigs/guiNConfig.json`、用户配置 marker、用户非 app-only marker、静态资源 endpoints 文件、完整 `webui/` 和完整 `bin/`。
- WebUI 包含自定义 `index.html`、`assets/user.js`、嵌套中文文件；升级前后 HTTP 获取首页及两份 assets 的响应内容也完全相同。
- `bin/` 包含用户 marker、Xray/sing-box/mihomo executable、libcronet、GeoIP/GeoSite、MMDB/SRS 文件；全部 hash 保持不变。
- `guiNDB.db` **8 个用户表的逻辑行快照完全一致**；settings/profile API 内容保持一致，profile 选择不变。SQLite 文件自身可能因正常 journal/checkpoint 改变，未把数据库文件字节 hash 当作数据完整性的唯一标准。
- 原 `.env` 中的 Management Key 继续成功登录；没有要求旧的进程内 session token 跨重启存活。

逐文件清单和 SHA-256、SQLite 快照、API 响应、phase timeline 均保存在下述证据目录。

## 发现的问题与最小修复

### 1. 回滚原地覆盖 executable 导致 helper 崩溃（阻塞项）

原始 `7.25.90 → 7.25.91` health timeout 回滚两次复现 helper `SIGSEGV`。旧 app 文件已恢复，但 progress 卡在 `verifying-health`，candidate/backup 未清理，不能判为回滚成功。

`/proc/<helper>/maps` 证实 single-file helper 同时映射原始已 unlink 的 executable 和 swap 后的新 executable（两个不同 inode，包含后者的可执行/共享映射）。原 `RestorePreviousApp` 用 `File.Copy(..., overwrite: true)` 原地截断/覆盖新 inode，破坏了 helper 延迟加载的 bundled assembly 映射。内核日志和两份 systemd crashdump 对应 helper PIDs `152785`、`154192`。

**修复**：仅把回滚 app 文件复制到安装目录内的随机临时文件，设置 executable 权限，再通过同文件系统 `File.Move(..., overwrite: true)` 原子替换；identity 同样暂存替换，`finally` 清理临时文件。补充 memory-mapped 文件不被回滚修改的回归测试。修复后真实回滚 E2E 通过。

### 2. helper 未持久化更新前的 Core 状态

running 升级恢复成功，但最终 progress 错报 `coreWasRunning=false`。plan/progress state 未携带此字段，API 重读时硬编码 false。

**修复**：plan/state 增加兼容旧 JSON 的可选字段，helper 统一持久化，API 重读保留该值。补充旧格式兼容测试，最终 running 升级和回滚均返回 true。

### 3. helper 的中间 progress 被缓存成永久 in-flight 状态

API 若读到 helper 的未完成状态，原代码会将它放进本地 `_updateProgress`，后续因 `IsComplete=false` 直接返回缓存，不再读取 helper 最终结果。

**修复**：只缓存 helper 的 terminal progress，未完成状态继续从持久化文件读取。补充 `verifying-health → failed/rollbackSucceeded` 回归测试。

修改范围：两个生产文件、两个相关测试文件，没有大规模重构或测试后门。

准备过程另外遇到 GitHub release index 短暂缓存、以及空 profile 选择在启动时被归一化为默认 profile；最终基线使用明确选中后停止的 profile，并通过公开 check API 等待 Release 可见。这些没有通过修改生产行为来绕过。

## 未覆盖范围

- ARM64 实机 E2E；ARM64 仅正式 workflow 构建/打包校验通过。
- sing-box/mihomo 的 running runtime 恢复；它们的文件保留已验证，运行恢复只测了真实 Xray。
- systemd/container 自更新（当前不支持，未拿它们冒充 native E2E）。
- 浏览器中完整第三方 WebUI 操作流程；验证的是安装文件及真实静态 HTTP 内容。
- 断电、磁盘写满、恶意 ZIP、错误 SHA/RID、长期使用等其它失败模式；本次失败 E2E 专门覆盖下载/校验成功后的启动 health timeout。

## 清理与证据

- 已删除 fork 临时 Releases `7.25.90`、`7.25.91`、`7.25.94`、`7.25.95` 及对应 tags。
- 已删除仅构建的 tags `7.25.92`、`7.25.93`。
- 已删除本次 6 次 workflow runs 及其全部 artifacts；删除后查询本次 artifacts 为零，`7.25.9*` refs 为零。
- fork 原有 `7.25.3` Release 和 `web-api-pr` 分支未改动；`2dust/v2rayN` Release 未改动。
- 本次测试安装目录、ZIP、解压目录和大临时文件已清理；不清扫与本次测试无关的 `/tmp` 内容。
- **无需用户手动删除 GitHub 对象。** 两份崩溃诊断 core dump 由系统 journal/coredump 机制保留，未擅自修改系统目录。

轻量证据、测试辅助脚本及修改前备份保存在：

`~/.local/state/v2rayN-web-self-update-e2e/20261005/`

该目录权限 `700`，不保留测试安装目录、登录 token 或明文 Management Key。`fixes.patch` 可用于审查/按补丁回滚；不要用旧整份配置覆盖后续用户修改。GitHub workflow 结果已归档为 JSON，因执行清理，原 Actions URLs 不再作为长期证据链接。

如需撤销本次代码修复，在项目根目录先运行以下检查，确认无冲突后再运行第二条；不回退整个分支：

```sh
git apply --check --reverse "$HOME/.local/state/v2rayN-web-self-update-e2e/20261005/fixes.patch"
git apply --reverse "$HOME/.local/state/v2rayN-web-self-update-e2e/20261005/fixes.patch"
```
