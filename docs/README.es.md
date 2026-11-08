# Notas personales

[简体中文](../README.md) · [English](README.en.md) · [Français](README.fr.md) · Español · [日本語](README.ja.md) · [한국어](README.ko.md)

`Personal Sticky Note_FilnaDo` es una aplicación de notas locales para Windows.

**[Descargar PersonalStickyNote.exe](https://github.com/Gangster-oo7/Personal-Sticky-Note_FilnaDo/raw/refs/heads/main/updates/PersonalStickyNote.exe)** · Primera versión pública **v1.0.1**.

En Windows 10 u 11 con .NET Framework 4.8 o posterior, haz doble clic en el EXE descargado para ejecutarlo. Guárdalo en una carpeta fija con permisos de escritura para permitir las actualizaciones y conservar los accesos directos.

Si usabas una versión de prueba con un número superior, ciérrala y sustituye su EXE por la v1.0.1 una sola vez. Podrás instalar las siguientes actualizaciones desde la aplicación.

Pulsa el globo junto a **Ajustes** en la lista de notas. De forma predeterminada, la aplicación usa el idioma de Windows o el inglés si ese idioma no está disponible. Puedes elegir chino simplificado, inglés, francés, español, japonés o coreano. La selección se aplica y se guarda de inmediato. Los selectores de archivos y colores de Windows usan el idioma del sistema.

- Mantén la lista o una nota encima de otras ventanas. Activa **Iniciar al entrar en Windows** para abrir la lista y las notas fijadas en sus posiciones guardadas al iniciar sesión.
- Crea, renombra, busca y agrupa notas. Arrastra una nota para moverla o cambiar su orden. Haz clic con el botón derecho para ver más acciones.
- Da formato al texto, inserta imágenes o dibujos y crea listas de tareas. Marca las casillas para completar tareas. Pulsa Intro para añadir una tarea.
- Elige colores para la lista y las notas nuevas, cambia entre tema claro y oscuro y ajusta las ventanas a los bordes o a notas cercanas. Personaliza los atajos en **Ajustes → Atajos**.

Las notas, los grupos y los ajustes se guardan en `%LOCALAPPDATA%\PersonalStickyNote\notes.xml` para la cuenta de Windows actual, con una copia de seguridad en la misma carpeta. Si mueves el EXE dentro de la misma cuenta, se conservan los datos. Otra cuenta u otro equipo usa su propia carpeta. Si mueves el EXE, vuelve a crear el acceso directo y desactiva y vuelve a activar el inicio automático para actualizar su ruta.

Busca actualizaciones desde los ajustes o el menú del área de notificación. No hace falta una cuenta de GitHub.

Para compilar, ejecuta `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` en la raíz del repositorio. El ejecutable se genera en `dist`. Las traducciones de la interfaz están en [`src/Strings.tsv`](../src/Strings.tsv).
