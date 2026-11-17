# Changelog

All notable changes to EasyPDM for SolidWorks are documented in this file. Until 0.9 the macros
and the drawing sheet were released with EasyPDM itself; see the
[EasyPDM changelog](https://github.com/pawelcel/EasyPDM/blob/main/CHANGELOG.md) for that history.

## [Unreleased]

### Added
- **The EasyPDM add-in**: an EasyPDM tab in the CommandManager for parts, assemblies and drawings, and a Tools → EasyPDM menu, with Upload, Download, Log in and Log out. The commands run the EasyPDM macros, compiled next to the add-in, so they behave exactly like the macros.
- **Version stamp on every upload.** The Upload macro writes a new `EasyPDM_ContentVersion` custom property into the part or assembly before sending it, and sends it along, so EasyPDM (0.9.1 or later for files copied into its shared storage) can tell a copy of that upload from an older or a changed one, also after SolidWorks saves the file again unchanged.
- **A repository of its own**, moved out of EasyPDM (`EasyPDM.SolidWorks/`) with its history.
