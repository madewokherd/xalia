# Introduction
Xalia is a program that provides a gamepad UI for traditional desktop applications. It does this using accessibility platforms like AT-SPI2 and UIAutomation(not yet implemented), a unique rule-based language, the .NET standard, and SDL2.

Rather than directly simulate a keyboard or mouse through gamepad inputs (which can be done using the AntiMicro projects or Steam Input), Xalia scans the active window for controls it can interact with, such as buttons and text boxes. An analog joystick or D-Pad can then be used to navigate to a specific control. There is no virtual mouse cursor, it simply jumps to a control in the direction pressed. The way you then interact with the control depends on what it is, but in simple cases like buttons it can be activated with the A button by default.

Global controls are also possible, such as opening a program's menus or switching tabs, without needing to navigate to a specific control.

# Current Status

Only the Linux platform with AT-SPI2 is currently working.

The GTK and wxWidgets toolkits work. Standard button, text box, combo box, check box, and tab controls are usable. Activating a textbox is currently hard-coded to run "onboard", unfortunately I could not find any on-screen keyboard for Linux that I could start which would allow for gamepad inputs.

Qt does not work yet due to an issue with discovering Qt windows, and because standard Qt controls work differently from GTK controls. All of those issues appear to be solvable.
