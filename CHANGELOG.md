# Changelog

All notable changes to EasyPDM for SolidWorks are documented in this file. Until 0.9 the macros
and the drawing sheet were released with EasyPDM itself; see the
[EasyPDM changelog](https://github.com/pawelcel/EasyPDM/blob/main/CHANGELOG.md) for that history.

## [Unreleased]

### Added
- **The EasyPDM add-in**: an EasyPDM tab in the CommandManager for parts, assemblies and drawings, and a Tools → EasyPDM menu. Upload, Download and Log in/out run the EasyPDM macros, compiled next to the add-in, so they behave exactly like the macros.
- **Document panel** in the Task Pane: who is signed in and where, the active document's item (record, name, revision, status, who holds the lock) with Refresh, Open in browser, Lock / Release, Upload and Log in / Log out, and for an assembly the tree of its components marked current, changed here, not in EasyPDM yet or outdated. It follows the active document and asks the server in the background.
- **Lock** and **Log in/out** commands, one button each: Lock is pressed while you hold the lock and greyed out for a released item or someone else's lock; Log in/out is pressed while you are signed in.
- **Upload refuses work built on outdated files**: when the document or one of its components has a newer file in EasyPDM, Upload lists them (who uploaded the newer file and when) instead of sending. Needs an EasyPDM 0.9 server.
- **Upload and Download sign in through the browser first** when there is no valid login, instead of the macros asking for a login and password; the macros' own password login remains only for EasyPDM servers before 0.9.
- English, Polish and German, by the Windows display language.
- **Version stamp on every upload.** The Upload macro writes a new `EasyPDM_ContentVersion` custom property into the part or assembly before sending it, and sends it along, so EasyPDM (0.9.1 or later for files copied into its shared storage) can tell a copy of that upload from an older or a changed one, also after SolidWorks saves the file again unchanged.
- **A repository of its own**, moved out of EasyPDM (`EasyPDM.SolidWorks/`) with its history.
