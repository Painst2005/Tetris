# Tetris · 俄罗斯方块

C# 课程大作业。12 列 × 16 行棋盘，WinForms 自绘界面，核心逻辑与渲染完全分离。

## 环境要求

| 项 | 要求 |
|---|---|
| 操作系统 | Windows 10 1809 及以上 / Windows 11 |
| SDK | .NET 10 SDK（≥ 10.0.402，LTS） |
| 架构 | x64 |
| 依赖 | 无第三方 UI 库；测试使用 xUnit |

.NET 10 SDK 下载：<https://dotnet.microsoft.com/download/dotnet/10.0>
验证安装：`dotnet --version` 应输出 `10.0.x`。

## 构建与运行

```bash
# 还原依赖
dotnet restore Tetris.sln

# 构建
dotnet build Tetris.sln -c Release

# 运行游戏
dotnet run --project Tetris.csproj -c Release
```

也可以直接用 Visual Studio 打开 `Tetris.sln`，把 `Tetris` 设为启动项目后按 F5。

## 单元测试

当前共 **19 个**测试：13 个覆盖棋盘碰撞、旋转、消行、计分、状态克隆，6 个覆盖存档往返、损坏隔离与文件占用降级。

```bash
dotnet test Tetris.Tests/Tetris.Tests.csproj
```

带覆盖率：

```bash
dotnet test Tetris.Tests/Tetris.Tests.csproj --collect:"XPlat Code Coverage"
```

## 发布

```bash
dotnet publish Tetris.csproj -c Release -r win-x64 --self-contained false -o publish
```

产物在 `publish/` 目录，双击 `Tetris.exe` 运行。目标机器需安装 .NET 10 Desktop Runtime。

## 操作说明

| 按键 | 功能 |
|---|---|
| ← / → | 左右移动（长按连续移动） |
| ↑ 或 Z | 顺时针 / 逆时针旋转 |
| ↓ | 软降（长按加速下落） |
| Space | 瞬间下落并锁定 |
| P | 暂停 / 继续 |
| R | 重新开始 |

## 规则

- 一次消除 `k` 行得 `100 × k` 分。
- 每累计消除 10 行升 1 级，下落间隔随等级缩短（下限 70 ms）。
- 分数与历史最高分在界面上以**十六进制**显示（`0x` 前缀），内部按十进制整数计算与存储。
- 历史最高分保存为 XML：`%LOCALAPPDATA%\Tetris\highscore.xml`，启动时自动读取。

## 项目结构

```
Tetris.sln
├─ Tetris.csproj              WinForms 宿主（渲染与输入）
├─ UI/MainForm.cs             界面绘制、按键、计时驱动
├─ Persistence/               最高分持久化（XML）
└─ Tetris.Core/               纯逻辑，net10.0，不引用任何 UI
   └─ Game/
      ├─ TetrisGame.cs        棋盘、移动、旋转、消行、计分
      ├─ Tetromino.cs         方块形状与旋转
      └─ GameSnapshot.cs      与界面无关的完整状态快照
└─ Tetris.Tests/              xUnit 单元测试
```

`Tetris.Core` 目标框架为 `net10.0`（非 `net10.0-windows`），因此可以在任何平台引用与测试，这一点是「逻辑与渲染分离」的直接证据。

## 异常处理说明

| 场景 | 行为 |
|---|---|
| 存档不存在 | 视为首次运行，最高分为 0 |
| 存档 XML 损坏 | 原文件重命名为 `highscore.xml.corrupt-时间戳`，按 0 分启动，下次启动干净 |
| 存档被占用 | 读取时使用共享模式并重试；写入失败不影响游戏继续 |
| 目录只读 / 无权限 | 成绩保留在内存中，界面底部提示「本次成绩未保存」 |
| 窗口缩放 | 画面按客户区等比缩放并居中，最小尺寸受限，不会溢出或错位 |
| 快速连按 / 同时按键 | 按键集合去重，长按走重复计时器，不会重复触发或漏键 |
