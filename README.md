# CELESTAY Idle 自动钓鱼试用插件

当前版本为 `CELESTAY Environment Check 0.3.0`，GUID 保持 `local.celestay.environmentcheck`，DLL 文件名保持不变，便于替换已有观察插件。

0.3.0 在 0.2.0 的钓鱼日志基础上增加自动操作：

- 检测 Idle 控制器的 `chance` 状态与活动感叹号，没有阻塞界面时切到 `pulling` 并播放原收竿点击音效。
- 原生协程继续完成动画、渔获抽取、收藏保存和资源发放。插件不缩短等待或重复发放奖励。
- 原生结果弹窗出现且 `resultShown` 标志就绪后开始延迟，默认 1 秒，再触发实际关闭按钮的完整 `onClick`。
- 玩家提前关闭、重启钓鱼序列、切换模式、控制器更换时取消旧的关闭动作。菜单／教程阻塞时等待；解除阻塞后重新计关闭延迟。
- Skill 模式不触发自动操作。后台运行时只要 Unity 仍在更新，自动钓鱼继续。

游戏没有在这条流程中提供独立的收竿动画结束回调。`ShowResult` 出现在收竿结束与资源发放之后，因此以结果就绪作为关闭的起点，避免根据动画长度猜时间。

本版本也会关闭记忆、唱片的原生结果窗口，但会完整保留此前的特殊演出、存档及关闭后的教程回调。Obake 无普通弹窗的路径由游戏自行结束。

## 编译

在项目目录执行：

```sh
sh scripts/build.sh
```

需要 .NET SDK，以及已安装 BepInEx 5.4.23.5 的本机 CELESTAY。
项目目标为 `netstandard2.1`，直接引用游戏随附的 Mono 程序集和 BepInEx，不下载 NuGet 包，不把这些依赖复制到输出目录。
本机游戏路径已设为默认值。游戏移动后，可以传入新路径：

```sh
sh scripts/build.sh '-p:GameRoot=/实际路径/CELESTAY'
```

生成文件：`outputs/CELESTAY.EnvironmentCheck.dll`。

## 安装和验证

退出游戏，将输出 DLL 放入游戏目录下的
`BepInEx/plugins/CELESTAY.EnvironmentCheck/`。
通过已配置的 Steam x64 启动脚本启动游戏，进入 Idle 钓鱼。

在 `BepInEx/LogOutput.log` 中确认：

- 加载 `CELESTAY Environment Check 0.3.0`。
- 插件 DLL 路径指向上述安装位置。
- 出现 `Plugin initialization complete.`。
- 出现且只出现一次 `First Update callback received.`。
- 没有该插件的异常日志。
- 出现 `ObservationReady`，随后有 `[FishingTrace ...]` 钓鱼观察日志。
- 出现 `AutoReady: enabled=True`。

按 `docs/auto-idle-fishing-trial.md` 观察两轮自动收竿和关闭，再测试抢先手动关闭、菜单遮挡与模式切换。此前的手动日志不能替代自动版本的游戏内验证。

首次加载会生成／更新 `BepInEx/config/local.celestay.environmentcheck.cfg`：

```ini
[AutoIdleFishing]
Enabled = true
ResultCloseDelaySeconds = 1
```

`Enabled = false` 关闭自动行为，保留观察日志。延迟范围为 0–60 秒；即使为 0，也在结果就绪后的下一帧尝试关闭。退出游戏后编辑配置，再重启验证。

关键日志为 `AutoBiteDetected`、`AutoPull`、`AutoCloseScheduled`、`AutoCloseInvoked`、`AutoCloseCompleted` 和 `AutoCloseCancelled`。原有钓鱼、UI、收藏与奖励日志仍保留。

自动关闭通过 `onClick.Invoke()` 发起，不经过鼠标 `Button.Press`，因此应看 `AutoCloseInvoked`，不要求出现 `ResultButtonPressed`。随后仍应看到原生关闭、按钮恢复和教程请求。

`AutoError` 表示本次启动的自动行为已停用，避免重复执行失败的部分操作。
若出现 `ObserverError`，对应观察可能不完整，应回传日志，不能把没有记录当成事件未发生。

## 本地行为检查

`tests/AutoIdleFishingHarness.csproj` 使用模拟 Unity 对象与原生回调运行同一份自动控制代码，覆盖模式／UI 门禁、延迟、手动关闭竞争、序列重启、控制器替换和异常后停止：

```sh
/usr/local/share/dotnet/dotnet run --project tests/AutoIdleFishingHarness.csproj --configuration Release
```

检查不启动游戏、不修改存档，也不能验证实际 Unity 动画、场景对象或音效；这些需要游戏内试用日志。

卸载时退出游戏，移除 `BepInEx/plugins/CELESTAY.EnvironmentCheck/` 即可。
