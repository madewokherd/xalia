# Introduction
Xalia is a program that provides a gamepad UI for traditional desktop applications. It does this using accessibility platforms like AT-SPI2 and UIAutomation(not yet implemented), a unique rule-based language, the .NET standard, and SDL2.

Rather than directly simulate a keyboard or mouse through gamepad inputs (which can be done using the AntiMicro projects or Steam Input), Xalia scans the active window for controls it can interact with, such as buttons and text boxes. An analog joystick or D-Pad can then be used to navigate to a specific control. There is no virtual mouse cursor, it simply jumps to a control in the direction pressed. The way you then interact with the control depends on what it is, but in simple cases like buttons it can be activated with the A button by default.

Global actions are also possible, such as opening a program's menus or switching tabs, without needing to navigate to a specific control.

# Current Status

Only the Linux platform with AT-SPI2 is currently working.

The GTK and wxWidgets toolkits work. Standard button, text box, combo box, check box, and tab controls are usable. Activating a textbox is currently hard-coded to run "onboard", unfortunately I could not find any on-screen keyboard for Linux that I could start which would allow for gamepad inputs.

Qt works but requires separate configuration and keyboard injection support (only implemented on X11).

I have not tested on Wayland yet, but I'm not aware of anything that would prevent it from working right now.

No button prompts are displayed yet. I think this would be good to have in the future.

# Setup

**Binary builds are not yet available. Coming soon.**

## Linux

Requirements:
 * Mono. On Ubuntu, for some reason, the "Facade" assemblies (which are needed to support .NET Standard applications) are in the `mono-devel` package, so you'll need to install that.
 * SDL2. This should just be a matter of installing the libsdl2 packages on your distribution (`libsdl2-2.0-0` on Ubuntu).
 * AT-SPI2. This is probably included with your desktop environment, but you will need to enable it.

You will need to enable AT-SPI2. On XFCE, this can be done by starting "Accessibility" in the applications menu and enabling the checkbox labeled "Enable assistive technologies". You will need to log in again for the change to take effect.

For Qt applications, you will need to run this command:
```
gsettings set org.gnome.desktop.a11y.applications screen-reader-enabled true
```
or set the environment variable `QT_LINUX_ACCESSIBILITY_ALWAYS_ON=1`.

Once all of the setup is complete, run `mono xalia.exe` in a terminal. To quit, press Ctrl+C in the terminal. There is no GUI available for configuration and starting/stopping the program yet, but hopefully that will change in the future.

# Building

I do my development on Windows using Visual Studio. I'm hoping to be able to target both Windows and Linux with a single build.

There is one quirk currently, which is that we need a special build of Tmds.DBus to work around an incompatibility with Mono: https://github.com/tmds/Tmds.DBus/issues/155. I use a build from this revision: https://github.com/madewokherd/Tmds.DBus/commit/c3debb916ba18cf7a39ea244ad59f3a79e8ab2c8

Mono should hopefully also be able to build the project, but I have not tried it.

Similarly, I have not tried .NET Core/5/6, but I'm not aware of any reason it can't work.

# Default Gamepad Controls

Here are the default controls when using a gamepad:
 * **D-Pad** or **Left Stick**: Navigate between controls or within menus.
 * **A (or bottom face button)**: Activate the targeted control or menu.
 * **X (or top face button)**: Secondary action for the selected control, currently this opens a virtual keyboard when a combo box containing a text field is targeted.
 * **B (or right face button)**: Exit a submenu, or click the Close/Cancel/No button if one exists in the current window.
 * **Start**: Toggle the application menu, or click the OK/Yes button if one exists in the current window.
 * **LB (or front left shoulder button)** and **RB (or front right shoulder button)**: Switch to the previous or next tab respectively. (Does not work in Qt applications because it is not possible to determine which tab is selected.)

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

 * Expand support for toolkits and their standard controls. I would like to have the standard controls of GTK, wxWidgets (Linux and Windows), Qt (Linux and Windows), MFC, Windows Forms,  WPF, and UWP fully supported. Individual programs and custom controls are in scope, if possible, but each one may require special attention.
 * Support all builtin features of AT-SPI2 and UIAutomation/MSAA through GUDL.
 * Fully document GUDL.
 * Provide an RPC service for other applications to interact with Xalia, and develop a UI to configure and start/stop it through this service. (Open question: A DBus service would be standard on Linux, but what should we do on Windows?)
 * Provide a window showing button prompts for the currently available actions. (Open question: Should this be built into Xalia or a separate program making use of the RPC service?)
