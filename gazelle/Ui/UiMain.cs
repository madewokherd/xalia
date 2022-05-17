using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading.Tasks;

using Gazelle.Gudl;
using Gazelle.UiDom;
using Gazelle.Sdl;

namespace Gazelle.Ui
{
    internal class UiMain
    {
        public UiDomRoot Root { get; }
        public WindowingSystem Windowing { get; }

        private Dictionary<UiDomObject, OverlayBox> targetable_boxes = new Dictionary<UiDomObject, OverlayBox>();

        public UiMain(UiDomRoot root)
        {
            Root = root;
            Windowing = new WindowingSystem();

            root.ElementDeclarationsChangedEvent += OnElementDeclarationsChanged;
            root.ElementDiedEvent += OnElementDied;
            SearchForTargetableElements(root);
        }

        private void OnElementDied(object sender, UiDomObject e)
        {
            DiscardTargetableElement(e);
        }

        private void OnElementDeclarationsChanged(object sender, UiDomObject e)
        {
            UpdateTargetableElement(e);
        }

        private void SearchForTargetableElements(UiDomObject obj)
        {
            UpdateTargetableElement(obj);

            foreach (var child in obj.Children)
            {
                SearchForTargetableElements(child);
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
    }
}
