using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xalia.Gudl;
using Xalia.Input;
using Xalia.UiDom;
using Xalia.Sdl;

namespace Xalia.Ui
{
    internal class UiMain
    {
        public UiDomRoot Root { get; }
        public WindowingSystem Windowing { get; }

        private Dictionary<UiDomObject, OverlayBox> targetable_boxes = new Dictionary<UiDomObject, OverlayBox>();

        private Dictionary<string, List<UiDomObject>> objects_defining_action = new Dictionary<string, List<UiDomObject>>();
        private Dictionary<UiDomObject, List<string>> object_actions = new Dictionary<UiDomObject, List<string>>();
        private Dictionary<string, UiDomRoutine> locked_inputs = new Dictionary<string, UiDomRoutine>();

        public UiMain(UiDomRoot root)
        {
            Root = root;
            Windowing = new WindowingSystem();

            InputSystem.Instance.ActionStateChangeEvent += OnActionStateChangeEvent;

            root.ElementDeclarationsChangedEvent += OnElementDeclarationsChanged;
            root.ElementDiedEvent += OnElementDied;
            ScanElements(root);
        }

        private void OnActionStateChangeEvent(object sender, InputSystem.ActionStateChangeEventArgs e)
        {
            UiDomRoutine routine;
#if DEBUG
            Console.WriteLine($"Got input: {e.Action} {e.PreviousState} => {e.State}");
#endif
            if (locked_inputs.TryGetValue(e.Action, out routine))
            {
#if DEBUG
                Console.WriteLine($"Passing locked input to routine: {routine}");
#endif
                routine.OnInput(e);

                if (!e.LockInput)
                {
                    locked_inputs.Remove(e.Action);
                    if (objects_defining_action[e.Action].Count == 0)
                        InputSystem.Instance.UnwatchAction(e.Action);
                }
            }
            else if (objects_defining_action.TryGetValue(e.Action, out var objects) &&
                objects.Count != 0)
            {
                if (e.State.Kind == InputStateKind.Disconnected ||
                    e.State.Kind == InputStateKind.Released)
                {
                    return;
                }

                var obj = objects[objects.Count - 1]; // most recently added object for this action

                routine = obj.GetDeclaration("action_on_" + e.Action) as UiDomRoutine;

                if (!(routine is null))
                {
#if DEBUG
                    Console.WriteLine($"Calling routine: {routine}");
#endif
                    routine.OnInput(e);

                    if (e.LockInput)
                    {
                        locked_inputs.Add(e.Action, routine);
                    }
                }
            }
        }

        private void OnElementDied(object sender, UiDomObject e)
        {
            DiscardTargetableElement(e);
            DiscardActions(e);
        }

        private void OnElementDeclarationsChanged(object sender, UiDomObject e)
        {
            UpdateTargetableElement(e);
            UpdateActions(e);
        }

        private void ScanElements(UiDomObject obj)
        {
            OnElementDeclarationsChanged(null, obj);

            foreach (var child in obj.Children)
            {
                ScanElements(child);
            }
        }

        private void UpdateTargetableElement(UiDomObject obj)
        {
            if (!obj.GetDeclaration("targetable").ToBool())
            {
                DiscardTargetableElement(obj);
                return;
            }

            int x, y, width, height;

            if (obj.GetDeclaration("target_x") is UiDomInt xint &&
                obj.GetDeclaration("target_y") is UiDomInt yint &&
                obj.GetDeclaration("target_width") is UiDomInt wint &&
                obj.GetDeclaration("target_height") is UiDomInt hint)
            {
                x = xint.Value;
                y = yint.Value;
                width = wint.Value;
                height = hint.Value;
            }
            else
            {
                DiscardTargetableElement(obj);
                return;
            }

            OverlayBox box;
            if (!targetable_boxes.TryGetValue(obj, out box))
            {
                box = Windowing.CreateOverlayBox();
                targetable_boxes[obj] = box;
                box.SetColor(224, 255, 255, 255);
            }

            box.Y = y;
            box.Width = width;
            box.Height = height;
            box.X = x;
            box.Show();
        }

        private void DiscardTargetableElement(UiDomObject obj)
        {
            if (targetable_boxes.TryGetValue(obj, out var box))
            {
                box.Dispose();
                targetable_boxes.Remove(obj);
            }
        }

        private void UpdateActions(UiDomObject obj)
        {
            var new_actions = new List<string>();

            foreach (var declaration in obj.Declarations)
            {
                string action = null;
                if (declaration.StartsWith("action"))
                {
                    if (obj.GetDeclaration(declaration) == UiDomUndefined.Instance)
                        continue;
                    if (declaration.StartsWith("action_on_"))
                    {
                        action = declaration.Substring(10);
                    }
                    else if (declaration.StartsWith("action_name_"))
                    {
                        action = declaration.Substring(12);
                    }
                }

                if (!string.IsNullOrEmpty(action) && !new_actions.Contains(action))
                {
                    new_actions.Add(action);
                }
            }

            List<string> old_actions;
            if (!object_actions.TryGetValue(obj, out old_actions))
            {
                if (new_actions.Count == 0)
                    return;
                old_actions = new List<string>();
            }

            foreach (var action in new_actions)
            {
                if (old_actions.Contains(action))
                {
                    old_actions.Remove(action);
                    continue;
                }
                List<UiDomObject> objects;
                if (!objects_defining_action.TryGetValue(action, out objects))
                {
                    objects = new List<UiDomObject>();
                    objects_defining_action[action] = objects;
                }
#if DEBUG
                Console.WriteLine($"action {action} defined on {obj}");
#endif
                objects.Add(obj);

                if (objects.Count == 1)
                {
                    InputSystem.Instance.WatchAction(action);
                }
            }
            object_actions[obj] = new_actions;

            foreach (var action in old_actions)
            {
#if DEBUG
                Console.WriteLine($"action {action} removed on {obj}");
#endif
                objects_defining_action[action].Remove(obj);
                if (objects_defining_action[action].Count == 0 && !locked_inputs.ContainsKey(action))
                {
                    InputSystem.Instance.UnwatchAction(action);
                }
            }
        }

        private void DiscardActions(UiDomObject obj)
        {
            if (object_actions.TryGetValue(obj, out var actions))
            {
                object_actions.Remove(obj);
                foreach (var action in actions)
                {
#if DEBUG
                    Console.WriteLine($"action {action} removed on {obj}");
#endif
                    objects_defining_action[action].Remove(obj);
                    if (objects_defining_action[action].Count == 0)
                    {
                        InputSystem.Instance.UnwatchAction(action);
                    }
                }
            }
        }
    }
}
