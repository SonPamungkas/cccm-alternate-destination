using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace AlteredDestination
{
    public static class MapRouteDisplay
    {
        private static readonly Color[] Wheel =
        {
            new Color(1f,    0f,    0f,    0.8f),
            new Color(1f,    0.49f, 0f,    0.8f),
            new Color(1f,    1f,    0f,    0.8f),
            new Color(0.49f, 1f,    0f,    0.8f),
            new Color(0f,    1f,    0f,    0.8f),
            new Color(0f,    1f,    0.49f, 0.8f),
            new Color(0f,    1f,    1f,    0.8f),
            new Color(0f,    0.49f, 1f,    0.8f),
            new Color(0f,    0f,    1f,    0.8f),
            new Color(0.49f, 0f,    1f,    0.8f),
            new Color(1f,    0f,    1f,    0.8f),
            new Color(1f,    0f,    0.49f, 0.8f),
        };
        private static int nextColorIndex;
        private static bool hidden;
        public static bool MapExists;
        public static bool SessionActive;
        public static void NoteKeyHeld(bool held)
        {
            if (!held) SessionActive = false;
        }
        private static readonly List<Missile> tracked = new List<Missile>();
        private static readonly Dictionary<Missile, List<GameObject>> segments = new Dictionary<Missile, List<GameObject>>();
        public static int NextSalvoColor()
        {
            int index = nextColorIndex;
            nextColorIndex = (nextColorIndex + 1) % Wheel.Length;
            return index;
        }
        public static void Track(Missile missile)
        {
            if (missile != null && !tracked.Contains(missile)) tracked.Add(missile);
        }
        public static void Redraw()
        {
            if (tracked.Count == 0) return;
            if (!MapExists || !DynamicMap.mapMaximized)
            {
                if (!hidden) { HideAll(); hidden = true; }
                return;
            }
            hidden = false;
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.iconLayer == null) return;
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                Missile missile = tracked[i];
                if (missile == null || missile.disabled
                    || !AlteredDestinationPlugin.MissileWaypoints.TryGetValue(missile, out var data)
                    || data.waypoints.Count == 0)
                {
                    Release(missile);
                    tracked.RemoveAt(i);
                    continue;
                }
                DrawRoute(map, missile, data);
            }
        }
        private static void DrawRoute(DynamicMap map, Missile missile, MissileWaypointData data)
        {
            if (!segments.TryGetValue(missile, out var lines))
            {
                lines = new List<GameObject>();
                segments[missile] = lines;
            }
            Color color = Wheel[Mathf.Clamp(data.colorIndex, 0, Wheel.Length - 1)];
            Vector3 from = ToMapLocal(missile.GlobalPosition(), map);
            for (int leg = 0; leg < data.waypoints.Count; leg++)
            {
                OverrideData wp = data.waypoints[leg];
                GlobalPosition legPos = wp.targetUnit != null && !wp.targetUnit.disabled
                    ? wp.targetUnit.GlobalPosition()
                    : wp.staticPos;
                Vector3 to = ToMapLocal(legPos, map);
                while (lines.Count <= leg) lines.Add(CreateLine(map.iconLayer.transform));
                Place(lines[leg], from, to, color);
                from = to;
            }
            for (int extra = data.waypoints.Count; extra < lines.Count; extra++)
            {
                if (lines[extra] != null) lines[extra].SetActive(false);
            }
        }
        private static Vector3 ToMapLocal(GlobalPosition position, DynamicMap map)
        {
            Vector3 scaled = position.AsVector3() * map.mapDisplayFactor;
            return new Vector3(scaled.x, scaled.z, 0f);
        }
        private static void Place(GameObject line, Vector3 from, Vector3 to, Color color)
        {
            if (line == null) return;
            Vector3 diff = to - from;
            float distance = diff.magnitude;
            if (distance < 1f)
            {
                line.SetActive(false);
                return;
            }
            line.SetActive(true);
            var image = line.GetComponent<Image>();
            if (image != null) image.color = color;
            var rect = line.GetComponent<RectTransform>();
            rect.localPosition = from;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
            rect.sizeDelta = new Vector2(distance, AlteredDestinationPlugin.LineThickness.Value);
        }
        private static GameObject CreateLine(Transform parent)
        {
            var go = new GameObject("MissileRouteLeg");
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            return go;
        }
        private static void HideAll()
        {
            foreach (var pair in segments)
            {
                foreach (GameObject line in pair.Value)
                {
                    if (line != null) line.SetActive(false);
                }
            }
        }
        public static void Release(Missile missile)
        {
            if (missile == null || !segments.TryGetValue(missile, out var lines)) return;
            foreach (GameObject line in lines)
            {
                if (line != null) Object.Destroy(line);
            }
            segments.Remove(missile);
        }
        public static void Forget()
        {
            segments.Clear();
            tracked.Clear();
            hidden = false;
        }
    }
}