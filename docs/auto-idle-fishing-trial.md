# Idle 自动钓鱼 0.3.0 试用

## 安装

1. 退出 CELESTAY。
2. 将 `outputs/CELESTAY.EnvironmentCheck.dll` 替换到游戏的 `BepInEx/plugins/CELESTAY.EnvironmentCheck/CELESTAY.EnvironmentCheck.dll`，只保留一份插件。
3. 按原来的 Steam x64 配置启动。日志应显示 `Loading [CELESTAY Environment Check 0.3.0]` 和 `AutoReady: enabled=True`。

默认开启自动收竿；原生结果弹窗出现后延迟 1 秒自动关闭。所有资源与收藏仍由游戏结算。记忆、唱片也会自动关闭结果窗口；特殊演出和教程保留。

本版使用“结果弹窗与显示标志已就绪”确认收竿结束。游戏先完成收竿、发资源，再显示弹窗，因此不会在刚检测到咬钩时就计关闭时间。

## 先观察两轮

使用已解锁、等待较短的 Idle 设置；完成两次渔获，第二次关闭后确认第三轮进入等待：

| 阶段 | 预期 |
|---|---|
| 等待 | 仍按游戏原来的深度、鱼饵、卷线器和等待时间进行 |
| 咬钩 | 不点击感叹号；应看到 `AutoBiteDetected`、`AutoPull` 和 `chance -> pulling` |
| 收竿 | 正常动画和结算，每轮只有一次计数与奖励 |
| 显示结果 | `ResultShown` 后出现 `AutoCloseScheduled: delaySeconds=1.000` |
| 自动关闭 | `AutoCloseInvoked` 后有原生关闭日志、按钮恢复和教程请求，随后 `AutoCloseCompleted` |
| 下一轮 | `resulting -> throwing -> waiting`；不再需要手动操作 |

`AutoCloseInvoked` 直接触发按钮事件，不模拟鼠标，故不必出现 `ResultButtonPressed`。

## 接着验证三条边界

1. **抢先手动关闭**：结果显示后在 1 秒内点击原生关闭区（包括可关闭的空白区域）。应出现 `AutoCloseCancelled`；下一轮不能在旧截止时间被关闭，也不能重复发奖。如果来不及，可退出后把延迟调到 3 秒再试。
2. **咬钩时菜单遮挡**：在快咬钩前打开图鉴或其他菜单。应保持 `chance`，直到关闭菜单才自动收竿。菜单等待不改变原生计时。
3. **切换 Skill 再切回 Idle**：在等待阶段应用模式切换。Skill 下不能出现自动收竿／关闭；切回 Idle 后按新一轮正常等待恢复。

记忆、唱片、Obake、未完成教程或其他剧情若自然出现，记录其表现即可，不必重置存档刷取。特殊路径本版尚未获得游戏内验证。

## 配置与停止

退出游戏后编辑游戏目录中的 `BepInEx/config/local.celestay.environmentcheck.cfg`：

```ini
[AutoIdleFishing]
Enabled = true
ResultCloseDelaySeconds = 1
```

将 `Enabled` 改为 `false` 并重启，可恢复只观察、不自动操作。也可退出后移除插件。延迟支持 0–60 秒。

出现 `AutoError` 后，本次启动的自动行为会停止；请保留日志。如果卡住或主按钮没有恢复，也先保存当前日志再重启。

测试结束后提供完整 `BepInEx/LogOutput.log`，注明完成了哪些操作，以及是否出现特殊渔获／教程。不要先再次启动游戏覆盖测试日志。

## 当前验证范围

插件已针对本机游戏程序集编译成功，0 警告、0 错误，14 项本地行为模拟检查通过。用户已在 macOS 实际游戏中试用 0.3.0，反馈运行正常，并确认其他菜单遮挡时暂停自动收竿、抢先手动关闭结果时行为正常。特殊渔获与教程路径尚未取得该版本的游戏内验证证据。
