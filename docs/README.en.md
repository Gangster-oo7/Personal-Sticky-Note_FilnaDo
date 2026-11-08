# Personal Sticky Note

[简体中文](../README.md) · English · [Français](README.fr.md) · [Español](README.es.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

`Personal Sticky Note_FilnaDo` is a local sticky note app for Windows.

**[Download PersonalStickyNote.exe](https://github.com/Gangster-oo7/Personal-Sticky-Note_FilnaDo/raw/refs/heads/main/updates/PersonalStickyNote.exe)** · First public version **v1.0.1**.

On Windows 10 or 11 with .NET Framework 4.8 or later, double-click the downloaded EXE to run it. Keep it in a permanent folder you can write to so the app can update and its shortcuts can find it.

If you used a test build with a higher version number, close it and replace its EXE with v1.0.1 once. Future updates start from v1.0.1 and can be installed from the app.

Click the globe next to **Settings** in the note list. The app follows the Windows display language by default and uses English when that language is unsupported. You can select Simplified Chinese, English, French, Spanish, Japanese, or Korean. The app applies and saves your choice immediately. Windows file and color pickers use the Windows display language.

- Pin the list or a note above other windows. Enable **Start at Windows sign-in** to open the list and pinned notes at their saved positions when you sign in.
- Create, rename, search, and group notes in the list. Drag a note to move or reorder it, and right-click to see more actions.
- Format text, insert images or sketches, and make checklists. Click a checkbox to mark a task done. Press Enter to add a task.
- Set separate default colors for the list and new notes, use light or dark appearance, and snap windows to screen edges or nearby notes. Change key bindings under **Settings → Shortcuts**.

Notes, groups, and settings are stored in the current Windows account’s `%LOCALAPPDATA%\PersonalStickyNote\notes.xml`, with a backup alongside it. Moving the EXE keeps the same data within the same Windows account. Another account or computer uses its own data folder. If you move the EXE, recreate the desktop shortcut and turn startup off and on again to update its path.

**Check for updates** is available in settings and in the tray menu. It requires no GitHub account.

To build from source, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` in the repository root. The EXE is written to `dist`. Interface translations are in [`src/Strings.tsv`](../src/Strings.tsv).
