using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xalia.Gudl;

namespace Xalia.UiDom
{
    public class ExpressionWatcher : IDisposable
    {
        public ExpressionWatcher(UiDomValue context, UiDomRoot root, GudlExpression expression)
        {
            Context = context;
            Root = root;
            Expression = expression;

            UpdateCurrentValue();
        }

        public UiDomValue Context { get; }
        public UiDomRoot Root { get; }
        public GudlExpression Expression { get; }

        public UiDomValue CurrentValue { get; private set; } = UiDomUndefined.Instance;

        private Dictionary<(UiDomElement, GudlExpression), IDisposable> notifiers = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();
        private bool disposedValue;

        private TaskCompletionSource<bool> changed_task;

        event EventHandler ValueChanged;

        private void UpdateCurrentValue()
        {
            var depends_on = new HashSet<(UiDomElement, GudlExpression)>();
            var value = Context.Evaluate(Expression, Root, depends_on);

            var updated_dependency_notifiers = new Dictionary<(UiDomElement, GudlExpression), IDisposable>();
            foreach (var dep in depends_on)
            {
                if (notifiers.TryGetValue(dep, out var notifier))
                {
                    updated_dependency_notifiers[dep] = notifier;
                    notifiers.Remove(dep);
                }
                else
                {
                    updated_dependency_notifiers.Add(dep,
                        dep.Item1.NotifyPropertyChanged(dep.Item2, OnDependencyChanged));
                }
            }
            foreach (var notifier in notifiers.Values)
            {
                notifier.Dispose();
            }
            notifiers = updated_dependency_notifiers;

            if (!CurrentValue.Equals(value))
            {
                CurrentValue = value;
                if (ValueChanged != null)
                    ValueChanged(this, new EventArgs());
                if (changed_task != null)
                {
                    var task = changed_task;
                    changed_task = null;
                    task.SetResult(false);
                }
            }
        }

        private void OnDependencyChanged(UiDomElement element, GudlExpression property)
        {
            UpdateCurrentValue();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var notifier in notifiers.Values)
                    {
                        notifier.Dispose();
                    }
                    notifiers.Clear();
                    if (!(changed_task is null))
                        changed_task.SetCanceled();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }

        public Task WaitChanged()
        {
            if (changed_task is null)
            {
                changed_task = new TaskCompletionSource<bool>();
            }
            return changed_task.Task;
        }
    }
}
