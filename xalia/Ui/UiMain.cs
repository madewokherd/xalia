using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xalia.Gudl;
using Xalia.Input;
using Xalia.UiDom;
using Xalia.Sdl;
using System.Runtime.InteropServices;

namespace Xalia.Ui
{
    internal class UiMain : IUiDomApplication
    {
        public UiDomRoot Root { get; private set; }
        public WindowingSystem Windowing { get; }

        private OverlayBox target_box;
        private Dictionary<UiDomObject, (int, int, int, int)> targetable_objects = new Dictionary<UiDomObject, (int, int, int, int)>();

        private Dictionary<string, List<UiDomObject>> objects_defining_action = new Dictionary<string, List<UiDomObject>>();
        private Dictionary<UiDomObject, List<string>> object_actions = new Dictionary<UiDomObject, List<string>>();
        private Dictionary<string, UiDomRoutine> locked_inputs = new Dictionary<string, UiDomRoutine>();

        public UiMain()
        {
            Windowing = new WindowingSystem();

            target_box = Windowing.CreateOverlayBox();
            target_box.SetColor(224, 255, 255, 255);
            
            InputSystem.Instance.ActionStateChangeEvent += OnActionStateChangeEvent;
        }

        public void RootElementCreated(UiDomRoot root)
        {
            if (!(Root is null))
            {
                throw new InvalidOperationException("RootElementCreated must only be called once.");
            }
            Root = root;
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

        public void ElementDied(UiDomObject e)
        {
            DiscardTargetableElement(e);
            DiscardActions(e);
        }

        public void ElementDeclarationsChanged(UiDomObject e)
        {
            UpdateTargetableElement(e);
            UpdateActions(e);
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

            targetable_objects[obj] = (x, y, width, height);

            if (targetable_objects.Count == 1)
            {
                Utils.RunIdle(SelectAnyTarget());
            }

            if (obj == Root.TargetedElement)
            {
                target_box.SetBounds(x, y, width, height);
                // TODO: stop any ongoing animation
            }
        }

        private void DiscardTargetableElement(UiDomObject obj)
        {
            targetable_objects.Remove(obj);
            if (obj == Root.TargetedElement)
                Root.TargetedElement = null;
        }

        private Stack<UiDomObject> GetAncestors(UiDomObject obj)
        {
            var result = new Stack<UiDomObject>();
            while (!(obj is null))
            {
                result.Push(obj);
                obj = obj.Parent;
            }

            return result;
        }

        private async Task SelectAnyTarget()
        {
            // This gets called when the first targetable element is found.
            // Give it some time to discover other elements so we can choose the best one.
            await Task.Delay(200);

            if (targetable_objects.Count == 0 || !(Root.TargetedElement is null))
                return;

            UiDomObject best_element = null;
            foreach (var candidate_element in targetable_objects.Keys)
            {
                if (best_element is null)
                {
                    best_element = candidate_element;
                    continue;
                }
                // FIXME: look for default or focused elements?

                // Choose the first element in the tree, prefer children to ancestors
                Stack<UiDomObject> best_ancestors = GetAncestors(best_element);
                Stack<UiDomObject> candidate_ancestors = GetAncestors(candidate_element);

                while (true)
                {
                    if (best_ancestors.Count == 0)
                    {
                        best_element = candidate_element;
                        break;
                    }
                    if (candidate_ancestors.Count == 0)
                    {
                        break;
                    }

                    var best_parent = best_ancestors.Pop();
                    var candidate_parent = candidate_ancestors.Pop();

                    if (best_parent == candidate_parent)
                        continue;

                    if (best_parent.Parent.Children.IndexOf(best_parent) >
                        candidate_parent.Parent.Children.IndexOf(candidate_parent))
                        best_element = candidate_element;

                    break;
                }
            }

            Root.TargetedElement = best_element;
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

        public UiDomValue EvaluateIdentifierHook(UiDomObject element, string id, [In, Out] HashSet<(UiDomObject, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "target_move_up":
                    return new UiDomRoutineSync(null, "target_move_up", TargetMoveUp);
                case "target_move_down":
                    return new UiDomRoutineSync(null, "target_move_down", TargetMoveDown);
                case "target_move_left":
                    return new UiDomRoutineSync(null, "target_move_left", TargetMoveLeft);
                case "target_move_right":
                    return new UiDomRoutineSync(null, "target_move_right", TargetMoveRight);
            }
            return null;
        }

        private enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }

        private void TargetMove(Direction direction)
        {
            // TODO
        }

        private void TargetMoveUp(UiDomRoutineSync obj)
        {
            TargetMove(Direction.Up);
        }

        private void TargetMoveDown(UiDomRoutineSync obj)
        {
            TargetMove(Direction.Down);
        }

        private void TargetMoveLeft(UiDomRoutineSync obj)
        {
            TargetMove(Direction.Left);
        }

        private void TargetMoveRight(UiDomRoutineSync obj)
        {
            TargetMove(Direction.Right);
        }

        public void TargetChanged(UiDomObject previous_target)
        {
            if (Root.TargetedElement is null)
            {
                target_box.Hide();

                if (targetable_objects.Count != 0)
                    Utils.RunIdle(SelectAnyTarget());

                return;
            }

            (int, int, int, int) bounds;
            if (!targetable_objects.TryGetValue(Root.TargetedElement, out bounds))
            {
                Console.WriteLine($"WARNING: {Root.TargetedElement} is targeted but it does not have targetable:true");
                target_box.Hide();
                // FIXME: Look for target_x etc and show box anyway - we may need this for navigating into and
                // out of scrollable elements with interactable descendents
            }

            // TODO: Animate this if previous_target is not null
            target_box.SetBounds(bounds.Item1, bounds.Item2, bounds.Item3, bounds.Item4);
            target_box.Show();
        }
    }
}
