# 个人便签

`Personal Sticky Note_FilnaDo` 是一款 Windows 桌面便笺应用。列表和每张便笺的标题栏都提供 **置顶**，位于最小化按钮左边。再次点击 **已置顶** 或按 `Ctrl+Shift+P` 可切换状态。

## 运行

在 Windows 10 或 11（.NET Framework 4.8 或更新）上双击 `PersonalStickyNote.exe`，即可打开便签列表。更新时先退出旧版，再用新版替换原位置的 EXE。

- 通过 **设置 → 创建桌面快捷方式**，以后可以双击桌面上的「个人便签」打开列表。请把 EXE 保存在固定位置，再创建快捷方式。
- **设置 → 登录 Windows 后自动启动** 默认关闭。确认开启后，登录 Windows 时会恢复上次打开的便签，保留内容、位置、大小和置顶状态。再次点击该选项可关闭自动启动。
- 点击分组名展开或折叠，右键可重命名、删除或在组内新建便笺。把便笺拖到分组名上可移入该组，拖到另一条便笺的上半部或下半部可排在它之前或之后。删除分组会把其中的便笺移到「未分组」。
- 点击便笺窗口中加号旁的 **命名便笺** 可编辑名称。列表优先显示名称，留空时显示正文预览。搜索同时匹配名称和正文，双击列表里的便笺即可打开。
- 点击 **待办** 把当前行转为待办项，点击方框切换完成状态。按回车可继续下一项，在空项按回车可退出待办。
- 用 **字体** 选择 Windows 已安装的字体，例如宋体、仿宋、楷体、幼圆，也可导入 TTF、OTF 或 TTC。选择的字体和 **文字颜色** 可应用到选中文字或接下来的输入。
- 在 **⋯ → 背景颜色** 中可选预设颜色，也可打开调色盘。工具栏仍支持常用文字格式和插入图片，**⋯** 菜单中可手写、涂画并插入结果。
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
| `Ctrl+Shift+J` / `Ctrl+Shift+K` | 添加待办 / 勾选或取消当前待办 |
| `Ctrl+Shift+P` | 切换当前窗口的置顶状态 |

## 从源码构建

在项目目录打开 PowerShell，运行以下命令。生成的程序位于 `dist\PersonalStickyNote.exe`。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

项目使用 C#、WinForms 和 Windows 自带的 .NET Framework 编译器，无需额外安装 NuGet 依赖。列表和分组界面在 `src/ListWindow.cs`，便笺编辑器在 `src/NoteWindow.cs`，标题栏在 `src/CaptionWindow.cs`，字体导入在 `src/NoteFonts.cs`，数据保存逻辑在 `src/NoteStore.cs`。

## 数据与功能范围

便笺和分组自动保存到 `%LOCALAPPDATA%\PersonalStickyNote\notes.xml`，并保留上一版保存记录 `notes.xml.bak`。导入的字体保存在同一目录下的 `Fonts` 子目录。旧版便笺升级后会保留分组、内容和窗口设置。使用本地版无需登录任何账号。

置顶适用于同一桌面的普通应用窗口。Windows 安全桌面、独占全屏及其他置顶窗口由系统管理。
