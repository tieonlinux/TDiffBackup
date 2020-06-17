# TDiffBackup
![CI](https://github.com/tieonlinux/TDiffBackup/workflows/CI/badge.svg)

Terraria TShock World file backup system


## Features
- Automatically backup your *wld* file while TShock is running
- Use [diff algorithm](https://github.com/mendsley/bsdiff) to drastically reduce disk usage
- Restore any backup with a single server command

## Commands
```
/tdiff <date>    | restore wld backup at <date>
/tdiff ls        | list most recent backups
/tdiff ls <date> | list backups close to given date
```

## Permissions
```
tdiff    | allow user/group to issue tdiff commands
```

## Limitations
- This plugin **only** backup the serving *wld* file. **No backup is made for sqlite, json, etc... tshock related files**. Ie if you use SSC you should backup the sqlite *db* files on your own.
- In order to restore a backup the server shuts off and one has to restart it.

## TODOs
- As of now no backup cleanup logic implemented. The backup folder's size keep growing up (slowly thanks to the diff algorithm).

## How it work

### AutoSave
Every time TShock successfully save the world file this plugin create a diff file of the *wld* file in a folder next to the original *wld* file.  
Note that a full copy is made if one of the following condition is meet:
- this is the 1st time a backup is made
- the latest full copy was made more than 7 days ago
- the recents diffs files are taking more than 50% of the current world file size


## Building

### Note
Alternative simplier path to edit & build this plugin is :
- fork
- edit
- commit & push to *github*
- look at **your** repo actions (for instance mine is [here](https://github.com/tieonlinux/TDiffBackup/actions))
- grab your dll in the *Actifacts* sections of the latest job

### 0) Prerequisite
- [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/visual-studio-sdks)
- [Python 3.x](https://www.python.org/downloads/)
- Git

### 1) Bootstraping
automatically download tshock & setup submodules (with namespace renames) using :
```
python3 bootstrap.py
```

### 2) Have fun
Open the solution using your favorite IDE, build the solution and run the unit tests.



## Integration tests
Those tests start a tshock server with the plugin installed and perform some tests

### Dependencies
In case you don't have them already:
```
python3 -m pip install --user requests bsdiff4
```

### Start the tests

```
python3 tests.py
```
Note that it may take some time (~5min) to run all the tests.



## Faq
### Where to download ?
Latest release is in the [release section](https://github.com/tieonlinux/TDiffBackup/releases) of this repo

### My world file is corrupt I can't load it nor use tdiff command
assuming there's at **least 1 backup**:
1. `copy` any *wld* file found in the backup folder to the *worlds folder*
2. `rename` the copied file as the **exact** original *wld* file name.
3. `start` tshock with the copied file (the world may be in a really old version that's not important for now)
4. use `/tdiff <date>` commands to restore the backup you want
5. `restart` tshock