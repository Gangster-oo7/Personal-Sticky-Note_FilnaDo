# 개인 메모

[简体中文](../README.md) · [English](README.en.md) · [Français](README.fr.md) · [Español](README.es.md) · [日本語](README.ja.md) · 한국어

`Personal Sticky Note_FilnaDo`는 메모를 기기에 저장하는 Windows 앱입니다.

**[PersonalStickyNote.exe 다운로드](https://github.com/Gangster-oo7/Personal-Sticky-Note_FilnaDo/raw/refs/heads/main/updates/PersonalStickyNote.exe)** · 첫 공개 버전 **v1.0.1**.

Windows 10 또는 11과 .NET Framework 4.8 이상에서 내려받은 EXE를 두 번 클릭하세요. 업데이트와 바로 가기를 사용하려면 쓰기 권한이 있는 고정 폴더에 보관하세요.

버전 번호가 더 높은 시험판을 사용했다면 앱을 종료한 후 기존 EXE를 v1.0.1로 한 번 교체하세요. 이후에는 앱에서 업데이트할 수 있습니다.

메모 목록에서 **설정** 옆의 지구본 버튼을 누르세요. 기본값은 Windows 표시 언어이며, 지원하지 않는 언어에서는 영어를 사용합니다. 중국어 간체, 영어, 프랑스어, 스페인어, 일본어, 한국어를 선택할 수 있습니다. 선택한 언어는 바로 적용되고 저장됩니다. Windows의 파일 및 색상 선택 창은 시스템 표시 언어를 사용합니다.

- 목록이나 메모를 항상 위에 표시할 수 있습니다. **Windows 로그인 시 시작**을 켜면 로그인할 때 목록과 고정한 메모가 저장된 위치에 열립니다.
- 목록에서 메모를 만들고, 이름을 바꾸고, 검색하고, 그룹으로 정리할 수 있습니다. 드래그로 메모를 옮기거나 순서를 바꾸고, 마우스 오른쪽 버튼으로 추가 메뉴를 엽니다.
- 글자 서식을 바꾸고 이미지나 손그림을 삽입하며 할 일 목록을 만들 수 있습니다. 체크 상자로 완료 상태를 바꾸고 Enter로 항목을 추가합니다.
- 목록과 새 메모의 기본 색상 및 밝은 테마와 어두운 테마를 설정하고, 창을 화면 가장자리나 가까운 메모에 맞출 수 있습니다. 키 조합은 **설정 → 단축키**에서 변경합니다.

메모, 그룹, 설정은 현재 Windows 계정의 `%LOCALAPPDATA%\PersonalStickyNote\notes.xml`에 저장되며 같은 폴더에 백업도 생성됩니다. 같은 계정에서 EXE를 옮겨도 기존 데이터를 읽습니다. 다른 계정이나 컴퓨터는 각각의 데이터 폴더를 사용합니다. EXE를 옮겼다면 바로 가기를 다시 만들고 자동 시작을 껐다가 켜서 경로를 갱신하세요.

설정 또는 알림 영역 메뉴에서 업데이트를 확인할 수 있습니다. GitHub 계정은 필요하지 않습니다.

저장소 루트에서 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`을 실행하면 `dist`에 실행 파일이 생성됩니다. 번역은 [`src/Strings.tsv`](../src/Strings.tsv)에 있습니다.
