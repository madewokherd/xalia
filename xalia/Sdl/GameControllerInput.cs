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

        private GameControllerInput(SdlSynchronizationContext sdl)
        {
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
                        Console.WriteLine($"button {(SDL.SDL_GameControllerButton)button.button} pressed");
                        break;
                    }
                case SDL.SDL_EventType.SDL_CONTROLLERBUTTONUP:
                    {
                        var button = e.SdlEvent.cbutton;
                        Console.WriteLine($"button {(SDL.SDL_GameControllerButton)button.button} released");
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
            }
        }

        private void OpenJoystick(int index)
        {
            if (game_controllers.ContainsKey(index))
                return;
#if DEBUG
            Console.WriteLine($"Game controller connected: {index}");
#endif
            game_controllers[index] = SDL.SDL_GameControllerOpen(index);
        }

        private void CloseJoystick(int index)
        {
            if (game_controllers.TryGetValue(index, out IntPtr controller))
            {
#if DEBUG
                Console.WriteLine($"Game controller disconnected: {index}");
#endif
                SDL.SDL_GameControllerClose(controller);
                game_controllers.Remove(index);
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

        protected override bool UnwatchAction(string name)
        {
            throw new NotImplementedException();
        }

        protected override bool WatchAction(string name)
        {
            throw new NotImplementedException();
        }
    }
}
