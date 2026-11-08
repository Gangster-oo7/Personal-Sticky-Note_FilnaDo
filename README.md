# 个人便签

`Personal Sticky Note_FilnaDo` 是一款 Windows 桌面便签应用。点击某张便签右上角的 **置顶**，它会在切换到其他普通应用窗口后继续显示。再次点击 **已置顶** 即可取消，也可以按 `Ctrl+Shift+P`。每张便签独立设置。

## 运行

在 Windows 10 或 11（.NET Framework 4.8 或更新）上双击 `PersonalStickyNote.exe`，即可打开便签列表。更新时先退出旧版，再用新版替换原位置的 EXE。

- 通过 **设置 → 创建桌面快捷方式**，以后可以双击桌面上的「个人便签」打开列表。请把 EXE 保存在固定位置，再创建快捷方式。
- **设置 → 登录 Windows 后自动启动** 默认关闭。确认开启后，登录 Windows 时会恢复上次打开的便签，保留内容、位置、大小和置顶状态。再次点击该选项可关闭自动启动。
- **管理分组** 可新建、重命名和删除分组。用下拉框筛选分组，在组内新建便签会自动归入该组。删除分组会把其中的便签移到「未分组」。
- 用搜索框查找当前分组中的便签正文，选择「全部便签」可跨组搜索。选中一条便签后，可以编辑、移动分组或删除，也可以双击打开。
- 工具栏支持加粗、斜体、下划线、删除线、项目符号和插入图片。选中图片后按 `Delete` 可以移除。
- 在 **⋯** 中可切换便签颜色，或在画板上手写、涂画并插入结果。画板以图片保存笔迹。
- 列表的 **设置** 提供浅色、深色、跟随系统和删除确认。通过 **退出个人便签** 退出时，会记住已打开的便签。

| 快捷键 | 操作 |
| --- | --- |
| `Ctrl+N` | 新建便签 |
| `Ctrl+F` | 打开便签列表和搜索 |
| `Ctrl+H` | 显示便签列表 |
| `Ctrl+W` | 关闭当前便签窗口 |
| `Ctrl+D` | 删除当前便签 |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | 在列表和已打开的便签之间切换 |
| `Ctrl+B` / `Ctrl+I` / `Ctrl+U` / `Ctrl+T` | 切换文字格式 |
| `Ctrl+Shift+L` | 切换项目符号 |
| `Ctrl+Shift+P` | 切换当前便签的窗口置顶 |

## 从源码构建

在项目目录打开 PowerShell，运行以下命令。生成的程序位于 `dist\PersonalStickyNote.exe`。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

项目使用 C#、WinForms 和 Windows 自带的 .NET Framework 编译器，无需额外安装 NuGet 依赖。应用入口和窗口管理在 `src/Program.cs`，便签列表和分组界面在 `src/Windows.cs`，编辑器和置顶开关在 `src/NoteWindow.cs`，数据保存逻辑在 `src/NoteStore.cs`，桌面和自启动快捷方式在 `src/DesktopIntegration.cs`。

## 数据与功能范围

便签和分组自动保存到 `%LOCALAPPDATA%\PersonalStickyNote\notes.xml`，并保留上一版保存记录 `notes.xml.bak`。旧版便签升级后会出现在「未分组」。使用本地版无需登录任何账号。微信绑定和跨设备同步尚未接入。

置顶适用于同一桌面的普通应用窗口。Windows 安全桌面、独占全屏及其他置顶窗口由系统管理。
