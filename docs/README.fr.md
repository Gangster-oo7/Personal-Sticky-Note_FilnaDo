# Notes personnelles

[简体中文](../README.md) · [English](README.en.md) · Français · [Español](README.es.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

`Personal Sticky Note_FilnaDo` est une application de notes locales pour Windows.

**[Télécharger PersonalStickyNote.exe](https://github.com/Gangster-oo7/Personal-Sticky-Note_FilnaDo/raw/refs/heads/main/updates/PersonalStickyNote.exe)** · Première version publique **v1.0.1**.

Sous Windows 10 ou 11 avec .NET Framework 4.8 ou ultérieur, double-cliquez sur l’EXE téléchargé pour démarrer. Gardez-le dans un dossier permanent où vous pouvez écrire pour permettre les mises à jour et conserver vos raccourcis.

Si vous utilisiez une version de test dont le numéro était supérieur, fermez-la et remplacez une fois son EXE par la v1.0.1. Vous pourrez installer les prochaines mises à jour depuis l’application.

Dans la liste des notes, cliquez sur le globe à côté de **Paramètres**. Par défaut, l’application suit la langue d’affichage de Windows et utilise l’anglais si cette langue est indisponible. Vous pouvez choisir le chinois simplifié, l’anglais, le français, l’espagnol, le japonais ou le coréen. Votre choix est appliqué et enregistré immédiatement. Les sélecteurs de fichiers et de couleurs de Windows utilisent la langue du système.

- Gardez la liste ou une note au premier plan. Activez **Démarrer à l’ouverture de session Windows** pour afficher la liste et les notes épinglées à leur emplacement enregistré.
- Créez, renommez, recherchez et regroupez vos notes. Faites glisser une note pour la déplacer ou la réorganiser. Le clic droit donne accès à d’autres actions.
- Mettez le texte en forme, insérez des images ou des dessins et créez des listes de tâches. Cliquez sur la case pour terminer une tâche. Appuyez sur Entrée pour ajouter une tâche.
- Choisissez les couleurs de la liste et des nouvelles notes, un thème clair ou sombre et l’alignement des fenêtres. Modifiez les raccourcis dans **Paramètres → Raccourcis**.

Les notes, groupes et réglages sont stockés dans `%LOCALAPPDATA%\PersonalStickyNote\notes.xml` pour le compte Windows actuel, avec une sauvegarde dans le même dossier. Déplacer l’EXE conserve les données de ce compte. Un autre compte ou ordinateur utilise son propre dossier. Si vous déplacez l’EXE, recréez le raccourci du bureau et désactivez puis réactivez le démarrage automatique pour mettre son chemin à jour.

Vérifiez les mises à jour dans les paramètres ou le menu de notification, sans compte GitHub.

Pour compiler, exécutez `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1` à la racine du dépôt. Le programme se trouve dans `dist`. Les traductions de l’interface sont dans [`src/Strings.tsv`](../src/Strings.tsv).
