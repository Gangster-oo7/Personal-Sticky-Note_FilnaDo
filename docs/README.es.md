# Notas personales

[简体中文](../README.md) · [English](README.en.md) · [Français](README.fr.md) · Español · [日本語](README.ja.md) · [한국어](README.ko.md)

`Personal Sticky Note_FilnaDo` es una aplicación de notas locales para Windows. Haz doble clic en [PersonalStickyNote.exe](../updates/PersonalStickyNote.exe) en Windows 10 u 11 con .NET Framework 4.8 o posterior.

Pulsa el globo junto a **Ajustes** en la lista de notas. De forma predeterminada, la aplicación usa el idioma de Windows o el inglés si ese idioma no está disponible. Puedes elegir chino simplificado, inglés, francés, español, japonés o coreano. La selección se aplica y se guarda de inmediato. Los selectores de archivos y colores de Windows usan el idioma del sistema.

- Mantén la lista o una nota encima de otras ventanas. Activa **Iniciar al entrar en Windows** para abrir la lista y las notas fijadas en sus posiciones guardadas al iniciar sesión.
- Crea, renombra, busca y agrupa notas. Arrastra una nota para moverla o cambiar su orden. Haz clic con el botón derecho para ver más acciones.
- Da formato al texto, inserta imágenes o dibujos y crea listas de tareas. Marca las casillas para completar tareas. Pulsa Intro para añadir una tarea o pega tareas Markdown con `- [ ]` y `- [x]`.
- Elige colores para la lista y las notas nuevas, cambia entre tema claro y oscuro y ajusta las ventanas a los bordes o a notas cercanas. Personaliza los atajos en **Ajustes → Atajos**.

Las notas y los grupos se guardan en `%LOCALAPPDATA%\PersonalStickyNote\notes.xml`, con una copia de seguridad. Puedes usar la aplicación sin cuenta. Para buscar actualizaciones del repositorio privado, usa un token de GitHub de acceso detallado con permiso de solo lectura en **Contents** para este repositorio.

Para compilar, ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` en la raíz del repositorio. El ejecutable se genera en `dist`. Las traducciones de la interfaz están en [`src/Strings.tsv`](../src/Strings.tsv).
