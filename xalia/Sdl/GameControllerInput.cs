using System;
using System.Collections.Generic;
using System.Threading;

using Xalia.Input;

using static SDL2.SDL;

namespace Xalia.Sdl
{
    internal class GameControllerInput : InputBackend
    {
        static bool initialized;

        Dictionary<int, IntPtr> game_controllers = new Dictionary<int, IntPtr>();

        string[] button_names =
        {
            "A",
            "B",
            "X",
            "Y",
            "back",
            "guide",
            "start",
            "LS",
            "RS",
            "LB",
            "RB",
            "dpad_up",
            "dpad_down",
            "dpad_left",
            "dpad_right",
            "misc1",
            "paddle1",
            "paddle2",
            "paddle3",
            "paddle4",
            "touchpad",

            // not actually buttons, not recognized by SDL:
            "left_stick",
            "right_stick",
            "LT",
            "RT",
        };

        const int BUTTON_LEFTSTICK = 11;
        const int BUTTON_RIGHTSTICK = 12;
        const int BUTTON_LEFTTRIGGER = 13;
        const int BUTTON_RIGHTTRIGGER = 14;

        Dictionary<string, byte> buttons_by_name;

        List<int>[] controllers_providing_button;

        InputMapping[][] button_mappings;
        InputMapping[] empty_mapping = new InputMapping[] { };

        Dictionary<int, bool[]> button_states;

        private GameControllerInput(SdlSynchronizationContext sdl)
        {
            buttons_by_name = new Dictionary<string, byte>();
            button_mappings = new InputMapping[button_names.Length][];
            for (byte i = 0; i < button_names.Length; i++)
            {
                var name = button_names[i];
                buttons_by_name[name] = i;
                var mapping = new InputMapping(name, $"game_{name}.png");
                button_mappings[i] = new InputMapping[] { mapping };
            }

            controllers_providing_button = new List<int>[button_names.Length];
            button_states = new Dictionary<int, bool[]>();

            sdl.SdlEvent += OnSdlEvent;

            for (int i = 0; i < SDL_NumJoysticks(); i++)
            {
                OpenJoystick(i);
            }
        }

        static bool DebugInput = !(Environment.GetEnvironmentVariable("XALIA_DEBUG_INPUT") is null &&
            Environment.GetEnvironmentVariable("XALIA_DEBUG_INPUT") != "0");

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            switch (e.SdlEvent.type)
            {
                case SDL_EventType.SDL_CONTROLLERAXISMOTION:
                    {
                        var axis = e.SdlEvent.caxis;
                        if (DebugInput)
                            Utils.DebugWriteLine($"axis {(SDL_GameControllerAxis)axis.axis} value updated to {axis.axisValue}");
                        switch ((SDL_GameControllerAxis)axis.axis)
                        {
                            case SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX:
                            case SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY:
                                ActionStateUpdated("left_stick");
                                break;
                            case SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX:
                            case SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY:
                                ActionStateUpdated("right_stick");
                                break;
                            case SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT:
                                ActionStateUpdated("LT");
                                break;
                            case SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT:
                                ActionStateUpdated("RT");
                                break;
                        }
                        break;
                    }
                case SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                    {
                        var button = e.SdlEvent.cbutton;
                        if (DebugInput)
                            Utils.DebugWriteLine($"button {(SDL_GameControllerButton)button.button} pressed on controller {button.which}");
                        if (button_states.TryGetValue(button.which, out var states))
                        {
                            states[button.button] = true;
                        }
                        ActionStateUpdated(button_names[button.button]);
                        break;
                    }
                case SDL_EventType.SDL_CONTROLLERBUTTONUP:
                    {
                        var button = e.SdlEvent.cbutton;
                        if (DebugInput)
                            Utils.DebugWriteLine($"button {(SDL_GameControllerButton)button.button} released on controller {button.which}");
                        if (button_states.TryGetValue(button.which, out var states))
                        {
                            states[button.button] = false;
                        }
                        ActionStateUpdated(button_names[button.button]);
                        break;
                    }
                case SDL_EventType.SDL_CONTROLLERDEVICEADDED:
                    {
                        var device = e.SdlEvent.cdevice;
                        OpenJoystick(device.which);
                        break;
                    }
                case SDL_EventType.SDL_CONTROLLERDEVICEREMOVED:
                    {
                        var device = e.SdlEvent.cdevice;
                        CloseJoystick(device.which);
                        break;
                    }
                case SDL_EventType.SDL_CONTROLLERDEVICEREMAPPED:
                    {
                        var device = e.SdlEvent.cdevice;
                        UpdateMappings(device.which);
                        break;
                    }
            }
        }

        private void UpdateMappings(int index, bool disconnected = false)
        {
            IntPtr game_controller = game_controllers[index];
            for (int button = 0; button < button_names.Length; button++)
            {
                bool bind_exists;

                if (disconnected)
                    bind_exists = false;
                else
                {

                    switch (button_names[button])
                    {
                        case "left_stick":
                            {
                                var bind = SDL_GameControllerGetBindForAxis(game_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX);
                                bind_exists = bind.bindType != SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_NONE;
                                // FIXME: Check for LEFTY?
                                break;
                            }
                        case "right_stick":
                            {
                                var bind = SDL_GameControllerGetBindForAxis(game_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX);
                                bind_exists = bind.bindType != SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_NONE;
                                // FIXME: Check for RIGHTY?
                                break;
                            }
                        case "LT":
                            {
                                var bind = SDL_GameControllerGetBindForAxis(game_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT);
                                bind_exists = bind.bindType != SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_NONE;
                                break;
                            }
                        case "RT":
                            {
                                var bind = SDL_GameControllerGetBindForAxis(game_controller, SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT);
                                bind_exists = bind.bindType != SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_NONE;
                                break;
                            }
                        default:
                            {
                                var bind = SDL_GameControllerGetBindForButton(game_controller, (SDL_GameControllerButton)button);
                                bind_exists = bind.bindType != SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_NONE;
                                break;
                            }
                    }
                }

                bool bind_existed = controllers_providing_button[button]?.Contains(index) ?? false;

                if (bind_existed == bind_exists)
                    continue;

                if (bind_exists)
                {
                    if (controllers_providing_button[button] is null)
                        controllers_providing_button[button] = new List<int>();
                    controllers_providing_button[button].Add(index);
                    if (controllers_providing_button[button].Count == 1)
                    {
                        ActionMappingUpdated(button_names[button], button_mappings[button]);
                    }
                }
                else
                {
                    controllers_providing_button[button].Remove(index);
                    if (controllers_providing_button[button].Count == 0)
                    {
                        ActionMappingUpdated(button_names[button], empty_mapping);
                    }
                }
                ActionStateUpdated(button_names[button]);
            }
        }

        private void OpenJoystick(int device_index)
        {
            var game_controller = SDL_GameControllerOpen(device_index);
            var joystick = SDL_GameControllerGetJoystick(game_controller);
            var index = SDL_JoystickInstanceID(joystick);
            if (DebugInput)
                Utils.DebugWriteLine($"Game controller connected: {index}");
            game_controllers[index] = game_controller;
            button_states[index] = new bool[button_names.Length];
            UpdateMappings(index);
        }

        private void CloseJoystick(int index)
        {
            if (game_controllers.TryGetValue(index, out IntPtr controller))
            {
                if (DebugInput)
                    Utils.DebugWriteLine($"Game controller disconnected: {index}");
                UpdateMappings(index, disconnected: true);
                SDL_GameControllerClose(controller);
                game_controllers.Remove(index);
                button_states.Remove(index);
            }
        }

        public static void Init()
        {
            if (initialized)
            {
                throw new InvalidOperationException("Init must only be called once");
            }

            var context = SynchronizationContext.Current;

            if (context is SdlSynchronizationContext sdl)
            {
                SDL_GameControllerAddMappingsFromFile("gamecontrollerdb.txt");
                SDL_GameControllerEventState(SDL_ENABLE);
                new GameControllerInput(sdl);
                initialized = true;
            }
            else
            {
                throw new InvalidOperationException("SynchronizationContext.Current must be SdlSynchronizationContext");
            }
        }

        protected internal override bool WatchAction(string name)
        {
            if (buttons_by_name.TryGetValue(name, out var button))
            {
                if ((controllers_providing_button[button]?.Count ?? 0) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        protected internal override bool UnwatchAction(string name)
        {
            // We don't need to do anything to watch the button, so always just return whether there's a binding
            return WatchAction(name);
        }

        protected internal override InputState GetActionState(string action)
        {
            InputState result = default;

            switch (action)
            {
                case "left_stick":
                case "right_stick":
                    {
                        var x_axis = action == "left_stick" ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX;
                        var y_axis = action == "left_stick" ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY;

                        var controllers = controllers_providing_button[action == "left_stick" ? BUTTON_LEFTSTICK : BUTTON_RIGHTSTICK];
                        if ((controllers?.Count ?? 0) != 0)
                        {
                            foreach (var controller in controllers)
                            {
                                var axis_state = new InputState();
                                axis_state.Kind = InputStateKind.AnalogJoystick;
                                axis_state.XAxis = SDL_GameControllerGetAxis(game_controllers[controller], x_axis);
                                axis_state.YAxis = SDL_GameControllerGetAxis(game_controllers[controller], y_axis);

                                result = InputState.Combine(result, axis_state);
                            }
                            return result;
                        }

                        break;
                    }
                case "LT":
                case "RT":
                    {
                        var axis = action == "LT" ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT;

                        var controllers = controllers_providing_button[action == "LT" ? BUTTON_LEFTTRIGGER : BUTTON_RIGHTTRIGGER];
                        if ((controllers?.Count ?? 0) != 0)
                        {
                            foreach (var controller in controllers)
                            {
                                var axis_state = new InputState();
                                axis_state.Kind = InputStateKind.AnalogButton;
                                axis_state.XAxis = SDL_GameControllerGetAxis(game_controllers[controller], axis);

                                result = InputState.Combine(result, axis_state);
                            }
                            return result;
                        }

                        break;
                    }
            }

            if (buttons_by_name.TryGetValue(action, out var button))
            {
                var controllers = controllers_providing_button[button];
                if ((controllers?.Count ?? 0) != 0)
                {
                    foreach (var controller in controllers)
                    {
                        var states = button_states[controller];
                        if (states[button])
                        {
                            result.Kind = InputStateKind.Pressed;
                            return result;
                        }
                    }
                    result.Kind = InputStateKind.Released;
                    return result;
                }
            }
            result.Kind = InputStateKind.Disconnected;
            return result;
        }
    }
}
