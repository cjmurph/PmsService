# <img src="PlexService/PlexService.ico" alt="Plex Service" width="25" height="25" style="width:25px;height:25px"> PmsService 
## The Plex Media Server Service Wrapper for Windows

# What is this?

As the name would imply, this software is a service wrapper for Plex Media Server.

You can use it to run PMS on a Windows computer without having to be logged in. Just install it, set the user it should run as, and you're all set.

Optionally, you can use PlexService to configure remote drive mappings for your media, as well as auxiliary applications to run when Plex is running.

For help and further information please visit the Plex forums:
https://forums.plex.tv/discussion/93994/pms-as-a-service

# Features
- Run Plex Media Server as a background service.
- Handy-dandy tray application, accessible to all users.
- Newly refreshed UI with over 46 different theme combinations.
- Access Plex Appdata folder and web interface from tray application.
- Option to mount UNC shares as mapped drives.
- Option to run/stop Auxiliary applications on Plex start/stop.
- Option to auto-restart Plex if it crashes/stops running.
- Automatically detect PMS updates and disable auto-restart feature until updates complete.
- Automatically detect failed drive mounts and wait a configured amount of time before re-trying.
- Option to not start Plex on drive map failures, preventing accidental library deletion with auto-clean.
- Option to log auxiliary application output to the PlexService log.
- Supports silent installation.

# Installation

Simply download the [latest release](https://github.com/cjmurph/PmsService/releases/latest), run the installer, and enter the user information when prompted.

You can also silently install the package using the following syntax:

```
msiexec.exe /i c:\PlexServiceInstaller.msi /QN SERVICE_USERNAME="MyUsername" SERVICE_PASSWORD="MyPassword"
```
*NOTE: You must run the install command as administrator if executing silently, as the application can't prompt for elevation.
Installation will fail if you don't run your script/command prompt as admin.* 

# Updating
You should be able to upgrade PlexService in-place. In rare occurrences, you may not be able to properly update or uninstall PlexService.

If that happens, see below for steps to manually remove it for a clean installation.


# Manual Uninstallation
To completely remove the PlexService application, do the following:
1. Open an *elevated* command prompt. (Start menu, type 'cmd', right-click, run as administrator)
2. Enter the following commands:
```
sc stop PlexService
taskkill /IM PlexService.exe /F
sc delete PlexService
del /S C:\progra~2\PlexService\
```
3. Optionally, ff you wish to delete your PlexService settings, execute the following command:
```
del /S "C:\users\<PMSSERVICEUSERNAME>\AppData\Local\Plex Service\"
```
4. Finally, reboot your PC to ensure all services are really stopped and released.
