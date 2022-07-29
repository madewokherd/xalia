# Introduction
Xalia is a program that provides a gamepad UI for traditional desktop applications. It does this using accessibility platforms like AT-SPI2 and UIAutomation(not yet implemented), a unique rule-based language, the .NET standard, and SDL2.

Rather than directly simulate a keyboard or mouse through gamepad inputs (which can be done using the AntiMicro projects or Steam Input), Xalia scans the active window for controls it can interact with, such as buttons and text boxes. An analog joystick or D-Pad can then be used to navigate to a specific control. There is no virtual mouse cursor, it simply jumps to a control in the direction pressed. The way you then interact with the control depends on what it is, but in simple cases like buttons it can be activated with the A button by default.

Global actions are also possible, such as opening a program's menus or switching tabs, without needing to navigate to a specific control.

# Current Status

## Linux

Linux/X11 is supported using AT-SPI2.

In some environments, AT-SPI2 will only work in the first graphical section after a reboot due to https://gitlab.gnome.org/GNOME/at-spi2-core/-/issues/84. This can be worked around by manually starting `/usr/libexec/at-spi2-registryd`, but applications started before this still will not work.

Qt applications running inside Flatpak do not work due to https://bugs.kde.org/show_bug.cgi?id=452132.

I have not tested on Wayland yet, but I'm not aware of anything that would prevent it from working right now. However, some features require keyboard injection which is not yet implemented for Wayland. These are noted under "Toolkit Support" as requring "XTEST".

## Windows

Windows is supported using UI Automation. Sometimes it is slow to respond to UI updates.

## Toolkit support

Widget \ Toolkit | Linux/GTK2 | Linux/GTK3 | Linux/Qt | Windows/Comctl32
--- | --- | --- | --- | ---
Button | Supported | Supported | Supported | Supported
Check Box | Supported | Supported | Supported | Supported
Combo Box | Broken[5] | Supported | Supported | Supported
Menu | Supported[2] | Supported[2] | Partial[1] | Supported
Text Entry | Partial[3] | Partial[3] | Partial[3] | Partial[4]
Tab Bar | Supported | Supported | Supported | Unsupported

Notes:
1. Menus on Linux/Qt cannot be accessed through AT-SPI2 and require XTEST.
2. Dismissing pop-up menus on Linux/GTK requires XTEST: https://gitlab.gnome.org/GNOME/gtk/-/issues/5008
3. I have not found an On-Screen Keyboard on Linux I can use that supports gamepad input or interaction through AT-SPI2. For now, the "show keyboard" functionality is hard-coded to start "onboard".
4. The Windows On-Screen Keyboard cannot be controlled with a gamepad. I am hoping to add this support in the future using UI Automation.
5. It's not possible to reliably determine when a combo box is open with GTK2, which can cause strange behaviors. This is unlikely to ever be fixed because GTK2 is no longer in development.

# Setup

The latest release can be downloaded from https://github.com/madewokherd/xalia/releases

## Linux

Requirements:
 * A .NET runtime. This can either be Mono or .NET 6. The "self-contained" archives contain a build of .NET 6. When using Mono, the "Facade" assemblies (which are needed to support .NET Standard applications) are required; for some reason, Ubuntu has them in the `mono-devel` package.
 * SDL2. This should just be a matter of installing the libsdl2 packages on your distribution (`libsdl2-2.0-0` on Ubuntu).
 * AT-SPI2. This is probably included with your desktop environment, but you will need to enable it.

You will need to enable AT-SPI2. On XFCE, this can be done by starting "Accessibility" in the applications menu and enabling the checkbox labeled "Enable assistive technologies". On Plasma, the option is in Accessibility settings under the Screen Reader tab, misleadingly named "Screen reader enabled". You will need to log in again for the change to take effect.

For Qt applications, you may need to run this command:
```
gsettings set org.gnome.desktop.a11y.applications screen-reader-enabled true
```
or set the environment variable `QT_LINUX_ACCESSIBILITY_ALWAYS_ON=1`.

Once all of the setup is complete, run `mono xalia.exe`, `dotnet xalia.dll`, or './xalia' in a terminal, depending on which build you are using. To quit, press Ctrl+C in the terminal. There is no GUI available for configuration and starting/stopping the program yet, but hopefully that will change in the future.

## Windows

Run `xalia.exe` from the net48-mono or net6-windows zip.

If you are using 32-bit Windows, you will need to use the net48-mono build and replace SDL.dll with a win32-x86 version from http://libsdl.org/download-2.0.php.

# Building

I do my development on Windows using Visual Studio.

We need a special build of Tmds.DBus to work around an incompatibility with Mono: https://github.com/tmds/Tmds.DBus/issues/155. A submodule of Tmds.DBus is provided for this reason, but it needs to be manually built before Xalia.

SDL.dll and README-SDL.txt from http://libsdl.org/download-2.0.php need to be dropped in the `xalia` project directory before building.

Mono should hopefully also be able to build the project, but I have not tried it.

The single-assembly version (which works on both Linux/Mono and Windows/.NET Framework 4.8), can be built from xalia.sln.

A .NET 6 version can be built with one of the following commands:

```
dotnet publish xalia-netcore.sln --runtime linux-x64 --configuration Release-Linux --self-contained
dotnet publish xalia-netcore.sln --runtime linux-x64 --configuration Release-Linux --no-self-contained
dotnet publish xalia-netcore.sln --runtime win-x64 --configuration Release-Windows --self-contained
dotnet publish xalia-netcore.sln --runtime win-x64 --configuration Release-Windows --no-self-contained
```

# Default Gamepad Controls

Here are the default controls when using a gamepad:
 * **D-Pad** or **Left Stick**: Navigate between controls or within menus.
 * **A (or bottom face button)**: Activate the targeted control or menu.
 * **X (or top face button)**: Secondary action for the selected control, currently this opens a virtual keyboard when a combo box containing a text field is targeted.
 * **B (or right face button)**: Exit a submenu, or click the Close/Cancel/No button if one exists in the current window.
 * **Start**: Toggle the application menu, or click the OK/Yes button if one exists in the current window.
 * **LB (or front left shoulder button)** and **RB (or front right shoulder button)**: Switch to the previous or next tab respectively.
 * **Right Stick**: Select an option in a collapsed combo box.

This is based on SDL2's GameController mapping, which should use the same layout for whatever controller you have, but the buttons may be labeled differently.

# GUDL

Controls and UI interactions are coded in a specialized language called GUDL. It looks a little bit like CSS. Here's some of the code that supports combo boxes:

```css
if (interactable) {
    combo_box {
        targetable: true;

        if (targeted) {
            action_on_A: spi_action.press;
            action_name_A: "Select";
            child (spi_role.text) {
                action_on_Y: set_focus+show_keyboard;
                action_name_Y: "Show Keyboard";
            }
        }
    }
}
```

The language works based on pattern-matching. In this example we are searching for an element with the combo_box role that is "interactable" (generally, in the active window while there isn't a menu open - the exact definition is defined within GUDL). All matching controls are marked as "targetable" which makes it possible to navigate to it. If the control is targeted, it also has actions defined on it. If a targeted combo box has a child with the "text" role then a secondary action to open an on-screen keyboard is defined on that.

All of these conditions are monitored, and if any of them changes (for example, a menu opens, making the combo box no longer "interactable"), the declarations inside no longer apply.

# TODO list

 * Develop a GUI for starting/stopping Xalia, configuring controls, and configuring startup behavior. This will require an RPC service and some refactoring of `main.gudl`.
 * Expand support for toolkits and their standard controls. I would like to have the standard controls of GTK, wxWidgets (Linux and Windows), Qt (Linux and Windows), MFC, Windows Forms, WPF, and UWP fully supported. Individual programs and custom controls are in scope, if possible, but each one may require special attention.
 * Support all builtin features of AT-SPI2 and UIAutomation/MSAA through GUDL.
 * Fully document GUDL.
 * Provide a window showing button prompts for the currently available actions. This would be a separate program making use of an RPC service.
