using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Object = System.Object;

namespace XRTLogging
{
    /// <summary>
    /// Meta XR-compatible version of the old SteamVR PointedAtLogger.
    ///
    /// What changed:
    /// - Removed Valve.VR.InteractionSystem.Hand.
    /// - Pointer logging still uses raycasts from assigned transforms.
    /// - Hand/controller hover logging now uses Meta-safe transforms, raycasts, and optional overlap-sphere fallback.
    ///
    /// Scene setup:
    /// - For trackedPointers, assign the transform you want to raycast from, usually a controller ray origin,
    ///   hand ray origin, CenterEyeAnchor, or any custom pointer transform.
    /// - For handHovers, assign LeftHandAnchor / RightHandAnchor / LeftControllerAnchor / RightControllerAnchor,
    ///   or leave the transform empty and enable autoFindMetaAnchorByName with a name containing "left" or "right".
    /// </summary>
    public class PointedAtLogger : ABufferedLogger
    {
        public bool defaultToGameObjectName = true;

        [Tooltip("The name to write when no valid object is being pointed at, for example the skybox.")]
        public string defaultNoObjectName = "null";

        [Header("Raycast Settings")]
        [Tooltip("Maximum distance used for pointer and hand/controller raycasts.")]
        public float defaultRaycastDistance = 20f;

        [Tooltip("Layers that can be logged by raycasts/hover checks.")]
        public LayerMask defaultLayerMask = ~0;

        [Tooltip("Whether trigger colliders should be considered by raycasts/hover checks.")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        public override string loggerNameForMetadata => "pointing at logging";
        protected override string specificLoggingDirectory => Path.Combine(loggingDirectory, "Pointing");

        private string _totalFormatString;

        protected override string totalFormatString
        {
            get
            {
                if (!string.IsNullOrEmpty(_totalFormatString)) return _totalFormatString;

                var sb = new StringBuilder();
                sb.Append("{0:yyyy-MM-dd HH:mm:ss.fff},");

                var index = 1;
                foreach (var _ in SafePointerList())
                {
                    sb.Append($"{{{index++}}},");
                }

                foreach (var _ in SafeHandHoverList())
                {
                    sb.Append($"{{{index++}}},");
                }

                _totalFormatString = sb.ToString();
                if (index < totalFields)
                {
                    Debug.LogWarning($"format string does not go to total number of fields.\nfields={totalFields}\nstring={_totalFormatString}");
                }

                return _totalFormatString;
            }
        }

        public List<namedTransformDirection> trackedPointers = new List<namedTransformDirection>();

        [Serializable]
        public class namedTransformDirection
        {
            public string name;

            [Tooltip("Transform to raycast from, such as a controller ray origin, hand ray origin, or CenterEyeAnchor.")]
            public Transform transform;

            [Tooltip("Local-space direction. Vector3.forward means the transform's blue Z axis.")]
            public Vector3 direction = Vector3.forward;

            [Tooltip("Optional per-pointer raycast distance. If 0 or less, the logger uses defaultRaycastDistance.")]
            public float raycastDistance = 0f;
        }

        public List<namedHand> handHovers = new List<namedHand>();

        [Serializable]
        public class namedHand
        {
            public string name;

            [Tooltip("Assign a Meta hand/controller anchor or ray origin. Examples: LeftHandAnchor, RightHandAnchor, LeftControllerAnchor, RightControllerAnchor.")]
            public Transform transform;

            [Tooltip("If transform is empty, the logger will try to find a Meta left/right hand or controller anchor using this entry's name.")]
            public bool autoFindMetaAnchorByName = true;

            [Tooltip("Local-space ray direction. Vector3.forward means the transform's blue Z axis.")]
            public Vector3 direction = Vector3.forward;

            [Tooltip("Optional per-hand/controller raycast distance. If 0 or less, the logger uses defaultRaycastDistance.")]
            public float raycastDistance = 0f;

            [Tooltip("If the raycast misses, also check nearby colliders around the hand/controller position.")]
            public bool useHoverRadiusFallback = true;

            [Tooltip("Radius for the fallback overlap check around the hand/controller position.")]
            public float hoverRadius = 0.08f;
        }

        private void OnValidate()
        {
            _totalFormatString = null;
            _totalFields = -1;

            if (defaultRaycastDistance <= 0f) defaultRaycastDistance = 20f;

            if (trackedPointers == null) trackedPointers = new List<namedTransformDirection>();
            if (handHovers == null) handHovers = new List<namedHand>();
        }

        private void Update()
        {
            if (!isLogging) return;

            if (firstLog)
            {
                if (File.Exists(completeLogFilePath))
                {
                    _completeLogFilePath = "";
                    return;
                }

                Debug.Log($"[PointedAtLogger] Will be logging to {completeLogFilePath}");
                Directory.CreateDirectory(Path.GetDirectoryName(completeLogFilePath) ?? "");

                try
                {
                    using (var fc = File.CreateText(completeLogFilePath))
                    {
                        fc.WriteLine(GetHeader());
                    }
                }
                catch (ArgumentException e)
                {
                    Debug.LogError($"[PointedAtLogger] something wrong with this path? {completeLogFilePath}");
                    throw new ArgumentException(e.Message, e.ParamName, e.InnerException);
                }

                firstLog = false;
            }

            var outputBuffer = new Object[totalFields];
            var dataIndex = 0;
            AddObjectsToBuffer(ref outputBuffer, ref dataIndex);
            AddToDataQueue(outputBuffer);
        }

        private string GetHeader()
        {
            var sb = new StringBuilder();
            sb.Append("Timestamp,");

            foreach (var p in SafePointerList())
            {
                sb.Append($"{GetColumnName(p?.name, "Pointer")},");
            }

            foreach (var h in SafeHandHoverList())
            {
                sb.Append($"{GetColumnName(h?.name, "HandOrController")},");
            }

            return sb.ToString();
        }

        private void AddObjectsToBuffer(ref Object[] outputBuffer, ref int dataIndex)
        {
            outputBuffer[dataIndex++] = timestampProvider.Timestamp;

            foreach (var pointer in SafePointerList())
            {
                outputBuffer[dataIndex++] = ResolvePointedObject(pointer);
            }

            foreach (var handHover in SafeHandHoverList())
            {
                outputBuffer[dataIndex++] = ResolveHandOrControllerHover(handHover);
            }
        }

        private string ResolvePointedObject(namedTransformDirection pointer)
        {
            if (pointer == null || pointer.transform == null || !pointer.transform.gameObject.activeInHierarchy)
            {
                return defaultNoObjectName;
            }

            var distance = pointer.raycastDistance > 0f ? pointer.raycastDistance : defaultRaycastDistance;
            var rayDirection = pointer.transform.rotation * SafeDirection(pointer.direction);

            if (Physics.Raycast(pointer.transform.position, rayDirection, out var hit, distance, defaultLayerMask, triggerInteraction))
            {
                return GetLogName(hit.collider.gameObject);
            }

            return defaultNoObjectName;
        }

        private string ResolveHandOrControllerHover(namedHand handHover)
        {
            if (handHover == null) return defaultNoObjectName;

            var sourceTransform = ResolveHoverTransform(handHover);
            if (sourceTransform == null || !sourceTransform.gameObject.activeInHierarchy)
            {
                return defaultNoObjectName;
            }

            var distance = handHover.raycastDistance > 0f ? handHover.raycastDistance : defaultRaycastDistance;
            var rayDirection = sourceTransform.rotation * SafeDirection(handHover.direction);

            if (Physics.Raycast(sourceTransform.position, rayDirection, out var hit, distance, defaultLayerMask, triggerInteraction))
            {
                return GetLogName(hit.collider.gameObject);
            }

            if (!handHover.useHoverRadiusFallback || handHover.hoverRadius <= 0f)
            {
                return defaultNoObjectName;
            }

            var colliders = Physics.OverlapSphere(sourceTransform.position, handHover.hoverRadius, defaultLayerMask, triggerInteraction);
            var nearest = GetNearestCollider(sourceTransform.position, colliders);
            return nearest != null ? GetLogName(nearest.gameObject) : defaultNoObjectName;
        }

        private Transform ResolveHoverTransform(namedHand handHover)
        {
            if (handHover.transform != null) return handHover.transform;
            if (!handHover.autoFindMetaAnchorByName) return null;

            var lowerName = (handHover.name ?? string.Empty).ToLowerInvariant();
            var wantsLeft = lowerName.Contains("left") || lowerName.Contains(" l ") || lowerName.StartsWith("l_") || lowerName.EndsWith("_l");
            var wantsRight = lowerName.Contains("right") || lowerName.Contains(" r ") || lowerName.StartsWith("r_") || lowerName.EndsWith("_r");

            if (wantsLeft) return FindAnchorByNames("LeftHandAnchor", "LeftControllerAnchor", "Left Hand", "Hand_L", "L_Hand");
            if (wantsRight) return FindAnchorByNames("RightHandAnchor", "RightControllerAnchor", "Right Hand", "Hand_R", "R_Hand");

            return null;
        }

        private Transform FindAnchorByNames(params string[] nameHints)
        {
#if UNITY_2023_1_OR_NEWER
            var transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
#else
            var transforms = FindObjectsOfType<Transform>();
#endif
            foreach (var t in transforms)
            {
                if (t == null) continue;
                var transformName = t.name;
                foreach (var hint in nameHints)
                {
                    if (transformName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        private Collider GetNearestCollider(Vector3 origin, Collider[] colliders)
        {
            if (colliders == null || colliders.Length == 0) return null;

            Collider nearest = null;
            var bestDistance = float.PositiveInfinity;

            foreach (var c in colliders)
            {
                if (c == null) continue;

                var closest = c.ClosestPoint(origin);
                var distance = Vector3.SqrMagnitude(closest - origin);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                nearest = c;
            }

            return nearest;
        }

        private string GetLogName(GameObject hitObject)
        {
            if (hitObject == null) return defaultNoObjectName;

            if (hitObject.TryGetComponent(out PointedAtLabel pointedAtLabel))
            {
                return pointedAtLabel.label;
            }

            var parentLabel = hitObject.GetComponentInParent<PointedAtLabel>();
            if (parentLabel != null)
            {
                return parentLabel.label;
            }

            return defaultToGameObjectName ? hitObject.name : defaultNoObjectName;
        }

        private static Vector3 SafeDirection(Vector3 direction)
        {
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        private static string GetColumnName(string configuredName, string fallback)
        {
            return string.IsNullOrWhiteSpace(configuredName) ? fallback : configuredName;
        }

        private IEnumerable<namedTransformDirection> SafePointerList()
        {
            if (trackedPointers == null)
            {
                return Array.Empty<namedTransformDirection>();
            }

            return trackedPointers;
        }

        private IEnumerable<namedHand> SafeHandHoverList()
        {
            if (handHovers == null)
            {
                return Array.Empty<namedHand>();
            }

            return handHovers;
        }

        private int _totalFields = -1;

        private int totalFields
        {
            get
            {
                if (_totalFields != -1) return _totalFields;

                _totalFields =
                    1 + // timestamp
                    (trackedPointers?.Count ?? 0) +
                    (handHovers?.Count ?? 0);

                return _totalFields;
            }
        }
    }
}
