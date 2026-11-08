# Notes personnelles

[简体中文](../README.md) · [English](README.en.md) · Français · [Español](README.es.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

`Personal Sticky Note_FilnaDo` est une application de notes locales pour Windows. Double-cliquez sur [PersonalStickyNote.exe](../updates/PersonalStickyNote.exe) sous Windows 10 ou 11 avec .NET Framework 4.8 ou ultérieur.

Dans la liste des notes, cliquez sur le globe à côté de **Paramètres**. Par défaut, l’application suit la langue d’affichage de Windows et utilise l’anglais si cette langue est indisponible. Vous pouvez choisir le chinois simplifié, l’anglais, le français, l’espagnol, le japonais ou le coréen. Votre choix est appliqué et enregistré immédiatement. Les sélecteurs de fichiers et de couleurs de Windows utilisent la langue du système.

- Gardez la liste ou une note au premier plan. Activez **Démarrer à l’ouverture de session Windows** pour afficher la liste et les notes épinglées à leur emplacement enregistré.
- Créez, renommez, recherchez et regroupez vos notes. Faites glisser une note pour la déplacer ou la réorganiser. Le clic droit donne accès à d’autres actions.
- Mettez le texte en forme, insérez des images ou des dessins et créez des listes de tâches. Cliquez sur la case pour terminer une tâche. Appuyez sur Entrée pour ajouter une tâche ou collez des tâches Markdown avec `- [ ]` et `- [x]`.
- Choisissez les couleurs de la liste et des nouvelles notes, un thème clair ou sombre et l’alignement des fenêtres. Modifiez les raccourcis dans **Paramètres → Raccourcis**.

Les notes et les groupes sont enregistrés dans `%LOCALAPPDATA%\PersonalStickyNote\notes.xml`, avec une copie de sauvegarde. Vous pouvez utiliser l’application sans compte. La recherche de mises à jour depuis le dépôt privé nécessite un jeton GitHub à accès précis avec la permission **Contents** en lecture seule pour ce dépôt.

Pour compiler, exécutez `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` à la racine du dépôt. Le programme se trouve dans `dist`. Les traductions de l’interface sont dans [`src/Strings.tsv`](../src/Strings.tsv).
