# FCNameColor

A plugin for [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher) that lets you colour character nameplates of your FC’s members.

## Usage

Type `/fcnc` to open up the configuration window.

From here you can tweak various settings:

![Config image](Assets/config.png)

![Example image](Assets/example.png)

## Known issues

- Currently it does not account for players (including yourself) joining or leaving the FC.
- Matching players is done based on Lodestone data, since FC tags are not necessarily unique. Because of this there is not a 100% guarantee it will be accurate when players leave/join or change their names.
- Nameplates do not immediately update when changing colours or disabling/enabling the nameplate colouring options. This is a technical limitation of the method used to override the character nameplates, if someone finds a better way let me know!
