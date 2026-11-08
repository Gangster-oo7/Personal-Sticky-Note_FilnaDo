# 个人便签

`Personal Sticky Note_FilnaDo` 是一款 Windows 桌面便签应用。点击某张便签右上角的 **置顶**，它会在切换到其他普通应用窗口后继续显示。再次点击 **已置顶** 即可取消，也可以按 `Ctrl+Shift+P`。每张便签独立设置。

## 运行

在 Windows 10 或 11（.NET Framework 4.8 或更新）上双击 `PersonalStickyNote.exe`。便签列表左上角的 **新建** 会打开一张独立便签。关闭便签窗口后，可以从列表双击打开。便签右上角的 **⋯** 菜单可以返回列表。

- 用搜索框查找便签正文，列表按最近编辑时间排序。
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

项目使用 C#、WinForms 和 Windows 自带的 .NET Framework 编译器，无需额外安装 NuGet 依赖。应用入口和窗口管理在 `src/Program.cs`，便签列表和配色在 `src/Windows.cs`，编辑器和置顶开关在 `src/NoteWindow.cs`，数据保存逻辑在 `src/NoteStore.cs`。

## 数据与功能范围

便签自动保存到 `%LOCALAPPDATA%\PersonalStickyNote\notes.xml`，并保留上一版保存记录 `notes.xml.bak`。应用会恢复便签位置、大小和置顶设置。此版本采用本地存储，微软账号登录和跨设备同步未接入。

置顶适用于同一桌面的普通应用窗口。Windows 安全桌面、独占全屏及其他置顶窗口由系统管理。
