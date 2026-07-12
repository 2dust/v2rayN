# 连接保活功能实现计划

## 一、需求概述

在 v2rayN 的【订阅分组设置】中为每个订阅分组增加【连接保活】开关。启用后，系统会定期检测当前活动节点的可用性；当当前节点不可用时，自动在该订阅分组内寻找真连接延迟最小的可用节点并切换；若全部分组节点均不可用，则触发订阅更新。订阅更新（自动或手动）后若当前活动节点丢失，也执行同样的检测与切换逻辑。

## 二、设计确认

| 项目 | 确认结果 |
|---|---|
| 按钮形态 | 开关（Toggle），在订阅分组编辑窗口启用/停用 |
| 检查间隔 | 默认 10 分钟，提供输入框可配置 |
| 最快节点 | 按**真连接延迟最小**选择 |
| 作用范围 | 配置在 `SubItem`（订阅分组）上；只有当前活动节点属于该分组时才执行保活 |
| 更新后兜底 | 更新后若当前节点丢失，立刻测试并切换最快可用节点；若全部不可用，**不发更新**，只告警，等下次保活检查周期再触发更新 |
| 告警频率 | 由 `KeepAliveInterval` 控制，不会刷屏 |

## 三、涉及文件清单

### 新增
- `ServiceLib/Manager/KeepAliveManager.cs`

### 修改
- `ServiceLib/Models/Entities/SubItem.cs`
- `ServiceLib/Handler/ConfigHandler.cs`
- `ServiceLib/Handler/ConnectionHandler.cs`
- `ServiceLib/Services/SpeedtestService.cs`
- `ServiceLib/Manager/ProfileExManager.cs`
- `ServiceLib/Manager/TaskManager.cs`
- `ServiceLib/ViewModels/MainWindowViewModel.cs`
- `ServiceLib/Resx/ResUI.resx`、`ResUI.zh-Hans.resx`
- `v2rayN/Views/SubEditWindow.xaml`
- `v2rayN/Views/SubEditWindow.xaml.cs`
- `v2rayN/Views/SubSettingWindow.xaml`
- `v2rayN.Desktop/Views/SubEditWindow.axaml`
- `v2rayN.Desktop/Views/SubEditWindow.axaml.cs`
- `v2rayN.Desktop/Views/SubSettingWindow.axaml`

## 四、详细修改步骤

### 1. 数据模型（SubItem）

在 `ServiceLib/Models/Entities/SubItem.cs` 中新增字段：

```csharp
public bool KeepAlive { get; set; }
public int KeepAliveInterval { get; set; } = 10; // 分钟
public long KeepAliveLastCheck { get; set; }
public long KeepAliveLastUpdate { get; set; }
```

### 2. 持久化（ConfigHandler.AddSubItem）

在 `ServiceLib/Handler/ConfigHandler.cs` 的 `AddSubItem(Config, SubItem)` 映射逻辑中增加：

```csharp
item.KeepAlive = subItem.KeepAlive;
item.KeepAliveInterval = subItem.KeepAliveInterval;
item.KeepAliveLastCheck = subItem.KeepAliveLastCheck;
item.KeepAliveLastUpdate = subItem.KeepAliveLastUpdate;
```

### 3. 资源字符串

在 `ServiceLib/Resx/ResUI.resx` 和 `ServiceLib/Resx/ResUI.zh-Hans.resx` 中新增：

- `LvKeepAlive` → 连接保活
- `LvKeepAliveInterval` → 保活间隔（分钟）
- `MsgKeepAliveSwitched` → 保活：已切换至节点 {0}
- `MsgKeepAliveAllFailed` → 保活：订阅 {0} 全部节点不可用，下次检查将尝试更新

### 4. UI（SubEditWindow）

在 WPF / Avalonia 的订阅分组编辑窗口中增加一行：
- 标签：`LvKeepAlive`
- 开关：`togKeepAlive`
- 间隔输入框：`txtKeepAliveInterval`（仅启用保活时可用）

并在 `SubSettingWindow` 列表中增加 `KeepAlive` 和 `KeepAliveInterval` 两列，方便查看状态。

### 5. ConnectionHandler 暴露当前节点延迟

在 `ServiceLib/Handler/ConnectionHandler.cs` 中新增公共方法：

```csharp
public static async Task<int> GetCurrentRealPingAsync()
{
    return await GetRealPingTimeInfo();
}
```

用于保活服务直接测试当前活动节点。

### 6. SpeedtestService 支持可等待

把 `ServiceLib/Services/SpeedtestService.cs` 中现有的 `RunAsync` 重命名为 `RunInternalAsync`，新增公共方法：

```csharp
public async Task RunAsync(ESpeedActionType actionType, List<ProfileItem> selecteds)
{
    await RunInternalAsync(actionType, selecteds);
    await ProfileExManager.Instance.SaveTo();
    await UpdateFunc("", ResUI.SpeedtestingCompleted);
}
```

`RunLoop` 改为 `Task.Run(() => RunAsync(...))` 的包装。

### 7. ProfileExManager 增加读取方法

在 `ServiceLib/Manager/ProfileExManager.cs` 中新增：

```csharp
public int GetTestDelay(string? indexId)
{
    var profileEx = _lstProfileEx.FirstOrDefault(t => t.IndexId == indexId);
    return profileEx?.Delay ?? 0;
}
```

保活测试完成后用此取延迟。

### 8. KeepAliveManager（核心）

新增 `ServiceLib/Manager/KeepAliveManager.cs`：

```csharp
public class KeepAliveManager
{
    public static KeepAliveManager Instance { get; }

    public void Init(Config config, Func<Task> reloadFunc, Func<bool, string, Task> updateFunc)

    public async Task RunKeepAliveAsync()          // 周期性入口
    public async Task RunPostUpdateFallbackAsync(string subId) // 更新后兜底
}
```

#### `RunKeepAliveAsync` 流程：
1. 取当前活动节点 `activeProfile`
2. 若为空，返回
3. 取 `subItem = activeProfile.Subid`
4. 若 `subItem.KeepAlive == false`，返回
5. 判断 `now - subItem.KeepAliveLastCheck >= KeepAliveInterval * 60`
6. 测试当前节点：`ConnectionHandler.GetCurrentRealPingAsync()`
7. 若延迟 > 0，更新 `KeepAliveLastCheck`，返回
8. 否则获取该分组下所有节点，执行 `SpeedtestService.RunAsync(Realping)`
9. 取延迟 > 0 的节点，选最小值：
   - 找到 → `SetDefaultServerIndex` + `Reload` + 通知
   - 未找到 → 若 `now - KeepAliveLastUpdate >= interval` 则调用 `SubscriptionHandler.UpdateProcess` 并更新 `KeepAliveLastUpdate`；否则只告警

#### `RunPostUpdateFallbackAsync` 流程：
1. 若 `subItem.KeepAlive == false`，返回
2. 获取该分组所有节点，执行 Realping
3. 若找到可用节点，切换最快并 `Reload`
4. 若未找到，**只告警不发更新**，等待下次 `RunKeepAliveAsync`

### 9. 调度入口（TaskManager）

在 `ServiceLib/Manager/TaskManager.cs` 的 `ScheduledTasks` 的 60 秒循环中，于 `UpdateTaskRunSubscription` 之后调用：

```csharp
try
{
    await KeepAliveManager.Instance.RunKeepAliveAsync();
}
catch (Exception ex)
{
    Logging.SaveLog("ScheduledTasks - KeepAlive", ex);
}
```

### 10. 订阅更新回调签名扩展

为了让 `UpdateTaskHandler` 知道“是哪个分组被更新了”，把 `SubscriptionHandler.UpdateProcess` 的回调从 `Func<bool, string, Task>` 改为 `Func<bool, string, string, Task>`（第三个参数为 `subId`）。

同步修改：
- `TaskManager._updateFunc`
- `TaskManager.RegUpdateTask`
- `MainWindowViewModel.UpdateTaskHandler`
- `MainWindowViewModel.UpdateSubscriptionProcess`

### 11. 更新后兜底（MainWindowViewModel）

在 `ServiceLib/ViewModels/MainWindowViewModel.cs` 的 `UpdateTaskHandler` 中，更新成功后：

```csharp
var indexIdOld = _config.IndexId;
await RefreshServers();

var profile = await AppManager.Instance.GetProfileItem(_config.IndexId);
if (profile == null)
{
    var fallbackSubId = subId.IsNullOrEmpty() ? _config.SubIndexId : subId;
    if (fallbackSubId.IsNotEmpty())
    {
        await KeepAliveManager.Instance.RunPostUpdateFallbackAsync(fallbackSubId);
    }
}
else if (...)
{
    await Reload();
}
```

### 12. 初始化

在 `ServiceLib/ViewModels/MainWindowViewModel.cs` 的 `Init` 中：

```csharp
TaskManager.Instance.RegUpdateTask(_config, UpdateTaskHandler);
KeepAliveManager.Instance.Init(_config, Reload, UpdateTaskHandler);
```

## 五、流程图

```
开始
  │
  ▼
获取当前活动节点 activeProfile
  │
  ▼
activeProfile 为空？ ──是──→ 返回
  │否
  ▼
activeProfile 所在 SubItem 的 KeepAlive？
  │
否──→ 返回
  │
是
  ▼
是否到达检查间隔？
  │
否──→ 返回
  │
是
  ▼
测试当前节点延迟
  │
  ▼
延迟 > 0？
  │
是──→ 更新 lastCheck，返回
  │
否
  ▼
对该分组所有节点执行 Realping
  │
  ▼
存在延迟 > 0 的节点？
  │
是──→ 选延迟最小者，SetDefaultServerIndex，Reload，通知
  │
否
  ▼
距离上次更新 >= 间隔？
  │
是──→ 触发 SubscriptionHandler.UpdateProcess，更新 lastUpdate
  │
否──→ 告警（通知+日志），等下次周期
```

## 六、边界与风险

1. **并发**：保活测试和手动测速不能同时运行。`KeepAliveManager` 内部加 `_isRunning` 锁，避免冲突。
2. **自定义节点**：当前活动节点若不属于任何 SubItem（`Subid` 为空），保活不执行。
3. **告警频率**：每次失败都告警可能刷屏。由于 `KeepAliveInterval` 已控制检查周期，10 分钟内只告警一次。
4. **Core 冲突**：`SpeedtestService` 会启动独立测试 Core，与当前运行 Core 不冲突，但要确保测试完成后再 `Reload`。
5. **多订阅更新**：`subId = ""` 的全量更新时，回调传入的 `subId` 为空，兜底会退回到 `_config.SubIndexId`；若用户未选中分组，则无法兜底。可考虑在 `UpdateSubscriptionProcess` 调用前保存当前节点 `Subid` 作为备用。

## 七、实现顺序建议

1. 后端数据模型与持久化（SubItem、ConfigHandler）
2. 资源字符串
3. 核心服务（KeepAliveManager）
4. 测速服务改造（SpeedtestService、ConnectionHandler、ProfileExManager）
5. 更新回调签名调整（TaskManager、MainWindowViewModel）
6. UI（SubEditWindow / SubSettingWindow 的 WPF 与 Avalonia）
7. 调度接入（TaskManager）
8. 测试与边界验证
