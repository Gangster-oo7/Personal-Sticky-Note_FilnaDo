# Personal Sticky Note

[简体中文](../README.md) · English · [Français](README.fr.md) · [Español](README.es.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

`Personal Sticky Note_FilnaDo` is a local sticky note app for Windows. Double-click [PersonalStickyNote.exe](../updates/PersonalStickyNote.exe) on Windows 10 or 11 with .NET Framework 4.8 or later.

Click the globe next to **Settings** in the note list. The app follows the Windows display language by default and uses English when that language is unsupported. You can select Simplified Chinese, English, French, Spanish, Japanese, or Korean. The app applies and saves your choice immediately. Windows file and color pickers use the Windows display language.

- Pin the list or a note above other windows. Enable **Start at Windows sign-in** to open the list and pinned notes at their saved positions when you sign in.
- Create, rename, search, and group notes in the list. Drag a note to move or reorder it, and right-click to see more actions.
- Format text, insert images or sketches, and make checklists. Click a checkbox to mark a task done. Press Enter to add a task or paste Markdown tasks with `- [ ]` and `- [x]`.
- Set separate default colors for the list and new notes, use light or dark appearance, and snap windows to screen edges or nearby notes. Change key bindings under **Settings → Shortcuts**.

Notes and groups are saved in `%LOCALAPPDATA%\PersonalStickyNote\notes.xml` with a backup. You can use the app without signing in. **Check for updates** is available in settings and the tray menu. Updating from the private repository requires a fine-grained GitHub token with read-only **Contents** access to this repository.

To build from source, run `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` in the repository root. The EXE is written to `dist`. Interface translations are in [`src/Strings.tsv`](../src/Strings.tsv).
