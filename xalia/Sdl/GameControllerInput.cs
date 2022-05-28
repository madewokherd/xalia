using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xalia.Input;

using SDL2;

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
        };

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

            for (int i = 0; i < SDL.SDL_NumJoysticks(); i++)
            {
                OpenJoystick(i);
            }
        }

        private void OnSdlEvent(object sender, SdlSynchronizationContext.SdlEventArgs e)
        {
            switch (e.SdlEvent.type)
            {
                case SDL.SDL_EventType.SDL_CONTROLLERAXISMOTION:
                    {
                        var axis = e.SdlEvent.caxis;
                        Console.WriteLine($"axis {(SDL.SDL_GameControllerAxis)axis.axis} value updated to {axis.axisValue}");
                        break;
                    }
                case SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                    {
                        var button = e.SdlEvent.cbutton;
#if DEBUG
                        Console.WriteLine($"button {(SDL.SDL_GameControllerButton)button.button} pressed on controller {button.which}");
#endif
                        if (button_states.TryGetValue(button.which, out var states))
                        {
                            states[button.button] = true;
                        }
                        ActionStateUpdated(button_names[button.button]);
                        break;
                    }
                case SDL.SDL_EventType.SDL_CONTROLLERBUTTONUP:
                    {
                        var button = e.SdlEvent.cbutton;
#if DEBUG
                        Console.WriteLine($"button {(SDL.SDL_GameControllerButton)button.button} released on controller {button.which}");
#endif
                        if (button_states.TryGetValue(button.which, out var states))
                        {
                            states[button.button] = false;
                        }
                        ActionStateUpdated(button_names[button.button]);
                        break;
                    }
                case SDL.SDL_EventType.SDL_CONTROLLERDEVICEADDED:
                    {
                        var device = e.SdlEvent.cdevice;
                        OpenJoystick(device.which);
                        break;
                    }
                case SDL.SDL_EventType.SDL_CONTROLLERDEVICEREMOVED:
                    {
                        var device = e.SdlEvent.cdevice;
                        CloseJoystick(device.which);
                        break;
                    }
                case SDL.SDL_EventType.SDL_CONTROLLERDEVICEREMAPPED:
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
            for (int button=0; button < button_names.Length; button++)
            {
                var bind = SDL.SDL_GameControllerGetBindForButton(game_controller, (SDL.SDL_GameControllerButton)button);

                bool bind_existed = controllers_providing_button[button]?.Contains(index) ?? false;

                bool bind_exists = !disconnected &&
                    bind.bindType != SDL.SDL_GameControllerBindType.SDL_CONTROLLER_BINDTYPE_NONE;

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
                        ActionStateUpdated(button_names[button]);
                    }
                }
                else
                {
                    controllers_providing_button[button].Remove(index);
                    if (controllers_providing_button[button].Count == 0)
                    {
                        ActionMappingUpdated(button_names[button], empty_mapping);
                        ActionStateUpdated(button_names[button]);
                    }
                }
            }
        }

        private void OpenJoystick(int device_index)
        {
            var game_controller = SDL.SDL_GameControllerOpen(device_index);
            var joystick = SDL.SDL_GameControllerGetJoystick(game_controller);
            var index = SDL.SDL_JoystickInstanceID(joystick);
#if DEBUG
            Console.WriteLine($"Game controller connected: {index}");
#endif
            game_controllers[index] = game_controller;
            button_states[index] = new bool[button_names.Length];
            UpdateMappings(index);
        }

        private void CloseJoystick(int index)
        {
            if (game_controllers.TryGetValue(index, out IntPtr controller))
            {
#if DEBUG
                Console.WriteLine($"Game controller disconnected: {index}");
#endif
                UpdateMappings(index, disconnected: true);
                SDL.SDL_GameControllerClose(controller);
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
                SDL.SDL_GameControllerAddMappingsFromFile("gamecontrollerdb.txt");
                SDL.SDL_GameControllerEventState(SDL.SDL_ENABLE);
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
