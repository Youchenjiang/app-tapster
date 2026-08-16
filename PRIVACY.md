# Privacy Policy for Tapster

**Last Updated:** August 16, 2026

## Overview
This Privacy Policy describes how **Tapster** ("the Application") handles user data and input interactions.

---

## 100% Offline & Zero Data Collection
**Tapster does not collect, store, track, or transmit any personal information, input logs, keystrokes, telemetry, or analytics data.**

The Application is engineered as a strictly local Windows utility:
- **Local Input Simulation & Recording**: All keystroke simulation, mouse clicking, coordinate detection, and macro recording/replaying operations are executed **100% locally on your device** via native Win32 APIs.
- **Zero Network Connectivity**: The Application does not contain any network sockets, telemetry SDKs, crash report uploaders, or external API endpoints.
- **No Keystroke Logging**: The macro recorder functions solely while actively engaged by the user in the foreground, saves transient actions in local memory, and does not secretly record or transmit background keystrokes.

---

## Local Configuration Storage
User preferences (e.g., startup with Windows, minimize to tray options) are stored exclusively in your local application data folder:
```
%LOCALAPPDATA%\Tapster\settings.json
```
This configuration never leaves your personal computer.

---

## Third-Party Services
Tapster integrates with **zero third-party services, zero cloud platforms, and zero advertising or tracking SDKs**.

---

## Contact
For security questions or concerns regarding this Privacy Policy, please open a discussion or report privately via GitHub:
https://github.com/Youchenjiang/Tapster
