# MediaMate - 个人多媒体播放器

## 项目方案文档

### 1. 项目背景

MediaMate 是一款 Windows 桌面多媒体播放器，作为《Windows 程序设计》课程项目开发完成。本项目的目标是构建一个轻量级但功能丰富的多媒体播放器，支持本地音频播放、歌单管理、实时音频频谱可视化和基础视频播放功能。项目运用现代化 C# WinForms 开发技术，结合第三方开源库，配合暗色主题界面，提供良好的用户体验。项目中使用了 AI 辅助编程工具 Claude Code 提升开发效率，并实践了 RDD（需求驱动开发）与 TDD（测试驱动开发）的开发模式。

### 2. 功能需求

| 功能模块 | 具体功能 | 课程知识点覆盖 |
|:--|:--|:--:|
| **音频播放引擎** | 播放/暂停/停止/跳转，支持 MP3/WAV/FLAC/OGG 格式，音量控制，进度跟踪 | 多媒体处理、并发控制 |
| **歌单管理** | SQLite 数据库持久化，创建/删除歌单，添加/移除曲目 | 数据库连接与处理 |
| **频谱可视化** | 实时 FFT 频域分析，32 条柱状图显示，峰值保持指示 | 图像处理、多媒体处理 |
| **文件管理** | 打开文件、扫描文件夹、显示文件元数据 | 文件处理 |
| **视频播放** | 支持 MP4/MKV/AVI/MOV 格式，音频/视频双模式切换 | 多媒体处理 |
| **UI 界面** | SunnyUI 暗色主题，响应式分栏布局，工具栏+底部控制栏 | WinForm 窗体与控件 |

### 3. 技术选型与理由

| 技术 | 用途 | 选型理由 |
|:--|:--|:--|
| **C# .NET Framework 4.8** | 开发平台 | 课程指定开发环境，WinForms 支持完善，.NET Framework 4.8 是 Windows 10/11 自带运行时，用户无需额外安装 |
| **NAudio 2.3.0** | 音频引擎 | 开源免费（MIT 协议），支持 FFT 频谱分析，可读取多格式音频文件（MP3/WAV/FLAC），纯托管代码无需安装额外解码器 |
| **System.Data.SQLite** | 数据库 | 零配置嵌入式数据库，无需安装数据库服务，数据库就是一个 `.db` 单文件随程序部署，SQL 语法与 MySQL 兼容 |
| **SunnyUI 3.9.7** | UI 框架 | MIT 开源协议，提供整套现代化控件（UIButton/UISlider/UIListBox），内置暗色主题，支持 DPI 自适应 |
| **LibVLCSharp 3.10.0** | 视频引擎 | 基于 VLC 的解码引擎，支持几乎所有视频格式，硬件加速解码，播放稳定 |
| **VideoLAN.LibVLC.Windows** | VLC 原生库 | LibVLCSharp 运行必需的原生解码库，包含音视频解码器 |

#### 技术对比分析

**NAudio 对比 WindowsMediaPlayer COM 组件**：NAudio 是纯托管代码库，可以直接访问音频样本数据实现 FFT 频谱分析——这是课程要求"深入技术实现"章节的核心功能。而 WindowsMediaPlayer COM 组件是黑盒，无法提取波形数据做可视化，且依赖系统安装 WMP，兼容性不可控。

**SQLite 对比 MySQL**：SQLite 无需安装数据库服务器，数据库就是一个 `.db` 文件，随程序一起提交。如果使用 MySQL，评审老师需要安装并配置 MySQL Server、导入数据库、配置连接字符串才能运行程序，这对课程作业评价来说不现实。

**SunnyUI 对比自行 GDI+ 绘制**：SunnyUI 提供统一的主题管理，开发者只需几行代码即可切换暗色/亮色主题，且所有控件风格一致。而自行绘制需要大量代码实现按钮悬浮效果、圆角、涟漪动画等细节，在课程项目时间限制下性价比不高。

### 4. 系统架构设计

```
┌────────────────────────────────────────────────────────┐
│                     主窗体 (UIForm)                      │
├────────────────────┬───────────────────────────────────┤
│  左侧面板          │  右侧面板                          │
│  ┌──────────────┐  │  ┌──────────────────────────────┐ │
│  │ TabControl   │  │  │ 专辑封面 180x180             │ │
│  │  音乐库列表  │  │  │ 当前曲目标题                 │ │
│  │  歌单曲目    │  │  │ 艺术家 | 专辑                │ │
│  │  视频列表    │  │  ├──────────────────────────────┤ │
│  └──────────────┘  │  │ 频谱可视化 / 视频画面        │ │
│  歌单选择器(80px)  │  │                              │ │
├────────────────────┴───────────────────────────────────┤
│                   底部控制栏 (90px)                      │
│  [进度条━━━━━━━━━━━━━━━━━]  00:00/00:00                │
│  [⏮] [▶] [⏭] [⏹]              [Vol ▬▬▬▬▬▬▬]          │
└────────────────────────────────────────────────────────┘
```

**三层架构设计**：

| 层次 | 组件 | 职责 |
|:--|:--|:--|
| **表现层** | Form1.cs + SpectrumVisualizer.cs | 界面布局、用户交互、频谱绘制 |
| **服务层** | AudioPlayerEngine.cs + VideoPlayerService.cs + DatabaseService.cs | 音频播放控制、视频播放控制、数据库 CRUD |
| **数据层** | SQLite (.db 文件) | 持久化存储音乐文件元数据、歌单关系 |

**事件驱动的播放控制流程**：

```
用户点击"播放" → Form1.BtnPlayPause_Click
  → AudioPlayerEngine.TogglePlayPause()
    → NAudio WaveOutEvent.Play()
      → 50ms 定时器触发
        → 更新进度条 (PositionChanged 事件)
        → 计算 FFT 数据 (FftDataAvailable 事件)
          → SpectrumVisualizer.UpdateData() → GDI+ 重绘
```

**模块间解耦**：播放引擎与 UI 通过 C# 事件 (`Action<T>` 委托) 通信，不直接依赖。这样做的好处是播放引擎可以在后台线程运行，UI 只负责订阅事件更新界面，互不阻塞。

### 5. 开发环境

| 项目 | 详情 |
|:--|:--|
| **开发工具** | Visual Studio 2022 (v17.14) |
| **编程语言** | C# 12.0 |
| **目标框架** | .NET Framework 4.8 |
| **操作系统** | Windows 11 23H2 |
| **版本控制** | Git + GitHub |
| **AI 辅助工具** | Claude Code (Deepseek-v4-flash) |



### 参考文献

1. NAudio - .NET 音频处理库. https://github.com/naudio/NAudio
2. SunnyUI - WinForm 开源控件库. https://github.com/yhuse/SunnyUI (MIT License)
3. LibVLCSharp - libVLC .NET 绑定. https://github.com/videolan/libvlcsharp
4. Material Design 深色主题配色指南. https://m3.material.io/styles/dark-colors
5. NAudio 2.3.0 NuGet 包. https://www.nuget.org/packages/NAudio/2.3.0
6. System.Data.SQLite NuGet 包. https://www.nuget.org/packages/System.Data.SQLite
