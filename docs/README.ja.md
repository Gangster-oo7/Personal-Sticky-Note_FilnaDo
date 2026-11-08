# 個人用付箋

[简体中文](../README.md) · [English](README.en.md) · [Français](README.fr.md) · [Español](README.es.md) · 日本語 · [한국어](README.ko.md)

`Personal Sticky Note_FilnaDo` は、付箋を端末に保存する Windows アプリです。Windows 10 または 11 と .NET Framework 4.8 以降の環境で [PersonalStickyNote.exe](../updates/PersonalStickyNote.exe) をダブルクリックして起動します。

付箋一覧の **設定** の横にある地球儀ボタンで表示言語を選択できます。既定では Windows の表示言語に合わせ、未対応の言語では英語を使用します。簡体字中国語、英語、フランス語、スペイン語、日本語、韓国語から選択でき、設定はすぐに反映・保存されます。Windows のファイル選択画面や色の選択画面にはシステムの表示言語が使われます。

- 一覧や付箋を最前面に固定できます。**Windows にサインインしたら起動** を有効にすると、サインイン時に一覧と固定した付箋が保存された位置に開きます。
- 一覧から付箋を作成、検索、名前変更し、グループに整理できます。ドラッグで移動や並べ替えができ、右クリックでメニューを開きます。
- 文字の書式設定、画像や手書きの挿入、チェックリストの作成ができます。チェックボックスで完了を切り替え、Enter で項目を追加できます。`- [ ]` や `- [x]` の Markdown タスクも貼り付けられます。
- 一覧と新規付箋の既定色や明暗テーマを設定し、ウィンドウを画面の端や近くの付箋に揃えられます。キー割り当ては **設定 → ショートカット** で変更できます。

付箋とグループは `%LOCALAPPDATA%\PersonalStickyNote\notes.xml` に保存され、バックアップも作成されます。アカウントなしで使用できます。プライベートリポジトリから更新を確認する場合は、このリポジトリの **Contents** に読み取り専用でアクセスできる GitHub トークンが必要です。

リポジトリのルートで `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` を実行すると、`dist` に実行ファイルが生成されます。翻訳は [`src/Strings.tsv`](../src/Strings.tsv) にあります。
