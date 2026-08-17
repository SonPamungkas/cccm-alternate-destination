using HarmonyLib;
using UnityEngine;
using InputFramework;
namespace AlteredDestination
{
    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    public static class DynamicMap_MapControls_Patch
    {
        private const float RetargetRadius = 1000f;
        public static void Postfix(DynamicMap __instance)
        {
            if (!DynamicMap.mapMaximized || !Input.GetMouseButtonDown(1)) return;
            if (!__instance.TryGetCursorCoordinates(out GlobalPosition cursorCoords)) return;
            bool keyHeld = IsAppendHeld();
            bool append = keyHeld && MapRouteDisplay.SessionActive;
            cursorCoords.y = SampleTerrainHeight(cursorCoords);
            Unit clickedEnemy = null;
            int salvoColor = -1;
            bool scanned = false;
            bool setAny = false;
            foreach (var baseIcon in __instance.selectedIcons)
            {
                if (!(baseIcon is UnitMapIcon unitIcon) || !(unitIcon.unit is Missile missile)) continue;
                if (!scanned)
                {
                    clickedEnemy = FindEnemyNear(cursorCoords);
                    scanned = true;
                }
                if (!AlteredDestinationPlugin.MissileWaypoints.TryGetValue(missile, out var waypointData))
                {
                    waypointData = new MissileWaypointData();
                    AlteredDestinationPlugin.MissileWaypoints.Add(missile, waypointData);
                }
                if (!append)
                {
                    waypointData.waypoints.Clear();
                    if (salvoColor < 0) salvoColor = MapRouteDisplay.NextSalvoColor();
                    waypointData.colorIndex = salvoColor;
                }
                else if (waypointData.colorIndex < 0)
                {
                    if (salvoColor < 0) salvoColor = MapRouteDisplay.NextSalvoColor();
                    waypointData.colorIndex = salvoColor;
                }
                waypointData.waypoints.Add(new OverrideData
                {
                    staticPos = cursorCoords,
                    targetUnit = clickedEnemy
                });
                MapRouteDisplay.Track(missile);
                setAny = true;
                if (keyHeld) MapRouteDisplay.SessionActive = true;
                if (clickedEnemy != null) MissileUtil.Retarget(missile, clickedEnemy);
            }
            if (setAny && AlteredDestinationPlugin.Verbose)
            {
                string verb = append ? "appended" : "set";
                string onto = clickedEnemy != null ? " onto " + clickedEnemy.name : "";
                AlteredDestinationPlugin.Log($"Missile waypoint {verb}{onto} at {cursorCoords}");
            }
        }
        public static bool IsAppendHeld()
        {
            if (!ExtraInputManager.RewiredInitialized) return false;
            Rewired.Player player = Rewired.ReInput.players.GetPlayer(0);
            return player != null && player.GetButton(AlteredDestinationPlugin.WaypointAction);
        }
        private static float SampleTerrainHeight(GlobalPosition cursorCoords)
        {
            Vector3 localClick = cursorCoords.ToLocalPosition();
            Vector3 rayOrigin = new Vector3(localClick.x, 20000f, localClick.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 30000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point.ToGlobalPosition().y;
            }
            if (Terrain.activeTerrain != null)
            {
                float sampled = Terrain.activeTerrain.SampleHeight(localClick);
                return new Vector3(localClick.x, sampled, localClick.z).ToGlobalPosition().y;
            }
            return cursorCoords.y;
        }
        private static Unit FindEnemyNear(GlobalPosition cursorCoords)
        {
            Unit closest = null;
            float closestDist = RetargetRadius;
            var allUnits = UnitRegistry.allUnits;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit u = allUnits[i];
                if (u == null || u is Missile || u.disabled || u.gameObject == null || !u.gameObject.activeInHierarchy) continue;
                if (DynamicMap.GetFactionMode(u.NetworkHQ) != FactionMode.Enemy) continue;
                GlobalPosition uPos = u.GlobalPosition();
                float dx = (float)(uPos.x - cursorCoords.x);
                float dz = (float)(uPos.z - cursorCoords.z);
                float dist2D = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist2D < closestDist)
                {
                    closestDist = dist2D;
                    closest = u;
                }
            }
            return closest;
        }
    }
}