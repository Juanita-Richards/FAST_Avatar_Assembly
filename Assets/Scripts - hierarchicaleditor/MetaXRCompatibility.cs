using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PlayStructure
{
    /// <summary>
    /// Small adapter layer that removes this project's SteamVR dependency.
    /// It works with Meta XR / Interaction SDK by looking for common Meta components
    /// such as OVRHand, Oculus.Interaction.Grabbable, HandGrabInteractable, and
    /// PointableElement-derived components.
    /// </summary>
    public static class MetaXRCompatibility
    {
        public enum HandSide
        {
            Unknown,
            Left,
            Right
        }

        public readonly struct HandInfo
        {
            public readonly Transform transform;
            public readonly HandSide side;

            public HandInfo(Transform transform, HandSide side)
            {
                this.transform = transform;
                this.side = side;
            }
        }
        // UPDATED: Expanded and corrected name hints so left/right anchors are detected reliably.
        private static readonly string[] LeftHandNameHints =
        {
            // Meta/OVR
            "lefthandanchor", "left hand anchor", "left_hand", "left hand", "hand_l",
            "lefthanddatasource", "leftcontrolleranchor",
            // OpenXR / XRI / Action-based
            "openxrlefthand", "xri lefthand", "xri left hand", "actionbasedlefthand",
            "leftcontroller", "left controller", "controller_left",
            // Other common rig names
            "left hand controller", "leftxrcontroller",
            // Misc variations seen in rigs
            "left hand device", "leftdevice", "left_hand_anchor"
        };
        private static readonly string[] RightHandNameHints =
        {
            // Meta/OVR
            "righthandanchor", "right hand anchor", "right_hand", "right hand", "hand_r",
            "righthanddatasource", "rightcontrolleranchor",
            // Fixed typos from original
            "metaquesttouchplus_right",
            // OpenXR / XRI / Action-based
            "openxrrighthand", "xri righthand", "xri right hand", "actionbasedrighthand",
            "rightcontroller", "right controller", "controller_right",
            // Other common rig names
            "right hand controller", "rightxrcontroller",
            // Misc variations seen in rigs
            "right hand device", "rightdevice", "right_hand_anchor"
        };

        public static Transform GetLeftHandTransform(Transform overrideTransform = null)
        {
            return overrideTransform != null ? overrideTransform : FindHandTransform(HandSide.Left);
        }

        public static Transform GetRightHandTransform(Transform overrideTransform = null)
        {
            return overrideTransform != null ? overrideTransform : FindHandTransform(HandSide.Right);
        }

        public static Transform[] GetHandTransforms(Transform leftOverride = null, Transform rightOverride = null)
        {
            var hands = new List<Transform>();
            var left = GetLeftHandTransform(leftOverride);
            var right = GetRightHandTransform(rightOverride);

            if (left != null) hands.Add(left);
            if (right != null && right != left) hands.Add(right);

            return hands.ToArray();
        }

        public static HandInfo GetBestHandForCue(GameObject target)
        {
            var targetPosition = target != null ? target.transform.position : Vector3.zero;
            var left = GetLeftHandTransform();
            var right = GetRightHandTransform();
            Debug.Log($"Left hand found: {(left != null ? left.name : "NULL")} | Right hand found: {(right != null ? right.name : "NULL")}");

            if (left == null && right == null) return new HandInfo(null, HandSide.Unknown);
            if (left != null && right == null) return new HandInfo(left, HandSide.Left);
            if (right != null && left == null) return new HandInfo(right, HandSide.Right);

            var leftDistance = Vector3.Distance(left.position, targetPosition);
            var rightDistance = Vector3.Distance(right.position, targetPosition);
            return leftDistance <= rightDistance
                ? new HandInfo(left, HandSide.Left)
                : new HandInfo(right, HandSide.Right);


        }

        public static bool IsHeld(GameObject obj)
        {
            if (obj == null) return false;

            var trackers = obj.GetComponentsInParent<MetaXRGrabStateTracker>(true)
                .Concat(obj.GetComponentsInChildren<MetaXRGrabStateTracker>(true));
            if (trackers.Any(t => t != null && t.IsHeld)) return true;

            return HasSelectingPoints(obj);
        }

        public static bool AnyObjectHeld()
        {
            return UnityEngine.Object.FindObjectsByType<MetaXRGrabStateTracker>(FindObjectsSortMode.None)
                .Any(t => t != null && t.IsHeld);
        }

        public static MetaXRGrabStateTracker EnsureGrabTracker(GameObject obj)
        {
            if (obj == null) return null;
            var tracker = obj.GetComponent<MetaXRGrabStateTracker>();
            if (tracker == null) tracker = obj.AddComponent<MetaXRGrabStateTracker>();
            return tracker;
        }

        public static void SetGrabLock(GameObject obj, bool locked)
        {
            var tracker = EnsureGrabTracker(obj);
            if (tracker == null) return;

            tracker.IsGrabLocked = locked;
            if (locked) tracker.NotifyGrabbed();
        }

        public static List<Behaviour> GetGrabBehaviours(GameObject obj)
        {
            var result = new List<Behaviour>();
            if (obj == null) return result;

            foreach (var component in obj.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                if (component is Behaviour behaviour && IsGrabRelatedType(component.GetType()))
                {
                    result.Add(behaviour);
                }
            }

            return result;
        }

        public static void RemoveGrabComponents(GameObject obj)
        {
            if (obj == null) return;

            foreach (var component in obj.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                if (!IsGrabRelatedType(component.GetType())) continue;
                DestroyComponent(component);
            }
        }

        internal static bool HasSelectingPoints(GameObject obj)
        {
            return TryReadSelectingState(obj, out var selected) && selected;
        }

        internal static bool TryReadSelectingState(GameObject obj, out bool selected)
        {
            selected = false;
            if (obj == null) return false;

            var foundSelectionReadableComponent = false;
            var components = obj.GetComponentsInParent<MonoBehaviour>(true)
                .Concat(obj.GetComponentsInChildren<MonoBehaviour>(true));

            foreach (var component in components)
            {
                if (component == null) continue;
                if (!TryComponentReportsSelected(component, out var componentSelected)) continue;

                foundSelectionReadableComponent = true;
                if (componentSelected) selected = true;
            }

            return foundSelectionReadableComponent;
        }

        private static bool TryComponentReportsSelected(MonoBehaviour component, out bool selected)
        {
            selected = false;
            var type = component.GetType();

            // Meta Interaction SDK PointableElement/Grabbable exposes SelectingPointsCount.
            if (TryGetIntMember(type, component, "SelectingPointsCount", out var selectingPointsCount))
            {
                selected = selectingPointsCount > 0;
                return true;
            }

            // Fallbacks for grab components that expose a simple selected/grabbed boolean.
            if (TryGetBoolMember(type, component, "IsSelected", out selected)) return true;
            if (TryGetBoolMember(type, component, "IsGrabbed", out selected)) return true;
            if (TryGetBoolMember(type, component, "Selected", out selected)) return true;

            return false;
        }

        private static Transform FindHandTransform(HandSide side)
        {
            // First try OVRHand components, if they exist in the scene.
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
             FindObjectsSortMode.None))
            {
                if (behaviour == null) continue;
                var type = behaviour.GetType();
                if (type.Name != "OVRHand") continue;

                var handType = GetObjectMember(type, behaviour, "HandType")?.ToString();
                if (string.IsNullOrEmpty(handType)) continue;

                if (side == HandSide.Left && handType.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
                    return behaviour.transform;
                if (side == HandSide.Right && handType.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
                    return behaviour.transform;
            }

            // Then try common OVRCameraRig / Interaction SDK anchor names.
            var hints = side == HandSide.Left ? LeftHandNameHints : RightHandNameHints;
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
             FindObjectsSortMode.None))
            {
                var lowerName = transform.name.ToLowerInvariant();
                if (hints.Any(h => lowerName.Contains(h.ToLowerInvariant()))) return transform;
            }

            return null;
        }

        private static bool IsGrabRelatedType(Type type)
        {
            var fullName = type.FullName ?? type.Name;
            var name = type.Name;

            // Meta XR / Interaction SDK component names.
            if (fullName.StartsWith("Oculus.Interaction", StringComparison.Ordinal))
            {
                return fullName.IndexOf("Grab", StringComparison.OrdinalIgnoreCase) >= 0
                       || fullName.IndexOf("Interactable", StringComparison.OrdinalIgnoreCase) >= 0
                       || fullName.IndexOf("Pointable", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Legacy Oculus Integration names.
            return name.IndexOf("OVRGrab", StringComparison.OrdinalIgnoreCase) >= 0
                   || name.IndexOf("OVRGrabbable", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetIntMember(Type type, object instance, string memberName)
        {
            return TryGetIntMember(type, instance, memberName, out var value) ? value : 0;
        }

        private static bool TryGetIntMember(Type type, object instance, string memberName, out int parsedValue)
        {
            parsedValue = 0;
            var value = GetObjectMember(type, instance, memberName);
            if (value is int i)
            {
                parsedValue = i;
                return true;
            }
            if (value == null) return false;
            return int.TryParse(value.ToString(), out parsedValue);
        }

        private static bool TryGetBoolMember(Type type, object instance, string memberName, out bool parsedValue)
        {
            parsedValue = false;
            var value = GetObjectMember(type, instance, memberName);
            if (value is bool b)
            {
                parsedValue = b;
                return true;
            }
            if (value == null) return false;
            return bool.TryParse(value.ToString(), out parsedValue);
        }

        private static object GetObjectMember(Type type, object instance, string memberName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try { return property.GetValue(instance); }
                catch { /* Some Meta components throw before Start; ignore and use other fallbacks. */ }
            }

            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                try { return field.GetValue(instance); }
                catch { /* Ignore and use other fallbacks. */ }
            }

            return null;
        }

        private static void DestroyComponent(UnityEngine.Object component)
        {
            if (component == null) return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(component);
            else
                UnityEngine.Object.DestroyImmediate(component);
        }
    }

    /// <summary>
    /// Optional grab-state tracker for project scripts. This can be added automatically by
    /// MetaXRCompatibility.EnsureGrabTracker or manually to grabbable objects.
    /// </summary>
    public class MetaXRGrabStateTracker : MonoBehaviour
    {
        [SerializeField] private bool isHeld;
        [SerializeField] private bool isGrabLocked;

        public bool IsHeld => isHeld;

        public bool IsGrabLocked
        {
            get => isGrabLocked;
            set => isGrabLocked = value;
        }

        public void NotifyGrabbed()
        {
            isHeld = true;
        }

        public void NotifyReleased()
        {
            if (!isGrabLocked) isHeld = false;
        }

        private void Update()
        {
            if (isGrabLocked)
            {
                isHeld = true;
                return;
            }

            if (MetaXRCompatibility.TryReadSelectingState(gameObject, out var selected))
            {
                isHeld = selected;
            }
        }
    }
}
