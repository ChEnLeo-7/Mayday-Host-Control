using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaydayHostControl
{
    // Intercept only our registered lists; all other game scroll views keep native behavior.
    internal static class SmoothListScroll
    {
        private sealed class State
        {
            internal ScrollRect Scroll;
            internal float Target;
            internal float LastPosition;
            internal float ContentHeight;
            internal float ViewHeight;
            internal bool Moving;
            internal bool Enabled;
        }

        private static readonly Dictionary<int, State> States = new();

        internal static void Register(ScrollRect scroll)
        {
            if (scroll == null) return;
            States[scroll.GetInstanceID()] = new State { Scroll = scroll, Enabled = true };
        }

        internal static void SetEnabled(ScrollRect scroll, bool enabled)
        {
            if (scroll == null || !States.TryGetValue(scroll.GetInstanceID(), out var state)) return;
            state.Enabled = enabled;
            state.Moving = false;
        }

        private static bool TryState(ScrollRect scroll, out State state)
        {
            state = null;
            return scroll != null && States.TryGetValue(scroll.GetInstanceID(), out state) &&
                state.Enabled && scroll.isActiveAndEnabled && scroll.content != null && scroll.viewport != null;
        }

        private static float Range(ScrollRect scroll) => Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);

        internal static bool OnWheel(ScrollRect scroll, PointerEventData data)
        {
            if (!TryState(scroll, out var state)) return false;
            var range = Range(scroll);
            var current = Mathf.Clamp01(scroll.verticalNormalizedPosition);
            if (!state.Moving || Mathf.Abs(current - state.LastPosition) > 0.002f)
                state.Target = current;
            var delta = data.scrollDelta.y;
            if (Mathf.Abs(data.scrollDelta.x) > Mathf.Abs(delta)) delta = data.scrollDelta.x;
            // Reversing direction discards outstanding travel instead of fighting the new input.
            if (state.Moving && delta * (state.Target - current) < 0f) state.Target = current;
            state.Target = range > 0.5f
                ? Mathf.Clamp01(state.Target + delta * scroll.scrollSensitivity / range)
                : 1f;
            state.ContentHeight = scroll.content.rect.height;
            state.ViewHeight = scroll.viewport.rect.height;
            state.LastPosition = current;
            state.Moving = range > 0.5f;
            scroll.StopMovement();
            data.Use();
            return true;
        }

        internal static void Cancel(ScrollRect scroll)
        {
            if (scroll != null && States.TryGetValue(scroll.GetInstanceID(), out var state)) state.Moving = false;
        }

        internal static void Tick(ScrollRect scroll)
        {
            if (!TryState(scroll, out var state) || !state.Moving) return;
            var current = Mathf.Clamp01(scroll.verticalNormalizedPosition);
            // Native dragging, scrollbar clicks, rebuilds and explicit position restoration win.
            if (Plugin.IsListPointerHeld || Mathf.Abs(current - state.LastPosition) > 0.002f ||
                Mathf.Abs(scroll.content.rect.height - state.ContentHeight) > 0.5f ||
                Mathf.Abs(scroll.viewport.rect.height - state.ViewHeight) > 0.5f)
            {
                state.Moving = false;
                return;
            }
            var next = Mathf.Lerp(current, state.Target, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
            if (Mathf.Abs(next - state.Target) * Range(scroll) < 0.25f)
            {
                next = state.Target;
                state.Moving = false;
            }
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = next;
            state.LastPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition);
        }

        internal static void Prune()
        {
            var stale = new List<int>();
            foreach (var pair in States)
                if (pair.Value.Scroll == null) stale.Add(pair.Key);
            foreach (var id in stale) States.Remove(id);
        }
    }

    [HarmonyPatch(typeof(ScrollRect), "OnScroll")]
    internal static class SmoothListWheelPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ScrollRect __instance, PointerEventData eventData) => !SmoothListScroll.OnWheel(__instance, eventData);
    }

    [HarmonyPatch(typeof(ScrollRect), "LateUpdate")]
    internal static class SmoothListUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ScrollRect __instance) => SmoothListScroll.Tick(__instance);
    }

    [HarmonyPatch(typeof(ScrollRect), "OnBeginDrag")]
    internal static class SmoothListDragPatch
    {
        [HarmonyPrefix]
        private static void Prefix(ScrollRect __instance) => SmoothListScroll.Cancel(__instance);
    }
}
