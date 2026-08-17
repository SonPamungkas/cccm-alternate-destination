# Idea by FXR (raptoringus)
<img width="734" height="373" alt="cccm" src="https://github.com/user-attachments/assets/7d3a30ad-9883-4883-8d10-c9ee600b2517" />

## Cruise missile behaviour for Nuclear Option.
AI missile ships pick better targets and commit to them, sister ships coordinate their launches, a salvo under fire clears its own path, and you can draw routes for missiles in flight from the map.

Every system is independent and can be switched off on its own.

## Features

- **Smart Launch** — AI missile stations concentrate on the most valuable target in range instead of spreading fire across everything they can reach: the carrier rather than its escort, the hangar rather than the air defence. Optionally empties the whole weapon station into that target rather than stopping once the AI decides it has fired "enough". Cruise missile stations only, so guns and point defence keep rationing normally.

- **Synchronized Launch** — when a ship fires a cruise missile, sister ships of the same class that can also reach the target are forced to launch at it as well. Distance and bearing are ignored, so ships spread around the map all fire together for a multi-pronged attack.

- **Smart Swarm (DEAD)** — when something starts shooting at the salvo, one missile breaks off and destroys the shooter. It picks the fastest missile that is still in cruise and far enough out to turn onto the threat, never one already in its terminal dive, and never at all if the strike is too small to spare one. If the threat dies first, that missile immediately resumes its original target.

- **Waypoint routes** — select missiles on the maximized map and draw them a route. Hold the **MissileWaypoint** key and right-click to build a multi-leg route; release and hold again to start a separate route. Plain right-click sets a single destination. Routes draw on the map, one colour per  salvo, and a missile resumes onto its original target once it has flown the last leg. Right-clicking  on an enemy retargets the missile; right-clicking open terrain is pure navigation and leaves its target alone.

- **Per-type cruise altitude** — every cruise missile type gets its own altitude entry, seeded from that type's own stock value. Give two types different heights and a mixed salvo stops flying on one plane.

- **Direct naval attack** (optional) — replaces the built-in pop-up with a flat, level run-in into the hull.

## Keybind

`MissileWaypoint` appears under **Controls > Debug** and ships **unbound** — the input framework cannot assign a default. Bind it before using waypoint routes; until then every right-click replaces the route instead of extending it.
