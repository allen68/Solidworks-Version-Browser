# SolidWorks Local Version Control

A lightweight, dark-themed version control tool for SolidWorks that works entirely locally (no cloud required). It creates "Time Machine" snapshots of your parts and allows you to branch/experiment safely.

![Screenshot](link-to-screenshot.png)

## Features
* **Auto-Snapshot:** Save versions with a single click inside SolidWorks.
* **Visual History:** View snapshots with JPG previews in a dark-mode browser.
* **Safe Restore:** Restore old versions without overwriting your current file.
* **Branching:** Create "Experiment" branches instantly.
* **Smart Tree:** See your entire PC's file structure with lazy loading.

## How to Install
1. Go to the [Releases](link-to-releases) page.
2. Download the `SolidWorks.Version.Control.zip`.
3. Extract it anywhere (e.g., `C:\SWTools`).
4. Run `VersionBrowser.exe`.

### Setting up the SolidWorks Button
1. Open SolidWorks.
2. Go to **Tools > Macro > New**.
3. Copy the code from `Macro/SnapshotTool.swp` in this repo.
4. Save it and add it to your toolbar.
