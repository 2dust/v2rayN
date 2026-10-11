# SSH feature 测试版打包发布

`release SSH feature preview` 从 `feature/native-ssh-profiles` 的提交构建测试版。
推送 `v<Version>-ssh-preview.<序号>` 标签即可触发，例如：

```bash
git switch feature/native-ssh-profiles
git pull --ff-only
git tag -a v7.25.5-ssh-preview.1 -m 'SSH feature preview 1'
git push origin feature/native-ssh-profiles
git push origin v7.25.5-ssh-preview.1
```

`Version` 必须与 `v2rayN/Directory.Build.props` 一致，序号为正整数。
标签必须属于 feature 分支，工作流从标签对应的提交构建，不会转到 `master`。
如果 fork 尚未自动注册新工作流，可通过已有的 `release all platforms` 入口启动：

```bash
gh workflow run build-all.yml --repo idefav/v2rayN \
  --ref v7.25.5-ssh-preview.1 -f release_tag=v7.25.5-ssh-preview.1
```

该入口识别 `-ssh-preview.` 标签后转入 feature 打包流程，不触发上游发布任务。
也可重跑失败的 Actions；新工作流进入默认分支后，可以直接手动选择标签并传入同名
`release_tag`。标签与所选 ref 必须指向同一个提交。

发布内容：

| 平台 | 架构 | 格式 |
| --- | --- | --- |
| Windows WPF | x64 / arm64 | ZIP |
| Windows Avalonia | x64 / arm64 | ZIP，文件名带 `-desktop` |
| Linux | x64 / arm64 | ZIP |
| macOS | Intel x64 / Apple Silicon arm64 | ZIP、DMG |

共 10 个包及一个 `SHA256SUMS` 文件。所有包包含 .NET 运行时和代理核心。
每次运行先固定 `2dust/v2rayN-core-bin` 的提交，测试和所有包使用同一份核心版本。
Linux ZIP 解压后运行 `v2rayN`；macOS 可使用 DMG 安装。

发布前必须通过单元测试、临时 Docker OpenSSH/Paramiko 集成测试、全部架构编译、
ZIP/DMG 完整性检查及资产数量检查。仅在全部成功后发布 GitHub Pre-release，
不会设为 Latest。上传中断只会留下草稿，可重跑；已公开的版本不会被覆盖，
新构建请增加标签序号。

测试版不要求 `GPG_PRIVATE_KEY`，不生成 GPG 签名；macOS 包未经 Apple 开发者签名
及公证。现有上游正式发布工作流仍独立使用其签名配置。

下载后可在包所在目录校验：

```bash
# Linux
sha256sum --check SHA256SUMS --ignore-missing
# macOS：核对输出与 SHA256SUMS 中对应文件的记录
shasum -a 256 v2rayN-macos-arm64.dmg
```
