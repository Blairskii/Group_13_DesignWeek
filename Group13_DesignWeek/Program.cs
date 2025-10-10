using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Group13_DesignWeek
{
    static class Game
    {
        static bool _dirty = true;
        static void MarkDirty() => _dirty = true;

        // Set these to your PNGs (or leave null to skip images and just show text boxes)
        // e.g., @"C:\Path\To\ALIEN.png"
        static readonly string TitleImagePath = null;
        static readonly string EndImagePath = null;

        static void Main()
        {
            Console.Title = "Alienation";
            Renderer.TryResizeConsole(120, 40);
            Console.CursorVisible = false;

            // Audio root (already working on your side)
            Audio.Init(AppDomain.CurrentDomain.BaseDirectory + "Assets_Audio");

            // --- TITLE CARD ---
            Renderer.ShowAsciiCard(
                TitleImagePath,
                "ALIENATION",
                "Press any key to begin",
                "(WASD move · E interact · X restart room · R restart run · Q quit)"
            );

            var world = BuildWorld();
            EnterRoom(world, 0, reset: true); // start in Training Wing

            bool quit = false;

            while (!quit)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    if (key == ConsoleKey.R)
                    {
                        world = BuildWorld();
                        EnterRoom(world, 0, reset: true);
                        MarkDirty();
                        continue;
                    }

                    if (key == ConsoleKey.X)
                    {
                        // local restart: reset current room only
                        EnterRoom(world, world.CurrentRoomIndex, reset: true);
                        MarkDirty();
                        continue;
                    }

                    switch (key)
                    {
                        case ConsoleKey.W: TryMove(world, 0, -1); MarkDirty(); break;
                        case ConsoleKey.A: TryMove(world, -1, 0); MarkDirty(); break;
                        case ConsoleKey.S: TryMove(world, 0, 1); MarkDirty(); break;
                        case ConsoleKey.D: TryMove(world, 1, 0); MarkDirty(); break;
                        case ConsoleKey.E: TryInteract(world); MarkDirty(); break;
                        case ConsoleKey.Q: quit = true; break;
                    }

                    bool lastRoom = world.CurrentRoomIndex == world.Rooms.Count - 1;
                    if (lastRoom && world.CurrentRoom.ExitPos.HasValue &&
                        world.PlayerPos == world.CurrentRoom.ExitPos.Value)
                    {
                        // --- END CARD ---
                        Renderer.ShowAsciiCard(
                            EndImagePath,
                            "You slip through the final blast door...",
                            "The desert night greets you... you made it.",
                            "Press R to retry · any key to quit"
                        );

                        // allow retry on end card
                        if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.R)
                        {
                            world = BuildWorld();
                            EnterRoom(world, 0, reset: true);
                            MarkDirty();
                            continue;
                        }
                        break;
                    }
                }

                if (_dirty)
                {
                    Renderer.Render(world);
                    _dirty = false;
                }

                Thread.Sleep(1);
            }
        }

        // ===================== MOVEMENT =====================
        static void TryMove(World world, int dx, int dy)
        {
            var room = world.CurrentRoom;
            var (x, y) = world.PlayerPos;
            int nx = x + dx, ny = y + dy;

            // push shape
            if (room.Shapes.ContainsKey((nx, ny)))
            {
                int sx = nx + dx, sy = ny + dy;
                if (room.IsEmptyForShape(world, sx, sy))
                {
                    var sid = room.Shapes[(nx, ny)];
                    room.Shapes.Remove((nx, ny));
                    room.Shapes[(sx, sy)] = sid;

                    if (sid == ShapeId.Barrel)
                    {
                        if (!world.DiscoveredLegend.ContainsKey('&'))
                            world.DiscoveredLegend['&'] = "Heavy cylinder";
                        
                    }

                    if (room.IsWalkable(world, nx, ny))
                        world.PlayerPos = (nx, ny);

                    EvaluateBarrelPlateForCurrentRoom(world);
                    TryAdvanceIfOnExit(world);
                }
                return;
            }

            if (room.IsWalkable(world, nx, ny))
            {
                world.PlayerPos = (nx, ny);
                TryAdvanceIfOnExit(world);
            }
        }

        // ===================== INTERACT =====================
        static void TryInteract(World world)
        {
            var room = world.CurrentRoom;
            var pos = world.PlayerPos;

            static void Learn(World w, char ch, string label)
            {
                if (!w.DiscoveredLegend.ContainsKey(ch))
                    w.DiscoveredLegend[ch] = label;
            }

            // 1) Doors (adjacent)
            foreach (var dir in new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
            {
                var neighbor = (pos.x + dir.dx, pos.y + dir.dy);
                if (room.Doors.Contains(neighbor))
                {
                    Learn(world, 'D', "Metal Mouth");
                    bool open = world.OpenDoors.Contains((room, neighbor));

                    if (!open && world.CurrentRoomIndex == 2 && world.HasKeyRoom2)
                    {
                        world.OpenDoors.Add((room, neighbor));
                        Renderer.ShowPopup("The metal Mouth Opens Combined with the shiny fang...");
                        Audio.PlayDoorOpen();
                    }
                    else if (!open && world.CurrentRoomIndex == 2)
                    {
                        Renderer.ShowPopup("The mouth's lips are sealed...");
                        Audio.PlayDoorLocked();
                    }
                    else if (!open && world.CurrentRoomIndex != 2)
                    {
                        world.OpenDoors.Add((room, neighbor));
                        Renderer.ShowPopup("A metal mouth with sealed lips.");
                        Audio.PlayDoorOpen();
                    }
                    else
                    {
                        Renderer.ShowPopup("The Mouth is already Open.");
                    }
                    return;
                }
            }

            // 2) Flavor glyphs (on cell)
            if (room.Flavors.Contains(pos))
            {
                if (room.FlavorGlyphs.TryGetValue(pos, out var g))
                {
                    switch (g)
                    {
                        case 'C': Learn(world, 'C', "Concealment Cube"); Renderer.ShowPopup("A concealment cube... what primitive technology..."); Audio.PlayOnce("scrape"); break;
                        case 'h': Learn(world, 'h', "Sleeper"); Renderer.ShowPopup("A thin sleeping creature with a thousand tiny legs..."); Audio.PlayOnce("sweep"); break;
                        case '[':
                        case ']':
                            if (!world.DiscoveredLegend.ContainsKey('[')) world.DiscoveredLegend['['] = "Metal maw";
                            Renderer.ShowPopup("A Portal to another Room...");
                            Audio.PlayOnce("door");
                            break;
                        case 'Y': Learn(world, 'Y', "Green friend"); Renderer.ShowPopup("A small hairy green friend..."); Audio.PlayOnce("leaves"); break;
                        case '0': Learn(world, '0', "Donaught"); Renderer.ShowPopup("Doughnaut...Home from space"); Audio.PlayOnce("portal"); break;
                        case 'O':
                        case 'F': Renderer.ShowPopup("A human artifact... symbols that match your species code..."); Audio.PlayOnce("Lever"); break;
                        default: Renderer.ShowPopup("Something unfamiliar... it hums softly..."); Audio.PlayOnce("Portal"); break;
                    }
                }
                else
                {
                    Renderer.ShowPopup("Something unfamiliar... it vibrates...");
                    Audio.PlayOnce("button");
                }
                return;
            }

            // 3) Searchables (T)
            if (room.Searchables.Contains(pos))
            {
                Learn(world, 'T', "Secret Container");

                if (world.CurrentRoomIndex == 2)
                {
                    var correct = world.FixedKeyItemPos ??
                                  room.Searchables.OrderBy(p => Math.Abs(p.x - room.Width / 2)
                                                             + Math.Abs(p.y - room.Height / 2)).First();

                    if (!world.HasKeyRoom2 && pos == correct)
                    {
                        world.HasKeyRoom2 = true;
                        Renderer.ShowPopup("Inside the secret box... a shiny fang. You take it...");
                        Audio.PlayFoundKey();
                    }
                    else
                    {
                        Renderer.ShowPopup("What looks to be Human Belongings... how archacaic...");
                        Audio.PlayOnce("button");
                    }
                }
                else
                {
                    Renderer.ShowPopup("A container full of secrets...");
                    Audio.PlayLockerOpen();
                }
                return;
            }

            // 4) Levers (L)
            if (room.Levers.Contains(pos))
            {
                Learn(world, 'L', "Bone stick");

                if (world.CurrentRoomIndex == 3)
                {
                    if (world.PulledLevers.Contains(pos))
                    {
                        Renderer.ShowPopup("The Bone is stuck...");
                        Audio.PlayOnce("button");
                        return;
                    }

                    world.PulledLevers.Add(pos);

                    if (room.CorrectLevers.Contains(pos))
                    {
                        var orderedDoors = room.Doors.OrderBy(d => d.x).ThenBy(d => d.y).ToList();
                        while (world.NextDoorIndexRoom3 < orderedDoors.Count &&
                               world.OpenDoors.Contains((room, orderedDoors[world.NextDoorIndexRoom3])))
                            world.NextDoorIndexRoom3++;

                        if (world.NextDoorIndexRoom3 < orderedDoors.Count)
                        {
                            var doorToOpen = orderedDoors[world.NextDoorIndexRoom3];
                            world.OpenDoors.Add((room, doorToOpen));
                            world.NextDoorIndexRoom3++;
                            Renderer.ShowPopup("The bone stick hums... metal mouth screams briefly...");
                            Audio.PlayDoorOpen();
                        }
                        else
                        {
                            Renderer.ShowPopup("Blarp...");
                            Audio.PlayOnce("button");
                        }
                    }
                    else
                    {
                        Renderer.ShowPopup("The bone stick yawns... nothing happens...");
                        Audio.PlayOnce("button");
                    }
                }
                else
                {
                    Renderer.ShowPopup("You pull the Bone stick. metal mouth screams from afar...");
                    Audio.PlayOnce("button");
                }
                return;
            }

            // 5) Plates (@)
            if (room.Plates.Contains(pos))
            {
                if (!world.DiscoveredLegend.ContainsKey('@'))
                    world.DiscoveredLegend['@'] = "Frisbee";

                Renderer.ShowPopup("A frisbee! lowers when i step on it...");
                Audio.PlayPlatePress();
                return;
            }

            // 6) Exit
            if (room.ExitPos.HasValue && pos == room.ExitPos.Value)
            {
                if (!world.DiscoveredLegend.ContainsKey('E'))
                    world.DiscoveredLegend['E'] = "Outflow";
                TryAdvanceIfOnExit(world);
                return;
            }

            Renderer.ShowPopup("I dont know what this is...");
            Audio.PlayOnce("button");
        }

        // ===================== FLOW + RULES =====================
        static void TryAdvanceIfOnExit(World world)
        {
            var room = world.CurrentRoom;
            if (!room.ExitPos.HasValue) return;
            if (world.PlayerPos != room.ExitPos.Value) return;

            if (world.CurrentRoomIndex < world.Rooms.Count - 1)
            {
                EnterRoom(world, world.CurrentRoomIndex + 1, reset: true);
                return;
            }
        }

        static void EvaluateBarrelPlateForCurrentRoom(World world)
        {
            var room = world.CurrentRoom;
            bool barrelOnPlate = room.Plates.Any(plate =>
                room.Shapes.TryGetValue(plate, out var sid) && sid == ShapeId.Barrel);

            foreach (var d in room.Doors)
            {
                if (barrelOnPlate) world.OpenDoors.Add((room, d));
                else world.OpenDoors.Remove((room, d));
            }
        }

        static void EnterRoom(World world, int index, bool reset)
        {
            world.CurrentRoomIndex = index;
            var room = world.CurrentRoom;

            if (reset) room.Reset();
            world.PlayerPos = room.PlayerStart;

            if (index == 1)
            {
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }
            else if (index == 2)
            {
                world.HasKeyRoom2 = false;
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }
            else if (index == 3)
            {
                world.PulledLevers.Clear();
                world.NextDoorIndexRoom3 = 0;
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }
            else
            {
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }

            // Show a small centered card per room
            Renderer.ShowPopup(
                room.Name,
                "",
                (index == 0) ? "You smash out of your containment tank... into a room full of objects unkown to you..." : "..."
            );

            MarkDirty();
        }

        // ===================== LEVELS =====================
        static World BuildWorld()
        {
            var world = new World();

            var room0 = new Room(
                "Stage 1... Overwhelmed",
                new[]
                {
                    "######################################################################################",
                    "#P..&....@..C..h..[.]..Y..0..O..F..L..T.................D.................E.........#",
                    "#....................................................................................#",
                    "#...............................................@....................................#",
                    "#...............................&....................................................#",
                    "#............C...........h...........[.]...........Y...........0...........T........#",
                    "#....................................................................................#",
                    "#.............................................................L......................#",
                    "#.................................................D..................................#",
                    "######################################################################################",
                });

            var room1 = new Room(
                "Stage 2... Fear",
                new[]
                {
                   "#########################################DD###########################################",
                    "#................C.......C...#....#........................#...#.....#################",
                    "#................C...........#....#........................#.........#............@..#",
                    "#....#..#....................#....#..................#######...#.....###...####...#..#",
                    "#....#..#######......#............#............................#.....#............####",
                    "#....#......&........#.......#....#..................#######...#.....#...............#",
                    "#....##########......#.......#....#........................#...#.....##...############",
                    "#....................#.......#.............................#...#.....................#",
                    "#..C.C...............#.......#.C..#......P.................#...#.....................#",
                    "#########################################DD###########################################",
                    "######################################        ########################################",
                    "######################################   E    #######################################",
                    "#####################################################################################",
                });

            var room2 = new Room(
                "Stage 3 ...Confusion",
                new[]
                {
                    "######################################################################################",
                    "#P.......#.....T.#.T.......#....................#.T.............#.............D...E#",
                    "#........#...########...####.............################.......#.............######",
                    "#........................................#..T......................................#",
                    "##...##....#########.....###########...###............#.......##########...........#",
                    "#.....#####......T.#........#.........................#.......#....................#",
                    "#.........#...######.....####............##############...#####.T..................#",
                    "#.......T.#..............#.T..............................#........................#",
                    "######################################################################################",
                });

            var room3 = new Room(
                "Room Stage 4... Freedom",
                new[]
                {
                    "#====================================================================================#",
                    "#h......L..0....L......D...==..h...==......==[][]==[][]................D...........E#",
                    "#....................CL==..==........==....CL==...............======...L==...........#",
                    "#.........=====........==..==........==......==========h......==........==...........#",
                    "#L........=====........==..============......D.......==.......==......CL==...........#",
                    "#.........=====........==....................======..==[]...[]==........==...........#",
                    "#......................================....CL==..==..===========..==...L==Y..........#",
                    "#P............L........G==============Y.......==..==...............==....==...........#",
                    "#====================================================================================#",
                });

            world.Rooms.Add(room0);
            world.Rooms.Add(room1);
            world.Rooms.Add(room2);
            world.Rooms.Add(room3);

            world.CorrectShapeForPlate = ShapeId.Two;
            return world;
        }
    }
}
