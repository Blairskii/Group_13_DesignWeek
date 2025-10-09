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

        static void Main()
        {
            Console.Title = "Alienation";
            Renderer.TryResizeConsole(120, 40);
            Console.CursorVisible = false;

            Audio.Init(AppDomain.CurrentDomain.BaseDirectory + "Assets_Audio");

            var world = BuildWorld();
            EnterRoom(world, 0, reset: true);

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
                        Renderer.ShowPopup(
                            "You slip through the final blast door...",
                            "The desert night greets you... you made it.",
                            "",
                            "Press R to retry... or any key to quit"
                        );
                        var k = Console.ReadKey(true).Key;
                        if (k == ConsoleKey.R)
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

        // =======================================================
        // MOVEMENT
        // =======================================================
        static void TryMove(World world, int dx, int dy)
        {
            var room = world.CurrentRoom;
            var (x, y) = world.PlayerPos;
            int nx = x + dx, ny = y + dy;

            // push shapes
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
                        Audio.PlayPushHeavy();
                    }

                    if (room.IsWalkable(world, nx, ny))
                        world.PlayerPos = (nx, ny);

                    if (world.CurrentRoomIndex == 0) EvaluateRoom1Plate(world);
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

        // =======================================================
        // INTERACTION
        // =======================================================
        static void TryInteract(World world)
        {
            var room = world.CurrentRoom;
            var pos = world.PlayerPos;

            static void Learn(World w, char ch, string label)
            {
                if (!w.DiscoveredLegend.ContainsKey(ch))
                    w.DiscoveredLegend[ch] = label;
            }

            // --- 1) Doors ---
            foreach (var dir in new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
            {
                var neighbor = (pos.x + dir.dx, pos.y + dir.dy);
                if (room.Doors.Contains(neighbor))
                {
                    Learn(world, 'D', "Door");
                    bool open = world.OpenDoors.Contains((room, neighbor));

                    if (!open && world.CurrentRoomIndex == 1 && world.HasKeyRoom2)
                    {
                        world.OpenDoors.Add((room, neighbor));
                        Renderer.ShowPopup("You unlock the door with the key...");
                        Audio.PlayDoorOpen();
                    }
                    else if (!open)
                    {
                        Renderer.ShowPopup("The door is locked...");
                        Audio.PlayDoorLocked();
                    }
                    else
                    {
                        Renderer.ShowPopup("The door is already open.");
                    }
                    return;
                }
            }

            // --- 2) Flavor glyphs ---
            if (room.Flavors.Contains(pos))
            {
                if (room.FlavorGlyphs.TryGetValue(pos, out var g))
                {
                    switch (g)
                    {
                        case 'C': Learn(world, 'C', "Concealment Cube"); Renderer.ShowPopup("A concealment cube... what primitive technology..."); Audio.PlayOnce("scrape"); break;
                        case 'h': Learn(world, 'h', "Sleeper"); Renderer.ShowPopup("A thin sleeping creature with a thousand tiny legs..."); Audio.PlayOnce("sweep"); break;
                        case '[':
                        case ']': if (!world.DiscoveredLegend.ContainsKey('[')) world.DiscoveredLegend['['] = "Metal maw"; Renderer.ShowPopup("A cube like beast with a large metal maw..."); Audio.PlayOnce("door"); break;
                        case 'Y': Learn(world, 'Y', "Green friend"); Renderer.ShowPopup("A small hairy green friend..."); Audio.PlayOnce("leaves"); break;
                        case '0': Learn(world, '0', "Portal"); Renderer.ShowPopup("A portal to another world..."); Audio.PlayOnce("portal"); break;
                        case 'O':
                        case 'F': Renderer.ShowPopup("A human artifact... symbols that match your species code..."); Audio.PlayOnce("button"); break;
                        default: Renderer.ShowPopup("Something unfamiliar... it hums softly..."); Audio.PlayOnce("button"); break;
                    }
                }
                else
                {
                    Renderer.ShowPopup("Something unfamiliar... it vibrates...");
                    Audio.PlayOnce("button");
                }
                return;
            }

            // --- 3) Searchables (room 2) ---
            if (world.CurrentRoomIndex == 1 && room.Searchables.Contains(pos))
            {
                Learn(world, 'T', "Secret Container");
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
                    Renderer.ShowPopup("Dust... notes... old coats... nothing useful...");
                    Audio.PlayOnce("button");
                }
                return;
            }

            // --- 4) Levers (room 3) ---
            if (world.CurrentRoomIndex == 2 && room.Levers.Contains(pos))
            {
                Learn(world, 'L', "Lever");

                if (world.PulledLevers.Contains(pos))
                {
                    Renderer.ShowPopup("The bone already remembered your touch...");
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
                        Renderer.ShowPopup("The bone stick hums... metal breath unlocks ahead...");
                        Audio.PlayDoorOpen();
                    }
                    else
                    {
                        Renderer.ShowPopup("A distant sigh... nothing left to open...");
                        Audio.PlayOnce("button");
                    }
                }
                else
                {
                    Renderer.ShowPopup("The bone stick yawns... nothing happens...");
                    Audio.PlayOnce("button");
                }
                return;
            }

            // --- 5) Plates (room 1) ---
            if (room.Plates.Contains(pos) && world.CurrentRoomIndex == 0)
            {
                Learn(world, '@', "Metal disc");
                Renderer.ShowPopup("A flat metallic disc protrudes from the floor, it bounces when touched...");
                Audio.PlayPlatePress();
                return;
            }

            // --- Default ---
            Renderer.ShowPopup("There is nothing to interact with here...");
            Audio.PlayOnce("button");
        }

        // =======================================================
        // ROOM FLOW + PLATE RULES
        // =======================================================
        static void TryAdvanceIfOnExit(World world)
        {
            var room = world.CurrentRoom;
            if (!room.ExitPos.HasValue) return;
            if (world.PlayerPos != room.ExitPos.Value) return;

            if (world.CurrentRoomIndex == 0)
            {
                foreach (var d in room.Doors)
                    if (!world.OpenDoors.Contains((room, d))) return;
                EnterRoom(world, 1, reset: true);
                return;
            }

            if (world.CurrentRoomIndex == 1)
            {
                foreach (var d in room.Doors)
                    if (!world.OpenDoors.Contains((room, d))) return;
                EnterRoom(world, 2, reset: true);
                return;
            }
        }

        static void EvaluateRoom1Plate(World world)
        {
            var room = world.Rooms[0];
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

            if (index == 0)
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            else if (index == 1)
            {
                world.HasKeyRoom2 = false;
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }
            else if (index == 2)
            {
                world.PulledLevers.Clear();
                world.NextDoorIndexRoom3 = 0;
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }

            Renderer.ShowPopup(
                room.Name,
                "",
                index == 0
                    ? "You are an escaped alien deep inside Area 51..."
                    : "..."
            );

            MarkDirty();
        }

        // =======================================================
        // LEVELS
        // =======================================================
        static World BuildWorld()
        {
            var world = new World();

            // Room 1
            var room1 = new Room(
                "Room 1... Storage Bay",
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

            // Room 2
            var room2 = new Room(
                "Room 2... Lab Ward",
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

            // Room 3
            var room3 = new Room(
                "Room 3... Lever Gauntlet",
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

            world.Rooms.Add(room1);
            world.Rooms.Add(room2);
            world.Rooms.Add(room3);
            world.CorrectShapeForPlate = ShapeId.Two;

            return world;
        }
    }
}
