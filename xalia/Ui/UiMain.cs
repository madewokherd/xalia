using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
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

        class ActionInfo
        {
            public UiDomObject element;
            public string action;
            public UiDomRoutine routine;
            public string name;
            public int repeat_delay;
            public int repeat_interval;

            public ActionInfo(UiDomObject element, string action)
            {
                this.element = element;
                this.action = action;
            }

            public override bool Equals(object obj)
            {
                if (obj is ActionInfo ai)
                {
                    return element.Equals(ai.element) &&
                        action == ai.action &&
                        routine == ai.routine &&
                        name == ai.name &&
                        repeat_delay == ai.repeat_delay &&
                        repeat_interval == ai.repeat_interval;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return (element, action, routine, name, repeat_delay, repeat_interval).GetHashCode();
            }
        }

        private Dictionary<string, List<UiDomObject>> objects_defining_action = new Dictionary<string, List<UiDomObject>>();
        private Dictionary<UiDomObject, List<ActionInfo>> object_actions = new Dictionary<UiDomObject, List<ActionInfo>>();
        private Dictionary<string, UiDomRoutine> locked_inputs = new Dictionary<string, UiDomRoutine>();

        private Dictionary<string, CancellationTokenSource> repeat_timers = new Dictionary<string, CancellationTokenSource>();

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

        private bool GetActionInfo(string action, out ActionInfo info)
        {
            if (objects_defining_action.TryGetValue(action, out var objects) &&
                objects.Count != 0)
            {
                var obj = objects[objects.Count - 1]; // most recently added object for this action
                info = object_actions[obj].Find((ActionInfo ai) => action == ai.action);
                return true;
            }
            info = default;
            return false;
        }

        private void OnActionStateChangeEvent(object sender, InputSystem.ActionStateChangeEventArgs e)
        {
            UiDomRoutine routine;
#if DEBUG
            Console.WriteLine($"Got input: {e.Action} {e.PreviousState} => {e.State}");
#endif
            if (repeat_timers.ContainsKey(e.Action) && !e.State.Pressed)
            {
                repeat_timers[e.Action].Cancel();
                repeat_timers.Remove(e.Action);
            }
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

                if (GetActionInfo(e.Action, out var info))
                {

                    var obj = info.element;

                    routine = info.routine;

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

                    if (info.repeat_interval != 0 && e.JustPressed && !repeat_timers.ContainsKey(e.Action))
                    {
                        Utils.RunTask(DoRepeatTimer(e.Action,
                            info.repeat_delay != 0 ? info.repeat_delay : info.repeat_interval));
                    }
                }
            }
        }

        private async Task DoRepeatTimer(string action, int initial_delay)
        {
            var source = new CancellationTokenSource();
            repeat_timers.Add(action, source);
            var token = source.Token;

            InputState repeat_state = default;
            repeat_state.Kind = InputStateKind.Repeat;

            try
            {
                await Task.Delay(initial_delay, token);

                while (GetActionInfo(action, out var info))
                {
                    InputSystem.Instance.InjectInput(action, repeat_state);

                    if (info.repeat_interval == 0)
                        return;

                    await Task.Delay(info.repeat_interval, token);
                }
            }
            catch (TaskCanceledException) { }
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

        private UiDomObject _targetedElement;
        public UiDomObject TargetedElement
        {
            get
            {
                return _targetedElement;
            }
            set
            {
                if (_targetedElement != value)
                {
                    var previous = TargetedElement;
                    _targetedElement = value;

                    Root.PropertyChanged("targeted_element");
                    if (!(_targetedElement is null))
                        _targetedElement.PropertyChanged("targeted");
                    if (!(previous is null))
                        previous.PropertyChanged("targeted");
                    TargetChanged(previous);
                }
            }
        }

        private bool TryGetTargetBoundsDeclarations(UiDomObject obj, out (int, int, int, int) bounds)
        {
            if (obj.GetDeclaration("target_x") is UiDomInt xint &&
                obj.GetDeclaration("target_y") is UiDomInt yint &&
                obj.GetDeclaration("target_width") is UiDomInt wint &&
                obj.GetDeclaration("target_height") is UiDomInt hint)
            {
                bounds = (xint.Value, yint.Value, wint.Value, hint.Value);
                return true;
            }
            bounds = default;
            return false;
        }

        private void UpdateTargetableElement(UiDomObject obj)
        {
            if (!obj.GetDeclaration("targetable").ToBool())
            {
                DiscardTargetableElement(obj);
                return;
            }

            int x, y, width, height;

            if (TryGetTargetBoundsDeclarations(obj, out var target_bounds))
            {
                x = target_bounds.Item1;
                y = target_bounds.Item2;
                width = target_bounds.Item3;
                height = target_bounds.Item4;
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

            if (obj == TargetedElement)
            {
                target_box.SetBounds(x, y, width, height);
                // TODO: stop any ongoing animation
            }
        }

        private void DiscardTargetableElement(UiDomObject obj)
        {
            targetable_objects.Remove(obj);
            if (obj == TargetedElement)
                TargetedElement = null;
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

            if (targetable_objects.Count == 0 || !(TargetedElement is null))
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

            TargetedElement = best_element;
        }

        private void UpdateActions(UiDomObject obj)
        {
            var new_actions = new List<ActionInfo>();
            var new_action_ids = new List<string>();

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

                if (!string.IsNullOrEmpty(action) && !new_action_ids.Contains(action))
                {
                    new_action_ids.Add(action);
                }
            }

            foreach (var action in new_action_ids)
            {
                ActionInfo info = new ActionInfo(obj, action);
                if (obj.GetDeclaration("action_on_"+action) is UiDomRoutine routine)
                {
                    info.routine = routine;
                }
                if (obj.GetDeclaration("action_name_" + action) is UiDomString name)
                {
                    info.name = name.Value;
                }
                if (obj.GetDeclaration("action_repeat_delay_" + action) is UiDomInt rd)
                {
                    info.repeat_delay = rd.Value;
                }
                if (obj.GetDeclaration("action_repeat_interval_" + action) is UiDomInt ri)
                {
                    info.repeat_interval = ri.Value;
                }
                new_actions.Add(info);
            }

            List<ActionInfo> old_actions;
            if (!object_actions.TryGetValue(obj, out old_actions))
            {
                if (new_actions.Count == 0)
                    return;
                old_actions = new List<ActionInfo>();
            }

            foreach (var action in new_actions)
            {
                if (old_actions.Find((ActionInfo ai) => ai.action == action.action) is ActionInfo old_info)
                {
                    old_actions.Remove(old_info);
                    continue;
                }
                List<UiDomObject> objects;
                if (!objects_defining_action.TryGetValue(action.action, out objects))
                {
                    objects = new List<UiDomObject>();
                    objects_defining_action[action.action] = objects;
                }
#if DEBUG
                Console.WriteLine($"action {action.action} defined on {obj}");
#endif
                objects.Add(obj);

                if (objects.Count == 1)
                {
                    InputSystem.Instance.WatchAction(action.action);
                }
            }
            object_actions[obj] = new_actions;

            foreach (var action in old_actions)
            {
#if DEBUG
                Console.WriteLine($"action {action.action} removed on {obj}");
#endif
                objects_defining_action[action.action].Remove(obj);
                if (objects_defining_action[action.action].Count == 0 && !locked_inputs.ContainsKey(action.action))
                {
                    InputSystem.Instance.UnwatchAction(action.action);
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
                    Console.WriteLine($"action {action.action} removed on {obj}");
#endif
                    objects_defining_action[action.action].Remove(obj);
                    if (objects_defining_action[action.action].Count == 0)
                    {
                        InputSystem.Instance.UnwatchAction(action.action);
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
                case "target_move":
                    return new TargetMoveRoutine(this);
                case "targeted_element":
                    depends_on.Add((Root, new IdentifierExpression("targeted_element")));
                    if (TargetedElement is null)
                        return UiDomUndefined.Instance;
                    return TargetedElement;
                case "targeted":
                    depends_on.Add((element, new IdentifierExpression("targeted")));
                    return UiDomBoolean.FromBool(TargetedElement == element);
            }
            return null;
        }

        private bool TryGetElementTargetBounds(UiDomObject element, out (int, int, int, int) bounds)
        {
            if (targetable_objects.TryGetValue(element, out bounds))
                return true;

            return TryGetTargetBoundsDeclarations(element, out bounds);
        }

        internal enum Direction
        {
            Up,
            Down,
            Left,
            Right
        }

        private (int,int,int,int) TranslateBox((int,int,int,int) box, Direction direction)
        {
            // Translate to a coordinate space where box.Item1 increases with direction
            switch (direction)
            {
                case Direction.Up:
                    return (-(box.Item2 + box.Item4), box.Item1, box.Item4, box.Item3);
                case Direction.Down:
                    return (box.Item2, box.Item1, box.Item4, box.Item3);
                case Direction.Left:
                    return (-(box.Item3 + box.Item1), box.Item2, box.Item3, box.Item4);
                case Direction.Right:
                    return box;
            }
            throw new ArgumentException("invalid Direction value");
        }

        internal void TargetMove(Direction direction, double bias=0)
        {
            if (TargetedElement is null)
                return;

            (int, int, int, int) current_bounds;
            if (!TryGetElementTargetBounds(TargetedElement, out current_bounds))
            {
                return;
            }

            current_bounds = TranslateBox(current_bounds, direction);

            UiDomObject best_element = null;
            // These are actually distance squared so we can skip sqrt.
            long best_edge_distance = 0;
            long best_center_distance = 0;
            
            foreach (var kvp in targetable_objects)
            {
                var candidate_element = kvp.Key;
                var candidate_bounds = kvp.Value;
                
                if (candidate_element == TargetedElement)
                    continue;

                candidate_bounds = TranslateBox(candidate_bounds, direction);

                if (candidate_bounds.Item1 < current_bounds.Item1 + current_bounds.Item3)
                {
                    // candidate's left edge must be to the right of current target
                    continue;
                }

                // Calculate edge distance
                long dx = candidate_bounds.Item1 - (current_bounds.Item1 + current_bounds.Item3);
                long dy;

                int y_diff_start = candidate_bounds.Item2 - current_bounds.Item2;

                if (bias != 0)
                {
                    y_diff_start -= (int)(Math.Round(bias * dx));
                }

                int y_diff_end = y_diff_start + candidate_bounds.Item4;

                if (y_diff_end < 0)
                    dy = -y_diff_end;
                else if (y_diff_start > current_bounds.Item4)
                    dy = y_diff_start;
                else
                    dy = 0;
                if (dy != 0)
                {
                    // Use the far edge/corner rather than the near in this case
                    dx += candidate_bounds.Item3;
                    dy += candidate_bounds.Item4;
                }
                var candidate_edge_distance = (dx * dx) + (dy * dy) * 4;

                // Calculate centerpoint distance
                dy = y_diff_start - (int)Math.Round(candidate_bounds.Item4 * (bias + 1) / 2); // use center point of candidate neutral, bottom point if bias is fully upwards
                dy -= (int)Math.Round(current_bounds.Item4 * (bias + 1) / 2); // use center point of current if neutral, top point if bias is fully upwards
                var candidate_center_distance = (dx * dx) + (dy * dy) * 4;

                if (best_element is null ||
                    candidate_edge_distance < best_edge_distance ||
                    (candidate_edge_distance == best_edge_distance && candidate_center_distance < best_center_distance))
                {
                    best_element = candidate_element;
                    best_edge_distance = candidate_edge_distance;
                    best_center_distance = candidate_center_distance;
                    continue;
                }
            }

            if (best_element is null)
                return;

            TargetedElement = best_element;
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
            if (TargetedElement is null)
            {
                target_box.Hide();

                if (targetable_objects.Count != 0)
                    Utils.RunIdle(SelectAnyTarget());

                return;
            }

            (int, int, int, int) bounds;
            if (!TryGetElementTargetBounds(TargetedElement, out bounds))
            {
                Console.WriteLine($"WARNING: {TargetedElement} is targeted but it does not have target bounds");
                target_box.Hide();
                return;
            }

            // TODO: Animate this if previous_target is not null
            target_box.SetBounds(bounds.Item1, bounds.Item2, bounds.Item3, bounds.Item4);
            target_box.Show();
        }
    }
}
