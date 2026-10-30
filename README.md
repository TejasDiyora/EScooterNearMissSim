# E-Scooter Near-Miss Simulator

A Unity simulation of e-scooters, cars, pedestrians and traffic signals at a four-way intersection. It detects near-misses and collisions involving the scooters, shows the results on a dashboard, and exports them to CSV.

Everything is generated from code when you press Play: the intersection, the NavMesh, the road users and the interface. There are no prefabs, textures or scenes to set up by hand.

## Setup

You need Unity 2022.3 LTS or Unity 6. The scripts are written to work with any render pipeline (Built-in, URP or HDRP).

1. In Unity Hub, create a new **3D** project. The Universal 3D template is fine.
2. Copy the `Assets/Scripts` folder from this download into your project's `Assets` folder.
3. Return to Unity and wait for it to finish compiling.
4. In the menu bar choose **Tools > E-Scooter Sim > Create Simulation Scene**. This creates and saves `Assets/Scenes/Intersection.unity`.
5. Set the Game view to a 16:9 aspect ratio (the drop-down at the top of the Game tab) and press **Play**.

If the Tools menu item is missing, do step 4 by hand: create a new scene, add an empty GameObject, and add the **Simulation Bootstrap** component to it.

## Using it

1. **Setup screen.** Pick a ready-made scenario or move the sliders, then press **Start run**.
2. **During the run.** The panel at the top left shows the clock and the live counts. Use the speed buttons to play faster, **Skip to end** to finish the run instantly, and the camera button to switch between the overview and a view that follows scooter 1.
3. **Results.** When the run ends the dashboard shows the totals, a breakdown by type of conflict, and the settings that produced them. **Export CSV** writes the data files. **Run with next seed** repeats the scenario with different random traffic.

What you see in the scene:

| Shape | Meaning |
|---|---|
| Rider in orange | Scooter 1, the one the camera can follow |
| Riders in yellow | The other scooters |
| Blue pedestrians | Wait for the walk signal |
| Pink pedestrians | Cross whenever there is a gap in car traffic |
| Flashing amber lamps on a car | It is indicating a right turn |
| Coloured bar before each stop line | Current signal state for that approach |
| Yellow disc on the road | A near-miss was logged here |
| Red disc on the road | A collision was logged here |

## Scenario settings

| Setting | Default | What it does |
|---|---|---|
| Number of scooters | 4 | Scooters riding at once. Each is tracked for near-misses. |
| Scooter speed | 5.5 m/s | Cruising speed (about 20 km/h). |
| Rider reaction time | 1.0 s | Delay between a hazard appearing and the rider responding. |
| Hardest braking | 4.5 m/s2 | The scooter's emergency deceleration. |
| Vehicle speed | 11 m/s | Cruising speed (about 40 km/h). |
| Time between vehicles | 5 s | Average gap between cars on each approach. Lower means heavier traffic. |
| Passing clearance | 1.2 m | Average side gap a car leaves when it passes a scooter. Each car varies by up to 0.3 m. |
| Vehicles turning right | 25% | These cars cross the bike lane to turn. |
| Careful drivers | 60% | Share of right-turning drivers who wait for a scooter and indicate early. The rest do not check and indicate late or not at all. |
| Number of pedestrians | 12 | Pedestrians walking at any time. |
| Walking speed | 1.4 m/s | Each pedestrian varies by up to 15%. |
| Wait for the walk signal | 75% | The rest cross on gaps in car traffic and do not look for scooters. |
| Passing closer than | 1.0 m | Distance threshold for a near-miss. |
| Time-to-collision below | 1.5 s | TTC threshold for a near-miss. |
| Run length | 180 s | Simulated time. |
| Random seed | 42 | The same settings and seed always give the same run. |

Signal timing (20 s green, 3 s yellow, 2 s all-red) and the detection radius (12 m) are fields of `ScenarioConfig` without a slider.

The ready-made scenarios each change one aspect of Baseline:

| Scenario | Change |
|---|---|
| Fast scooter | Scooter speed 8.5 m/s |
| Slow reaction | Reaction time 2.0 s |
| Risky pedestrians | 18 pedestrians, 30% wait for the signal |
| Close passing | Passing clearance 0.8 m, a car every 3.5 s |
| Aggressive turns | 50% turn right, 10% careful drivers, 4 pedestrians |

Aggressive turns also lowers the pedestrian count. Pedestrians on a crosswalk hold right-turning cars back at the start of each green, which lets the scooters get away first and hides the turn conflict.

To keep your own scenarios as assets, right-click in the Project window and choose **Create > E-Scooter Sim > Scenario Preset**. Assign one to the **Start Preset** field of the Simulation Bootstrap component and tick **Start Immediately** to skip the setup screen.

## How an event is decided

Every vehicle or pedestrian that comes within 12 m of a scooter starts an **encounter**. The detector tracks the encounter until the two are apart again and classifies it once, when it ends.

| Result | Rule |
|---|---|
| Collision | The two bodies overlapped while closing at 1 m/s or more. |
| Near-miss by distance | A moving road user passed closer than the distance threshold. |
| Near-miss by TTC | The two stayed on a collision course with a time-to-collision below the threshold for at least 0.3 s. |
| Normal pass | Anything else. Counted as an encounter but not logged as an event. |

Details that matter when you explain the numbers:

- **Bodies are rows of circles on the ground.** A car is three circles, a scooter three small ones, a pedestrian one. The gap is the free space between the nearest two circles.
- **Time-to-collision** is the time until the two bodies touch if both keep their current velocity. If their paths do not meet it is infinite. This is what separates a real conflict from two road users simply being near each other.
- **Only movement counts.** A car queued beside a scooter at a red light is close but is not a near-miss. A car that has just braked to a stop to avoid a scooter still counts.
- **Each event has a type**: `ClosePass` (a car going the same way passed too close), `PedestrianCrossing`, or `TurnConflict` (a car turning across the bike lane).

## Output files

**Export CSV** writes to a folder named `EScooterSimData` under Unity's persistent data path. The dashboard shows the full path after exporting, and it is also printed in the Console. On Windows it is under `C:\Users\<you>\AppData\LocalLow\<Company>\<Product>\`. On macOS it is under `~/Library/Application Support/<Company>/<Product>/`.

`events_<run>_<scenario>.csv` has one row per near-miss or collision:

| Column | Meaning |
|---|---|
| `run_id`, `scenario_name` | Which run the row belongs to |
| `time_s` | Simulation time of the closest approach |
| `event_type` | `NearMiss` or `Collision` |
| `conflict_type` | `ClosePass`, `PedestrianCrossing` or `TurnConflict` |
| `trigger` | Which rule fired: `Contact`, `Distance`, `TTC` or `Distance+TTC` |
| `scooter_id` | Which scooter |
| `other_agent_type`, `other_agent_id` | The other road user |
| `scooter_speed_mps`, `other_speed_mps` | Speeds at the closest approach |
| `min_distance_m` | Closest the two bodies came. 0 for a collision. |
| `min_ttc_s` | Lowest sustained time-to-collision. Empty if it never dropped below the threshold. |
| `signal_state` | The scooter's signal at that moment |
| `scooter_heading` | Direction the scooter was travelling |
| `reaction_time_s` | The reaction time setting for the run |
| `pos_x`, `pos_z` | Where it happened. The intersection centre is 0, 0. |

`run_summary.csv` gets one row added per exported run: every setting, the totals, the breakdown by conflict type, the number of encounters, the distance the scooters rode, and events per kilometre. Export several runs and you have one table for comparing scenarios.

## How the code is organised

```
Assets/Scripts/
  Core/        ScenarioConfig, ScenarioPreset, SimulationManager, SimulationBootstrap
  Roads/       RoadLayout, RoutePath, NavMeshBaker, IntersectionBuilder
  Agents/      AgentBase, ScooterController, VehicleController, PedestrianController,
               Spawner, AgentFactory
  Traffic/     IntersectionManager, TrafficLightController
  Detection/   ConflictMath, Encounter, NearMissDetector
  Data/        NearMissEvent, DataRecorder, CsvExporter
  UI/          UIFactory, ScenarioSetupUI, HudUI, ResultsDashboardUI
  Visuals/     MaterialLibrary, Shapes, CameraRig, EventMarkers
  Editor/      CreateSimulationScene
```

| Script | Job |
|---|---|
| `SimulationBootstrap` | The one component in the scene. Builds everything on Play. |
| `SimulationManager` | Owns the clock, the agent list, the random generator and the update order. |
| `ScenarioConfig` | Every setting for a run, plus the ready-made scenarios. |
| `RoadLayout` | The intersection's dimensions and the routes agents follow. |
| `RoutePath` | A polyline an agent follows, addressed by distance travelled. |
| `NavMeshBaker` | Builds the NavMesh at start-up with separate road, sidewalk and crosswalk areas, and plans each agent's path on it. |
| `AgentBase` | Shared behaviour: follow a path, change speed, obey the signal, find what is in the way. |
| `ScooterController` | Rides the bike lane and responds to hazards after the reaction time. |
| `VehicleController` | Keeps its lane, follows, turns right, indicates, gives way. |
| `PedestrianController` | Walks the sidewalk and crosses on the walk signal or on a gap. |
| `Spawner` | Adds scooters, cars and pedestrians. |
| `IntersectionManager` | Runs the signal cycle. |
| `TrafficLightController` | Shows one signal's state. |
| `NearMissDetector` | Tracks encounters and classifies them. |
| `ConflictMath` | Gap and time-to-collision calculations. |
| `DataRecorder` | Holds the events and running totals. |
| `CsvExporter` | Writes the CSV files. |
| `ScenarioSetupUI`, `HudUI`, `ResultsDashboardUI` | The three screens. |

Each simulation step runs in this order: signals, spawning, every agent decides its target speed, every agent moves, the detector measures, finished agents are removed. Deciding and moving are separate passes so the result does not depend on the order agents are stored in.

The simulation advances in fixed 0.02 s steps and all random choices come from one seeded generator, so the same settings and seed produce the same events at any frame rate.

## Using your own models

The road users are assembled from primitive shapes. To use real models, assign prefabs to the **Scooter Prefab**, **Vehicle Prefab** and **Pedestrian Prefab** fields of the Simulation Bootstrap component. A prefab should face +Z with its origin on the ground at the centre of the body. Body sizes used by the simulation are constants in `RoadLayout`.

## How this differs from the earlier project spec

| Spec | This code | Reason |
|---|---|---|
| Agents steer with `NavMeshAgent` | The NavMesh plans each path; agents follow it with their own speed control | `NavMeshAgent` updates once per frame and steers around other agents. That makes results depend on frame rate and smooths away the close calls being measured. |
| A trigger sphere and physics callbacks detect encounters | The detector checks distances against the agent list every step | Kinematic bodies do not raise collision callbacks against each other, and a direct check is repeatable. |
| `StopZone` trigger volumes | The stop line is a distance along each route | Same behaviour with no physics involved. |
| One scooter | Several scooters, 4 by default | More data per run. Set the slider to 1 for a single scooter. |
| TTC is distance divided by closing speed | TTC is the time until the bodies touch on their current velocities, and it must persist for 0.3 s | The simple formula flags cars that pass in the next lane. |
| Scene and NavMesh built by hand | Both generated on Play from `RoadLayout` | Nothing to set up, and the picture cannot drift from the numbers. |
| Not in the spec | Conflict types, indicators, careful and careless drivers, events per kilometre, the run summary file | Added while building. |

## Problems found while testing, and what the code does about them

These came up when the simulation logic was run and are the reason for several rules in the code.

1. **One close call logged on every step it lasted.** Fixed by tracking encounters and classifying each once (`Encounter`, `NearMissDetector`).
2. **Every car in the next lane looked like a conflict.** Distance divided by closing speed is low for any overtaking car. Replaced with a time-to-collision that is infinite unless the two bodies would actually touch (`ConflictMath.TimeToCollision`).
3. **A turning car triggered a near-miss from 8 m away.** For an instant its velocity pointed at a scooter. The TTC rule now requires the low value to last 0.3 s.
4. **Riding past a queue at a red light counted as close passes.** The distance rule now needs the other road user to be moving, or to have stopped within the last second.
5. **Right-turn conflicts always ended in a collision.** The rider had no warning before the car's direction changed. Cars now indicate, and a rider can predict the path of a car that is indicating (`ConflictMath.PredictedTimeToCollision`). How early a driver indicates depends on how careful they are.
6. **Cars drove through pedestrians.** Drivers now give way if a pedestrian would reach their path before the car has passed, and pedestrians do not walk into a car that is in their way.
7. **Gridlock.** Four separate causes: two road users each waiting for the other beside their path, a car entering the intersection while cross traffic was still inside, a scooter creeping forever toward a stopped car, and a car and scooter left touching after a near-miss. Fixed by keeping extra clearance only from things that are moving, a do-not-block-the-box rule, and a gridlock breaker in `VehicleController` that lets a car edge through when everyone in a ring is waiting for the next.
8. **Turn conflicts vanished when pedestrians were added.** Not a bug: pedestrians with the walk signal hold right-turning cars at the start of each green, so the scooters clear the corner first. The Aggressive turns scenario uses few pedestrians for this reason.

## How this was checked, and what was not

The scripts were written without access to Unity or a C# compiler, so they have not been compiled or run in the Editor. What was done instead:

- Every script was parsed with a C# grammar. No syntax errors.
- Every reference between the project's own classes was checked for member names, argument counts and access level.
- Every Unity API member the scripts use was checked against Unity's published C# reference source for 2022.3, 6.0, 6.3 and 6.7.
- The simulation logic (everything except rendering, the NavMesh and the interface) was ported line for line to Python and run for 116 simulated runs: every ready-made scenario with six seeds, mixed high-risk settings, and 26 runs at the extremes of the sliders. In those runs no vehicle or scooter crossed a stop line on red, no pedestrian set to wait crossed against the signal, no car overlapped a pedestrian, and no run froze. The longest any road user stood still was 79 s, at a crossing crowded with pedestrians. Cars overlapped other cars 5 times in total, 3 of them in one run at the highest vehicle speed. The same seed gave the same events every time.

What that leaves untested: type errors only a compiler finds, how the scene and interface look, and whether the NavMesh areas bake as intended. If a path cannot be planned on the NavMesh the agent uses a straight line instead and a warning with the count appears in the Console when the run ends.

Averages per 3-minute run from the Python port, over six seeds, with 4 scooters:

| Scenario | Near-misses | Collisions | Mostly |
|---|---|---|---|
| Baseline | 4.7 | 0.2 | Close passes |
| Fast scooter | 9.2 | 0.7 | Close passes, with more pedestrian and turn events |
| Slow reaction | 4.2 | 0.5 | Close passes, with more collisions |
| Risky pedestrians | 9.8 | 0.2 | Close passes and 2.0 pedestrian near-misses |
| Close passing | 36.2 | 0.3 | Close passes |
| Aggressive turns | 7.2 | 0.3 | 3.0 turn near-misses |

Treat these as a guide to what to expect, not as results. The Unity version uses a different random generator, so its numbers for a given seed will differ, and six runs per scenario is a small sample: a difference of one or two events between two scenarios is within the noise. Reaction time mainly changes how conflicts end rather than how many there are. With risky pedestrians and careless drivers together, collisions per run went from 0.2 at a 0.3 s reaction time to 0.5 at 1.0 s and 1.2 at 2.0 s.

## If something goes wrong

| Symptom | What to do |
|---|---|
| Compile errors after copying the scripts | Copy the first error from the Console, with its file name and line number. It is usually a one-line fix. |
| `The type or namespace name 'UI' does not exist` | Install the **Unity UI** package from Window > Package Manager. |
| Buttons and sliders do not respond | In Edit > Project Settings > Player > Other Settings, set **Active Input Handling** to **Both** and let Unity restart. |
| Everything is pink | The render pipeline has no default material. Assign a pipeline asset in Project Settings > Graphics. |
| Agents do not move, or a Console warning mentions path legs | Note the numbers in the warning. The simulation still runs on straight-line paths. |
| The panels are cut off or the wrong size | Set the Game view to 16:9. To rescale the interface, change `referenceResolution` in `UIFactory.CreateCanvas`. |
