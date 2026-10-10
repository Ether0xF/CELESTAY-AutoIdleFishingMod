# CELESTAY Idle 自动钓鱼 Mod

[English](README.md)

一个小型 mod，让 CELESTAY 的 **Idle 钓鱼模式**持续运行，不再需要每次咬钩和显示渔获结果时手动点击。

- 鱼咬钩时自动收竿。
- 渔获结果显示后，经过可配置的延迟自动关闭结果窗口，让下一轮钓鱼开始。默认延迟为 1 秒。
- 菜单或教程阻挡钓鱼时等待；你提前手动关闭结果窗口时，不会重复关闭。
- 钓鱼等待时间、动画、渔获抽取和奖励发放仍由游戏处理。
- 不自动操作 Skill 模式。

后台钓鱼能否继续，取决于游戏本身是否仍在运行。Mac 进入睡眠后，mod 不会让游戏继续运行。

**仅支持 macOS。** Windows 尚未测试，本版本不提供支持。0.3.0 已在 Apple Silicon Mac 上进行游戏内试用，检查了普通钓鱼、菜单遮挡和手动关闭结果窗口的行为。特殊渔获与教程流程尚未完成充分的游戏内验证。

## 环境与依赖

- Steam 版 CELESTAY 的 macOS 版本。当前游戏应用要求 macOS 11 或更高版本；本 mod 仅在 macOS 26.6.2、Unity 6000.0.58f2 环境下测试过。更早的 macOS 版本和 Intel Mac 尚未测试。
- Apple Silicon Mac 需要 **Rosetta 2**。如果 macOS 提示安装，请按提示操作；也可以在终端运行 `softwareupdate --install-rosetta`，然后按提示完成安装。
- **[BepInEx 5.4.23.5 macOS 安装包](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip)**。下面的步骤针对这个具体安装包。它已包含 Doorstop 4.5.0 和 Harmony 依赖，无需另外下载。
- 本项目发布的编译版 `CELESTAY.EnvironmentCheck.dll`，以及仓库中的 [prepare-macos-launcher.sh](scripts/prepare-macos-launcher.sh)。如果还没有可下载的编译版，请参照下方的“从源码编译”。

在 Apple Silicon Mac 上，启动脚本通过 Rosetta 运行游戏的 Intel（`x86_64`）版本，以绕过 BepInEx 5 在 ARM64 原生模式下的补丁兼容性问题。这会让整个游戏通过 Rosetta 运行，而不仅仅是插件。详情见 [上游兼容性问题记录](https://github.com/BepInEx/BepInEx/issues/1303)。

## 四步安装

1. **退出 CELESTAY，备份存档。** 在 Steam 中右键点击 CELESTAY → **管理 → 浏览本地文件**。保持这个游戏目录打开；它就是包含 `CELESTAY.app` 的文件夹。

2. **把文件复制到游戏目录。** 解压 BepInEx ZIP，将里面的内容复制到游戏目录，再把 `prepare-macos-launcher.sh` 复制进去。创建 `BepInEx/plugins/CELESTAY.EnvironmentCheck/` 文件夹，把 `CELESTAY.EnvironmentCheck.dll` 放入其中。只保留一份插件。目录应如下所示：

   ```text
   CELESTAY/
   ├── CELESTAY.app
   ├── libdoorstop.dylib
   ├── run_bepinex.sh
   ├── prepare-macos-launcher.sh
   └── BepInEx/
       ├── core/
       └── plugins/
           └── CELESTAY.EnvironmentCheck/
               └── CELESTAY.EnvironmentCheck.dll
   ```

   直接复制 ZIP 内的内容，不要在游戏目录中再套一层解压文件夹。如果已经安装 BepInEx 并使用其他 mod，请先备份现有安装，再替换文件。

3. **生成启动脚本。** 打开“终端”，输入 `cd `（包括末尾的空格），将游戏文件夹从 Finder 拖进终端，再按回车。然后运行：

   ```sh
   sh ./prepare-macos-launcher.sh
   ```

   这会生成 `run_bepinex_x64.sh`，赋予它运行权限，并输出一行包含实际游戏路径的 Steam 启动选项。原始 BepInEx 启动脚本会保留。如果 macOS 拦截了下载的文件，请参照下方的“常见问题”。

4. **设置 Steam 启动选项，然后开始游戏。** 在 Steam 中右键点击 CELESTAY → **属性 → 通用 → 启动选项**。粘贴第 3 步输出的整行内容，包括引号和 `%command%`。格式如下：

   ```text
   "/your/actual/game/folder/run_bepinex_x64.sh" %command%
   ```

   通过 Steam 启动游戏，开始 **Idle** 钓鱼。自动钓鱼默认开启。要确认插件已加载，可以打开游戏目录中的 `BepInEx/LogOutput.log`，查找 `Loading [CELESTAY Environment Check 0.3.0]` 和 `AutoReady: enabled=True`。插件内部名称目前仍为“CELESTAY Environment Check”。

## 配置

首次启动后，退出游戏，编辑 `BepInEx/config/local.celestay.environmentcheck.cfg`：

```ini
[AutoIdleFishing]
Enabled = true
ResultCloseDelaySeconds = 1
```

- 将 `Enabled` 设为 `false` 可以关闭自动操作，诊断日志仍会保留。
- `ResultCloseDelaySeconds` 支持 0–60 秒。延迟从结果就绪后开始计算，此时动画和奖励处理已经完成。设为 0 时，会在下一个符合条件的帧关闭结果窗口。

修改配置后，重新启动游戏。

## 常见问题

- **游戏正常启动，但没有自动钓鱼：** 确认当前为 Idle 模式、配置已启用，并且 Steam 使用的是 `run_bepinex_x64.sh`。原始启动脚本可能会在 Apple Silicon Mac 上选择 ARM64 模式。检查安装第 4 步提到的日志。
- **macOS 拦截下载的启动脚本或加载器：** 如果你信任从上方链接下载的文件，可以在游戏目录中运行以下命令，移除这些文件的隔离属性，然后重新执行安装第 3 步。这些操作不会为文件签名或完成公证。

  ```sh
  xattr -d com.apple.quarantine ./prepare-macos-launcher.sh
  xattr -d com.apple.quarantine ./run_bepinex.sh
  xattr -d com.apple.quarantine ./libdoorstop.dylib
  ```

  “No such xattr”表示该文件没有隔离属性，继续执行下一条即可。不要全局关闭 Gatekeeper。
- **日志出现 `AutoError` 或初始化错误：** 退出游戏，并在再次启动前保存一份 `BepInEx/LogOutput.log`。游戏更新可能导致兼容性问题；必要时移除插件。

## 卸载

退出游戏，删除 `BepInEx/plugins/CELESTAY.EnvironmentCheck/`。如果不再使用 BepInEx，也请清空 Steam 启动选项。如果其他 mod 仍依赖 BepInEx，请保留它。

## 从源码编译

使用编译版的玩家不需要安装 .NET SDK。如果要自行编译，请先安装 .NET SDK，并将 BepInEx 安装到游戏目录，再在本仓库目录中运行：

```sh
sh scripts/build.sh '-p:GameRoot=/absolute/path/to/CELESTAY'
```

输出文件为 `outputs/CELESTAY.EnvironmentCheck.dll`。编译时引用本机已安装游戏的程序集，不会重新分发这些程序集。如果游戏程序集发生变化，请针对本机游戏重新编译。

本地行为检查：

```sh
dotnet run --project tests/AutoIdleFishingHarness.csproj --configuration Release
```

这些检查使用模拟游戏对象，不能代替游戏内测试。当前验证范围见 [试用记录](docs/auto-idle-fishing-trial.md)。

## 开发说明

本 mod 使用 **vibe coding** 制作，由 AI 编程工具辅助编写代码和文档。它已在我的 Mac 上进行游戏内试用，但测试范围有限。

## 免责声明

我做这个 mod，是因为我自己需要这个功能。如果它对你有用，欢迎按需取用，但请自行判断是否愿意承担相关风险。

这是非官方 mod，按**现状**提供，不附带任何保证，也不承诺持续维护。安装前请备份存档。在适用法律允许的范围内，我不对使用本 mod 导致的游戏数据损坏或丢失、存档损坏、崩溃及其他问题承担责任。使用风险由你自行承担。

## 许可证

本项目的代码和文档采用 **[MIT 许可证](LICENSE)**。BepInEx、Doorstop 及其依赖保留各自的许可证。CELESTAY 及其资源属于各自的权利人，不包含在本仓库中。
