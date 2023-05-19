# Introduction
Xalia is a program that provides a gamepad UI for traditional desktop applications. It does this using accessibility platforms like AT-SPI2 and UIAutomation, a unique rule-based language, the .NET standard, and SDL2.

Rather than directly simulate a keyboard or mouse through gamepad inputs (which can be done using AntiMicroX or Steam Input), Xalia scans the active window for controls it can interact with, such as buttons and text boxes. An analog joystick or D-Pad can then be used to navigate to a specific control. There is no virtual mouse cursor, it simply jumps to a control in the direction pressed. The way you then interact with the control depends on what it is, but in simple cases like buttons it can be activated with the A button by default.

Global actions are also possible, such as opening a program's menus or switching tabs, without needing to navigate to a specific control.

# Current Status

## Linux

Linux/X11 is supported using AT-SPI2.

Wayland has not been tested yet, but it probably does not work.

## Windows

Windows is supported using UI Automation. Sometimes it is slow to respond to UI updates.

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

SDL.dll and README-SDL.txt from http://libsdl.org/download-2.0.php need to be dropped into the `xalia` project directory before building.

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
 * **B (or right face button)**: Exit a submenu, click the Close/Cancel/No button if one exists, or press Escape (if the current window has any control that Xalia can interact with).
 * **X (or left face button)**: Secondary action for the selected control, currently this opens a virtual keyboard when a combo box containing a text field is targeted, or double-clicks on list items.
 * **Y (or top face button)**: Activate the context menu of the targeted control, or right-click.
 * **Start**: Toggle the application menu, click the OK/Yes button if one exists, or press Enter (if the current window has any control that Xalia can interact with).
 * **Back/Select**: Cycle through controls.
 * **Right Stick**: Adjust the selected control (for scrollbars and slider controls), scroll the view, or simulate the mouse wheel.
 * **LB (or front left shoulder button)** and **RB (or front right shoulder button)**: Switch to the previous or next tab respectively.

This is based on SDL2's GameController mapping, which should use the same layout for whatever controller you have, but the buttons may be labeled differently.

# GUDL

Controls and UI interactions are coded in a specialized language called GUDL. It looks a little bit like CSS. Here's some of the code (simplified) that supports combo boxes:

```css
if (interactable) {
    combo_box (not uia_expand_collapse_state.leaf_node) {
        targetable: true;

        primary_action: spi_action.(press or Press) or uia_expand;
        primary_action_name: "Select";

        if (child_matches(text_box)) {
            secondary_action: child_matches(text_box).set_focus + show_keyboard;
            secondary_action_name: "Show Keyboard";
        }
    }
}
```

The language works based on pattern-matching. In this example we are searching for an element with the combo_box role that is "interactable" (generally, in the active window while there isn't a menu open - the exact definition is defined within GUDL). All matching controls are marked as "targetable" which makes it possible to navigate to it. If the control is targeted, it also has actions defined on it. If a targeted combo box has a child with the "text" role then a secondary action to open an on-screen keyboard is defined.

All of these conditions are monitored, and if any of them changes (for example, a menu opens, making the combo box no longer "interactable"), the declarations inside no longer apply.

# TODO list

 * Rewrite the AT-SPI2 code using Tmds.Dbus.Protocol, which will solve a limitation on the number of match rules one can have on DBus.
 * Allow for watching X11 state, which is useful for detecting window decorations, and objects invisible to AT-SPI2 (due to bugs).
 * Add late evaluation and function declarations to GUDL, as the limitations are unweildy.
 * Add "include" directive to GUDL so that toolkit and application specific behaviors, and user settings, can be organized better.
 * Fill out support for some Linux desktops and their builtin applications.
 * Create an AT-SPI2 bridge for Win32/MSAA/UIAutomation, which will be a separate project, and use that from Xalia. This can be shared with Wine and will solve some difficulties with MSAA an UIAutomation. It's also necessary if we want Xalia to work correctly with applications running in Wine.
 * Fill out support for the Windows desktop and its builtin applications.
 * Add support for the ISimpleDOM interfaces (web browsers).
 * Develop a GUI for starting/stopping Xalia, configuring controls, and configuring startup behavior. This will require an RPC service.
 * Fully document GUDL.
 * Provide a window showing button prompts for the currently available actions. This would be a separate program making use of an RPC service.

 # Debugging Environment Variables

`XALIA_DEBUG` can be set to a GUDL boolean expression selecting some elements, for which Xalia will output debugging information. For example, `XALIA_DEBUG=is_root` will output declarations on the root element. Currently, any properties used must be referenced inside `main.gudl` for this to work.

`XALIA_DEBUG_INPUT=1` displays gamepad inputs.

`XALIA_DEBUG_EXCEPTIONS=1` displays expected exceptions.

