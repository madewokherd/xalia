﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xalia.Gudl;
using Xalia.Input;
using Xalia.Sdl;
using Xalia.UiDom;

namespace Xalia.Ui
{
    internal class UiMain : IUiDomApplication
    {
        public UiDomRoot Root { get; private set; }
        public WindowingSystem Windowing { get; }

        private OverlayBox target_box;
        private Dictionary<UiDomElement, (int, int, int, int)> targetable_elements = new Dictionary<UiDomElement, (int, int, int, int)>();

        private OverlayBox current_view_box;

        class ActionInfo
        {
            public string action;
            public UiDomRoutine routine;
            public string name;
            public InputQueue queue;

            public ActionInfo(string action)
            {
                this.action = action;
            }

            public override bool Equals(object obj)
            {
                if (obj is ActionInfo ai)
                {
                    return action == ai.action &&
                        routine.Equals(ai.routine) &&
                        name == ai.name;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return (action, routine, name).GetHashCode();
            }
        }

        private Dictionary<string, ActionInfo> defined_actions = new Dictionary<string, ActionInfo>();

        private uint target_sequence_counter;
        private Dictionary<UiDomElement, uint> element_target_sequence = new Dictionary<UiDomElement, uint>();

        private int target_move_bound; // Number of active bindings to a target_move routine

        private int current_view_bound; // Number of active bindings to a current_view routine

        public UiMain()
        {
            Windowing = WindowingSystem.Instance;

            target_box = Windowing.CreateOverlayBox();
            target_box.SetColor(224, 255, 255, 255);

            current_view_box = Windowing.CreateOverlayBox();
            current_view_box.SetColor(224, 255, 255, 255);
            current_view_box.Thickness = 7;
            
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

        static bool DebugInput = !(Environment.GetEnvironmentVariable("XALIA_DEBUG_INPUT") is null &&
            Environment.GetEnvironmentVariable("XALIA_DEBUG_INPUT") != "0");

        private void OnActionStateChangeEvent(object sender, InputSystem.ActionStateChangeEventArgs e)
        {
            if (DebugInput)
                Utils.DebugWriteLine($"Got input: {e.Action} {e.State}");
            if (defined_actions.TryGetValue(e.Action, out var info))
            {
                if (info.queue is null)
                {
                    if (e.State.Kind is InputStateKind.Disconnected)
                        return;

                    if (DebugInput)
                        Utils.DebugWriteLine($"Passing input to routine: {info.routine}");
                    info.queue = new InputQueue();
                    info.queue.Enqueue(e.State);

                    Utils.RunTask(info.routine.ProcessInputQueue(info.queue));
                }
                else
                {
                    if (DebugInput)
                        Utils.DebugWriteLine($"Passing input to routine: {info.routine}");
                    info.queue.Enqueue(e.State);
                    if (e.State.Kind is InputStateKind.Disconnected)
                    {
                        info.queue = null;
                    }
                }
            }
        }

        public void ElementDied(UiDomElement e)
        {
            DiscardTargetableElement(e);
            element_target_sequence.Remove(e);
        }

        public void ElementDeclarationsChanged(UiDomElement e)
        {
            UpdateTargetableElement(e);
            if (e.Equals(Root))
            {
                UpdateActions();

                CurrentView = e.GetDeclaration("current_view") as UiDomElement;
            }

            if (e.Equals(CurrentView))
            {
                UpdateCurrentViewBox();
            }
        }

        private void UpdateCurrentViewBox()
        {
            if (!(CurrentView is null) && current_view_bound != 0 &&
                (TryGetBoundsDeclarations(CurrentView, "scroll_pane", out var bounds) ||
                 TryGetBoundsDeclarations(CurrentView, "target", out bounds)))
            {
                current_view_box.SetBounds(bounds.Item1, bounds.Item2, bounds.Item3, bounds.Item4);
                current_view_box.Show();
            }
            else
                current_view_box.Hide();
        }

        internal void CurrentViewRoutineStarted()
        {
            current_view_bound++;
            if (current_view_bound == 1)
                UpdateCurrentViewBox();
        }

        internal void CurrentViewRoutineStopped()
        {
            current_view_bound--;
            if (current_view_bound == 0)
                current_view_box.Hide();
        }

        private UiDomElement _targetedElement;
        public UiDomElement TargetedElement
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

                    if (!(value is null))
                    {
                        element_target_sequence[value] = target_sequence_counter;
                        target_sequence_counter++;
                    }

                    if (Root.MatchesDebugCondition() ||
                        (!(previous is null) && previous.MatchesDebugCondition()) ||
                        (!(_targetedElement is null) && _targetedElement.MatchesDebugCondition()))
                        Utils.DebugWriteLine($"targeted_element: {_targetedElement}");

                    Root.PropertyChanged("targeted_element");
                    if (!(_targetedElement is null))
                        _targetedElement.PropertyChanged("targeted");
                    if (!(previous is null))
                        previous.PropertyChanged("targeted");
                    TargetChanged();
                }
            }
        }

        private bool TryGetBoundsDeclarations(UiDomElement element, string prefix, out (int, int, int, int) bounds)
        {
            if (element.GetDeclaration($"{prefix}_x").TryToInt(out int xint) &&
                element.GetDeclaration($"{prefix}_y").TryToInt(out int yint) &&
                element.GetDeclaration($"{prefix}_width").TryToInt(out int wint) &&
                element.GetDeclaration($"{prefix}_height").TryToInt(out int hint))
            {
                bounds = (xint, yint,
                    wint > 0 ? wint : 1, hint > 0 ? hint : 1);
                return true;
            }
            bounds = default;
            return false;
        }

        private bool TryGetTargetBoundsDeclarations(UiDomElement element, out (int, int, int, int) bounds)
        {
            if (TryGetBoundsDeclarations(element, "target", out bounds))
            {
                // Make sure this is in range of all ancestor views

                var parent = element.Parent;

                while (!(parent is null))
                {
                    if (TryGetBoundsDeclarations(parent, "scroll_view", out var ancestor_bounds))
                    {
                        if (!BoundsIntersect(bounds, ancestor_bounds))
                            return false;
                    }

                    parent = parent.Parent;
                }
                return true;
            }
            return false;
        }

        private static bool BoundsIntersect((int, int, int, int) bounds, (int, int, int, int) ancestor_bounds)
        {
            if (ancestor_bounds.Item1 + ancestor_bounds.Item3 < bounds.Item1)
                return false;
            if (ancestor_bounds.Item2 > bounds.Item2 + bounds.Item4)
                return false;
            if (ancestor_bounds.Item2 + ancestor_bounds.Item4 < bounds.Item2)
                return false;
            if (ancestor_bounds.Item1 > bounds.Item1 + bounds.Item3)
                return false;
            return true;
        }

        private void UpdateTargetableElement(UiDomElement element)
        {
            if (!element.GetDeclaration("targetable").ToBool())
            {
                DiscardTargetableElement(element);
                return;
            }

            int x, y, width, height;

            if (TryGetTargetBoundsDeclarations(element, out var target_bounds))
            {
                x = target_bounds.Item1;
                y = target_bounds.Item2;
                width = target_bounds.Item3;
                height = target_bounds.Item4;
            }
            else
            {
                DiscardTargetableElement(element);
                return;
            }

            targetable_elements[element] = (x, y, width, height);

            if (targetable_elements.Count == 1)
            {
                Utils.RunTask(SelectAnyTarget());
            }

            if (element == TargetedElement)
            {
                target_box.SetBounds(x, y, width, height);
                // TODO: stop any ongoing animation
            }
        }

        private void DiscardTargetableElement(UiDomElement element)
        {
            targetable_elements.Remove(element);
            if (element == TargetedElement)
                TargetedElement = null;
        }

        private Stack<UiDomElement> GetAncestors(UiDomElement element)
        {
            var result = new Stack<UiDomElement>();
            while (!(element is null))
            {
                result.Push(element);
                if (element is UiDomRoot)
                    return result;
                element = element.Parent;
            }

            return null;
        }

        private async Task SelectAnyTarget()
        {
            // This gets called when the first targetable element is found.
            // Give it some time to discover other elements so we can choose the best one.
            await Task.Delay(200);

            if (targetable_elements.Count == 0 || !(TargetedElement is null))
                return;

            UiDomElement best_element = null;
            foreach (var candidate_element in targetable_elements.Keys)
            {
                Stack<UiDomElement> candidate_ancestors = GetAncestors(candidate_element);

                if (candidate_ancestors is null)
                    continue;

                if (best_element is null)
                {
                    best_element = candidate_element;
                    continue;
                }

                // Choose the most recently-targeted element
                if (element_target_sequence.TryGetValue(candidate_element, out var candidate_seq))
                {
                    if (element_target_sequence.TryGetValue(best_element, out var best_seq))
                    {
                        if (candidate_seq > best_seq)
                        {
                            best_element = candidate_element;
                        }
                        continue;
                    }
                    else
                    {
                        best_element = candidate_element;
                        continue;
                    }
                }
                else if (element_target_sequence.ContainsKey(best_element))
                    continue;

                // Look for default or focused elements
                double best_priority, candidate_priority;

                if (!best_element.GetDeclaration("target_priority").TryToDouble(out best_priority))
                    best_priority = 0;

                if (!candidate_element.GetDeclaration("target_priority").TryToDouble(out candidate_priority))
                    candidate_priority = 0;

                if (candidate_priority > best_priority)
                {
                    best_element = candidate_element;
                    continue;
                }
                if (best_priority > candidate_priority)
                    continue;

                // Choose the first element in the tree, prefer children to ancestors
                Stack<UiDomElement> best_ancestors = GetAncestors(best_element);

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

        private Dictionary<UiDomElement, (int,int)> BuildPositionMap()
        {
            // returns a dictionary mapping elements to the upper-left corner of the
            // bounding box of their targetable descendents

            var result = new Dictionary<UiDomElement, (int, int)>();

            foreach (var element in targetable_elements.Keys)
            {
                if (TryGetElementTargetBounds(element, out var bounds))
                {
                    var parent = element;
                    while (!(parent is null))
                    {
                        if (result.TryGetValue(parent, out var prev_ul))
                        {
                            if (prev_ul.Item1 <= bounds.Item1 && prev_ul.Item2 <= bounds.Item2)
                                break;
                            result[parent] = (Math.Min(prev_ul.Item1, bounds.Item1), Math.Min(prev_ul.Item2, bounds.Item2));
                        }
                        else
                            result[parent] = (bounds.Item1, bounds.Item2);
                        parent = parent.Parent;
                    }
                }
            }

            return result;
        }

        private int TargetTraverseOrder(UiDomElement a, UiDomElement b, Dictionary<UiDomElement, (int,int)> position_map)
        {
            var a_ancestors = GetAncestors(a);
            var b_ancestors = GetAncestors(b);

            if (a_ancestors.Contains(b))
                return 1;

            if (b_ancestors.Contains(a))
                return -1;

            while (a_ancestors.Count != 0 && b_ancestors.Count != 0)
            {
                var a_parent = a_ancestors.Pop();
                var b_parent = b_ancestors.Pop();

                if (a_parent == b_parent)
                {
                    continue;
                }

                if (a_parent.Parent is null || b_parent.Parent is null)
                    return 0;

                if (!position_map.TryGetValue(a_parent, out var a_parent_pos))
                    return 0;

                if (!position_map.TryGetValue(b_parent, out var b_parent_pos))
                    return 0;

                if (a_parent_pos.Item2 > b_parent_pos.Item2)
                    return 1;
                if (b_parent_pos.Item2 > a_parent_pos.Item2)
                    return -1;

                if (a_parent_pos.Item1 > b_parent_pos.Item1)
                    return 1;
                if (b_parent_pos.Item1 > a_parent_pos.Item1)
                    return -1;

                return (a_parent.Parent.Children.IndexOf(a_parent) > b_parent.Parent.Children.IndexOf(b_parent)) ?
                    1 : -1;
            }

            return 0;
        }

        private void TargetTraverse(bool forward)
        {
            UiDomElement best_candidate = null;
            bool best_candidate_after_current = false;
            int mult = forward ? 1 : -1;

            if (TargetedElement is null)
                return;

            var position_map = BuildPositionMap();

            foreach (var candidate_element in targetable_elements.Keys)
            {
                if (candidate_element == TargetedElement)
                    continue;

                var targeted_cmp = mult * TargetTraverseOrder(TargetedElement, candidate_element, position_map);
                if (targeted_cmp == 0)
                    continue;

                if (best_candidate is null)
                {
                    best_candidate = candidate_element;
                    best_candidate_after_current = targeted_cmp < 0;
                    continue;
                }

                if (best_candidate_after_current && targeted_cmp > 0)
                {
                    // current candidate is before target in the order, best candidate is after
                    continue;
                }
                if (!best_candidate_after_current && targeted_cmp < 0)
                {
                    // current candidate is after target in the order, best candidate is before
                    best_candidate = candidate_element;
                    best_candidate_after_current = targeted_cmp < 0;
                    continue;
                }

                var candidate_cmp = mult * TargetTraverseOrder(best_candidate, candidate_element, position_map);
                if (candidate_cmp > 0)
                {
                    // current candidate is sooner in the order, therefore closer to target
                    best_candidate = candidate_element;
                    best_candidate_after_current = targeted_cmp < 0;
                    continue;
                }
            }

            if (!(best_candidate is null))
            {
                TargetedElement = best_candidate;
            }
        }

        private void TargetNext(UiDomRoutineSync obj)
        {
            TargetTraverse(true);
        }

        private void TargetPrevious(UiDomRoutineSync obj)
        {
            TargetTraverse(false);
        }

        private void UpdateActions()
        {
            var new_actions = new Dictionary<string, ActionInfo>();
            var new_action_ids = new List<string>();

            foreach (var declaration in Root.Declarations)
            {
                if (!declaration.StartsWith("action_on_"))
                    continue;
                if (!(Root.GetDeclaration(declaration) is UiDomRoutine))
                    continue;
                string action = declaration.Substring(10);

                if (!string.IsNullOrEmpty(action) && !new_action_ids.Contains(action))
                {
                    new_action_ids.Add(action);
                }
            }

            foreach (var action in new_action_ids)
            {
                ActionInfo info = new ActionInfo(action);
                info.routine = (UiDomRoutine)Root.GetDeclaration("action_on_"+action);
                if (Root.GetDeclaration("action_name_" + action) is UiDomString name)
                {
                    info.name = name.Value;
                }
                new_actions[action] = info;
            }

            var old_actions = defined_actions;

            foreach (var info in new List<ActionInfo>(new_actions.Values))
            {
                if (old_actions.TryGetValue(info.action, out var old_info) && old_info.Equals(info))
                {
                    new_actions[info.action] = old_info;
                    old_actions.Remove(info.action);
                    continue;
                }
                if (DebugInput)
                    Utils.DebugWriteLine($"action {info.action} defined as {info.routine}");
            }
            defined_actions = new_actions;

            foreach (var info in old_actions.Values)
            {
                if (DebugInput)
                    Utils.DebugWriteLine($"action {info.action} is no longer {info.routine}");
                if (!(info.queue is null))
                    info.queue.Enqueue(new InputState(InputStateKind.Disconnected));
                if (!new_actions.ContainsKey(info.action))
                    InputSystem.Instance.UnwatchAction(info.action);
            }
            foreach (var info in new_actions.Values)
            {
                if (!old_actions.ContainsKey(info.action))
                    InputSystem.Instance.WatchAction(info.action);
                if (info.queue is null)
                {
                    var current_state = InputSystem.Instance.PollAction(info.action);
                    if (current_state.Kind != InputStateKind.Disconnected)
                    {
                        info.queue = new InputQueue();
                        info.queue.Enqueue(current_state);

                        Utils.RunTask(info.routine.ProcessInputQueue(info.queue));
                    }
                }
            }
        }

        public UiDomValue EvaluateIdentifierHook(UiDomElement element, string id, [In, Out] HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            switch (id)
            {
                case "target_move_up":
                    return new TargetMoveButtonRoutine(this, "target_move_up", TargetMoveUp);
                case "target_move_down":
                    return new TargetMoveButtonRoutine(this, "target_move_down", TargetMoveDown);
                case "target_move_left":
                    return new TargetMoveButtonRoutine(this, "target_move_left", TargetMoveLeft);
                case "target_move_right":
                    return new TargetMoveButtonRoutine(this, "target_move_right", TargetMoveRight);
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
                case "show_keyboard":
                    if (Windowing.CanShowKeyboard())
                    {
                        return new UiDomRoutineAsync(null, "show_keyboard", ShowKeyboard);
                    }
                    break;
                case "send_key":
                    if (Windowing.CanSendKeys)
                    {
                        return new SendKey(Windowing);
                    }
                    break;
                case "send_click":
                    return new UiDomRoutineAsync(element, "send_click", SendClick);
                case "send_right_click":
                    return new UiDomRoutineAsync(element, "send_right_click", SendRightClick);
                case "send_scroll":
                    return new SendScroll(element, Windowing);
                case "target_next":
                    return new UiDomRoutineSync("target_next", TargetNext);
                case "target_previous":
                    return new UiDomRoutineSync("target_previous", TargetPrevious);
                case "wrap_current_view_action":
                    return new UiDomMethod("wrap_current_view_action", WrapCurrentViewMethod);
            }
            return null;
        }

        private UiDomValue WrapCurrentViewMethod(UiDomMethod method, UiDomValue context, GudlExpression[] arglist, UiDomRoot root, HashSet<(UiDomElement, GudlExpression)> depends_on)
        {
            if (arglist.Length < 1)
                return UiDomUndefined.Instance;

            UiDomRoutine routine;

            routine = context.Evaluate(arglist[0], root, depends_on) as UiDomRoutine;

            if (routine is null)
                return UiDomUndefined.Instance;

            return new CurrentViewRoutine(this, routine);
        }

        public void DumpElementProperties(UiDomElement element)
        {
            if (element is UiDomRoot)
                Utils.DebugWriteLine($"  targeted_element: {TargetedElement}");
            if (TargetedElement == element)
                Utils.DebugWriteLine("  targeted: true");
        }

        private async Task SendClick(UiDomRoutineAsync obj, WindowingSystem.MouseButton button)
        {
            var position = await obj.Element.GetClickablePoint();

            if (!position.Item1)
            {
                Utils.DebugWriteLine($"WARNING: Could not get clickable point for {obj.Element}");
                return;
            }

            try
            {
                await Windowing.SendMouseMotion(position.Item2, position.Item3);

                await Windowing.SendClick(button);
            }
            catch (NotImplementedException)
            {
                Utils.DebugWriteLine($"WARNING: Cannot send click events on the current windowing system");
            }
        }

        private Task SendClick(UiDomRoutineAsync obj)
        {
            return SendClick(obj, WindowingSystem.MouseButton.LeftButton);
        }

        private Task SendRightClick(UiDomRoutineAsync obj)
        {
            return SendClick(obj, WindowingSystem.MouseButton.RightButton);
        }

        private async Task ShowKeyboard(UiDomRoutineAsync obj)
        {
            await Windowing.ShowKeyboardAsync();
        }

        private bool TryGetElementTargetBounds(UiDomElement element, out (int, int, int, int) bounds)
        {
            if (targetable_elements.TryGetValue(element, out bounds))
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

            UiDomElement best_element = null;
            // These are actually distance squared so we can skip sqrt.
            long best_edge_distance = 0;
            long best_center_distance = 0;
            
            foreach (var kvp in targetable_elements)
            {
                var candidate_element = kvp.Key;
                var candidate_bounds = kvp.Value;
                
                if (candidate_element == TargetedElement)
                    continue;

                candidate_bounds = TranslateBox(candidate_bounds, direction);

                if (candidate_bounds.Item1 + candidate_bounds.Item3 > current_bounds.Item1 &&
                    current_bounds.Item1 + current_bounds.Item3 > candidate_bounds.Item1 &&
                    candidate_bounds.Item2 + candidate_bounds.Item4 > current_bounds.Item2 &&
                    current_bounds.Item2 + current_bounds.Item4 > candidate_bounds.Item2)
                {
                    // candidate intersects current target
                    int current_center_x = current_bounds.Item1 + current_bounds.Item3 / 2;
                    int current_center_y = current_bounds.Item2 + current_bounds.Item4 / 2;
                    int candidate_center_x = candidate_bounds.Item1 + candidate_bounds.Item3 / 2;
                    int candidate_center_y = candidate_bounds.Item2 + candidate_bounds.Item4 / 2;

                    int center_dx = candidate_center_x - current_center_x;
                    int center_dy = candidate_center_y - current_center_y;

                    if (center_dx <= 0)
                    {
                        // candidate center is not to the right of target
                        continue;
                    }

                    if (center_dx < Math.Abs(center_dy))
                    {
                        // candidate is more up/down than right.
                        continue;
                    }

                    /* This value of edge_distance will be negative. This is intentional. */
                    int edge_distance = candidate_bounds.Item1 - (current_bounds.Item1 + current_bounds.Item3);
                    int center_distance = center_dx * center_dx + center_dy * center_dy;

                    if (best_element is null ||
                        edge_distance < best_edge_distance ||
                        (edge_distance == best_edge_distance && center_distance < best_center_distance))
                    {
                        best_element = candidate_element;
                        best_edge_distance = edge_distance;
                        best_center_distance = center_distance;
                    }
                    continue;
                }

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

                // Calculate centerpoint distance, with bias
                var candidate_biased_y = candidate_bounds.Item2 + (int)Math.Round(candidate_bounds.Item4 * (1 - bias) / 2);
                candidate_biased_y -= (int)Math.Round(bias * dx);
                var current_biased_y = current_bounds.Item2 + (int)Math.Round(current_bounds.Item4 * (1 + bias) / 2);
                dy = candidate_biased_y - current_biased_y;
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
            {
                if (ScrollAncestor(TargetedElement, direction))
                    return;

                AdjustValue(TargetedElement, direction);

                return;
            }

            TargetedElement = best_element;

            ScrollIntoView(TargetedElement);
        }

        private bool AdjustValue(UiDomElement targetedElement, Direction direction)
        {
            string routine_name;

            switch (direction)
            {
                case Direction.Left:
                    routine_name = "adjust_value_left_action";
                    break;
                case Direction.Down:
                    routine_name = "adjust_value_down_action";
                    break;
                case Direction.Up:
                    routine_name = "adjust_value_up_action";
                    break;
                case Direction.Right:
                    routine_name = "adjust_value_right_action";
                    break;
                default:
                    return false;
            }

            var routine = targetedElement.GetDeclaration(routine_name) as UiDomRoutine;

            if (!(routine is null))
            {
                routine.Pulse();
                return true;
            }

            return false;
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

        public void TargetChanged()
        {
            if (TargetedElement is null)
            {
                target_box.Hide();

                if (targetable_elements.Count != 0)
                    Utils.RunTask(SelectAnyTarget());

                return;
            }

            (int, int, int, int) bounds;
            if (!TryGetElementTargetBounds(TargetedElement, out bounds))
            {
                Utils.DebugWriteLine($"WARNING: {TargetedElement} is targeted but it does not have target bounds");
                target_box.Hide();
                return;
            }

            target_box.SetBounds(bounds.Item1, bounds.Item2, bounds.Item3, bounds.Item4);

            if (target_move_bound != 0)
                target_box.Show();
        }

        internal void TargetMoveRoutineStarted()
        {
            // Called when a TargetMoveRoutine starts getting inputs.
            target_move_bound++;
            if (target_move_bound == 1 && !(TargetedElement is null))
                target_box.Show();
        }

        internal void TargetMoveRoutineStopped()
        {
            // Called when a TargetMoveRoutine stops getting inputs.
            target_move_bound--;
            if (target_move_bound == 0)
                target_box.Hide();
        }

        private void ScrollIntoView(UiDomElement targetedElement)
        {
            if (!TryGetElementTargetBounds(targetedElement, out var bounds))
                return;

            UiDomElement ancestor = targetedElement.Parent;

            while (!(ancestor is null))
            {
                if (ancestor.GetDeclaration("scroll_target_margin").TryToInt(out int margin) &&
                    ancestor.GetDeclaration("scroll_view_action") is UiDomRoutine routine &&
                    TryGetBoundsDeclarations(ancestor, "scroll_view", out var scroll_view_bounds) &&
                    targetedElement != ancestor.GetDeclaration("hscroll") &&
                    targetedElement != ancestor.GetDeclaration("vscroll"))
                {
                    var padded_bounds = (
                        bounds.Item1 - margin,
                        bounds.Item2 - margin,
                        bounds.Item3 + margin * 2,
                        bounds.Item4 + margin * 2);
                    int xofs, yofs;

                    if (padded_bounds.Item3 > scroll_view_bounds.Item3)
                        // target bounds do not fully fit in view, not sure how to handle this.
                        xofs = 0;
                    else if (padded_bounds.Item1 + padded_bounds.Item3 > scroll_view_bounds.Item1 + scroll_view_bounds.Item3)
                        xofs = (padded_bounds.Item1 + padded_bounds.Item3) -
                            (scroll_view_bounds.Item1 + scroll_view_bounds.Item3);
                    else if (padded_bounds.Item1 < scroll_view_bounds.Item1)
                        xofs = padded_bounds.Item1 - scroll_view_bounds.Item1;
                    else
                        xofs = 0;

                    if (padded_bounds.Item4 > scroll_view_bounds.Item4)
                        // target bounds do not fully fit in view, not sure how to handle this.
                        yofs = 0;
                    else if (padded_bounds.Item2 + padded_bounds.Item4 > scroll_view_bounds.Item2 + scroll_view_bounds.Item4)
                        yofs = (padded_bounds.Item2 + padded_bounds.Item4) -
                            (scroll_view_bounds.Item2 + scroll_view_bounds.Item4);
                    else if (padded_bounds.Item2 < scroll_view_bounds.Item2)
                        yofs = padded_bounds.Item2 - scroll_view_bounds.Item2;
                    else
                        yofs = 0;

                    if (xofs != 0 || yofs != 0)
                    {
                        InputState st = new InputState(InputStateKind.PixelDelta);
                        st.XAxis = (short)xofs;
                        st.YAxis = (short)yofs;

                        InputQueue queue = new InputQueue();
                        queue.Enqueue(st);
                        queue.Enqueue(new InputState(InputStateKind.Disconnected));

                        Utils.RunTask(routine.ProcessInputQueue(queue));
                    }

                    break;
                }

                ancestor = ancestor.Parent;
            }
        }

        private bool ScrollAncestor(UiDomElement targetedElement, Direction direction)
        {
            UiDomElement ancestor = targetedElement.Parent;

            while (!(ancestor is null))
            {
                if (ancestor.GetDeclaration("scroll_view_action") is UiDomRoutine routine &&
                    TryGetBoundsDeclarations(ancestor, "scroll_view", out var scroll_view_bounds))
                {
                    int xofs=0, yofs=0;

                    switch (direction)
                    { // FIXME: Arbitrarily scrolling by 1/5 of the view
                        case Direction.Left:
                            xofs = -(scroll_view_bounds.Item3 / 5);
                            break;
                        case Direction.Down:
                            yofs = scroll_view_bounds.Item4 / 5;
                            break;
                        case Direction.Up:
                            yofs = -(scroll_view_bounds.Item4 / 5);
                            break;
                        case Direction.Right:
                            xofs = scroll_view_bounds.Item3 / 5;
                            break;
                    }

                    InputState st = new InputState(InputStateKind.PixelDelta);
                    st.XAxis = (short)xofs;
                    st.YAxis = (short)yofs;

                    InputQueue queue = new InputQueue();
                    queue.Enqueue(st);
                    queue.Enqueue(new InputState(InputStateKind.Disconnected));

                    Utils.RunTask(routine.ProcessInputQueue(queue));

                    return true;
                }

                ancestor = ancestor.Parent;
            }
            return false;
        }

        private UiDomElement _currentView;
        private UiDomElement CurrentView
        {
            get
            {
                return _currentView;
            }
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;

                    UpdateCurrentViewBox();
                }
            }
        }
    }
}
