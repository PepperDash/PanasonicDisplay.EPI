![PepperDash Essentials Plugin Logo](/images/essentials-plugin-blue.png)

# Panasonic Display Essentials Plugin (c) 2025

![Essentials-v2](https://img.shields.io/badge/Essentials-v2-teal.svg)

## License

Provided under MIT license

## Overview

This is a PepperDash Essentials plugin for controlling Panasonic TH series displays
over a streaming connection (RS-232 or TCP/IP). It implements two-way control of power,
input switching, audio volume/mute and video mute.

This plugin now targets **Essentials v2.x** and builds for the Crestron **4-series**
platform. The Essentials v1.x / 3-series line is maintained on the `maintenance/1x`
branch.

## Cloning Instructions

After cloning this repository, dependencies are restored automatically from NuGet on
build. You must have `nuget.exe` installed and on the `PATH` to restore manually; it is
available at [nuget.org](https://dist.nuget.org/win-x86-commandline/latest/nuget.exe).

## Dependencies

The [Essentials](https://github.com/PepperDash/Essentials) libraries (v2.x) are required
and are referenced via NuGet (`PepperDashEssentials`).

## Device Configuration

The device supports the type names `panasonicDisplay` and `panasonicThDisplay`. It uses
the standard Essentials communication block (`control`). Example device config:

```json
{
    "key": "display-1",
    "name": "Panasonic Display",
    "type": "panasonicThDisplay",
    "group": "displays",
    "properties": {
        "control": {
            "method": "tcpIp",
            "tcpSshProperties": {
                "address": "172.22.1.101",
                "port": 1024,
                "autoReconnect": true,
                "autoReconnectIntervalMs": 5000
            }
        }
    }
}
```

For RS-232 control, set `control.method` to `com` and provide the appropriate
`comParams`.

## SIMPL Bridge Join Map

The plugin uses the standard Essentials `DisplayControllerJoinMap` and adds video-mute
joins. Joins are offset by the `joinStart` provided to the bridge.

### Digital

| Join | Type      | Description                  |
|------|-----------|------------------------------|
| 1    | To/From   | Power Off (set/feedback)     |
| 2    | To/From   | Power On (set/feedback)      |
| 3    | From SIMPL| Is Two-Way Display feedback  |
| 5    | To/From   | Volume Up                    |
| 6    | To/From   | Volume Down                  |
| 7    | To/From   | Volume Mute Toggle / feedback|
| 8    | From SIMPL| Is Online feedback           |
| 21   | To/From   | Video Mute Off / feedback    |
| 22   | To/From   | Video Mute On / feedback     |
| 23   | To/From   | Video Mute Toggle / feedback |

### Analog

| Join | Type      | Description                  |
|------|-----------|------------------------------|
| 11   | To/From   | Input Select / feedback      |
| 22   | From SIMPL| Volume Level feedback        |

### Serial

| Join | Type      | Description                  |
|------|-----------|------------------------------|
| 1    | From SIMPL| Device Name                  |

> Note: input select offset and input name joins follow the standard
> `DisplayControllerJoinMap` layout, populated dynamically from the configured input
> ports (HDMI 1, HDMI 2, DVI, VGA).

## Documentation

For detailed documentation about how Essentials plugins work, see the Essentials Wiki
[Plugins](https://pepperdash.github.io/Essentials/docs/Plugins.html) article.
<!-- START Minimum Essentials Framework Versions -->
### Minimum Essentials Framework Versions

- 2.36.5
<!-- END Minimum Essentials Framework Versions -->
<!-- START Config Example -->
### Config Example

```json
{
    "key": "GeneratedKey",
    "uid": 1,
    "name": "GeneratedName",
    "type": "panasonicDisplay",
    "group": "Group",
    "properties": {}
}
```
<!-- END Config Example -->
<!-- START Supported Types -->
### Supported Types

- panasonicDisplay
- panasonicThDisplay
<!-- END Supported Types -->
<!-- START Join Maps -->

<!-- END Join Maps -->
<!-- START Interfaces Implemented -->
### Interfaces Implemented

- IBasicVolumeWithFeedback
- ICommunicationMonitor
- IBridgeAdvanced
<!-- END Interfaces Implemented -->
<!-- START Base Classes -->
### Base Classes

- DisplayControllerJoinMap
- TwoWayDisplayBase
<!-- END Base Classes -->
<!-- START Public Methods -->
### Public Methods

- public void InputHdmi1()
- public void InputHdmi2()
- public void InputHdmi3()
- public void InputHdmi4()
- public void InputDisplayPort1()
- public void InputDisplayPort2()
- public void InputDvi1()
- public void InputVideo1()
- public void InputVga()
- public void InputRgb()
- public void VideoMuteOff()
- public void VideoMuteOn()
- public void VideoMuteToggle()
- public void MuteOff()
- public void MuteOn()
- public void MuteToggle()
- public void VolumeDown(bool pressRelease)
- public void VolumeUp(bool pressRelease)
- public void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
- public void Dispatch()
<!-- END Public Methods -->
<!-- START Bool Feedbacks -->
### Bool Feedbacks

- VideoIsMutedFeedback
- MuteFeedback
<!-- END Bool Feedbacks -->
<!-- START Int Feedbacks -->
### Int Feedbacks

- InputNumberFeedback
- VolumeLevelFeedback
<!-- END Int Feedbacks -->
<!-- START String Feedbacks -->

<!-- END String Feedbacks -->
