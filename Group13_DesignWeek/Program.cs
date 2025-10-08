using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Group13_DesignWeek
{
    // game... main loop... rendering... rules
    static class Game
    {
        // redraw only when needed
        static bool _dirty = true;
        static void MarkDirty() => _dirty = true;

        static void Main()
        {
            Console.Title = "Alienation";
            Renderer.TryResizeConsole(120, 40);
            Console.CursorVisible = false;

            var world = BuildWorld();
            EnterRoom(world, 0, reset: true);

            bool quit = false;

            while (!quit)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    // retry any time
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

                    // win... last room step onto exit
                    bool lastRoom = world.CurrentRoomIndex == world.Rooms.Count - 1;
                    if (lastRoom && world.CurrentRoom.ExitPos.HasValue &&
                        world.PlayerPos == world.CurrentRoom.ExitPos.Value)
                    {
                        Console.Clear();
                        Console.WriteLine("You slip through the final blast door...");
                        Console.WriteLine("The desert night greets you... you made it.");
                        Console.WriteLine();
                        Console.WriteLine("Press any key to quit... or R to retry...");
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

        // movement... push shapes first... then walk
        static void TryMove(World world, int dx, int dy)
        {
            var room = world.CurrentRoom;
            var (x, y) = world.PlayerPos;
            int nx = x + dx, ny = y + dy;

            // push shape if present
            if (room.Shapes.ContainsKey((nx, ny)))
            {
                int sx = nx + dx, sy = ny + dy;
                if (room.IsEmptyForShape(world, sx, sy))
                {
                    var sid = room.Shapes[(nx, ny)];
                    room.Shapes.Remove((nx, ny));
                    room.Shapes[(sx, sy)] = sid;

                    if (room.IsWalkable(world, nx, ny))
                        world.PlayerPos = (nx, ny);

                    if (world.CurrentRoomIndex == 0) EvaluateRoom1Plate(world);
                    TryAdvanceIfOnExit(world);
                }
                return;
            }

            // walk
            if (room.IsWalkable(world, nx, ny))
            {
                world.PlayerPos = (nx, ny);
                TryAdvanceIfOnExit(world);
            }
        }

        // interact... flavor... items... levers... doors
        static void TryInteract(World world)
        {
            var room = world.CurrentRoom;
            var pos = world.PlayerPos;

            // helper to learn legend entries once
            static void Learn(World w, char ch, string label)
            {
                if (!w.DiscoveredLegend.ContainsKey(ch))
                    w.DiscoveredLegend[ch] = label;
            }

            // 1) doors from adjacent cells
            foreach (var dir in new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
            {
                var neighbor = (pos.x + dir.dx, pos.y + dir.dy);
                if (room.Doors.Contains(neighbor))
                {
                    Learn(world, 'D', "Door");

                    bool open = world.OpenDoors.Contains((room, neighbor));
                    Console.WriteLine();
                    if (!open && world.CurrentRoomIndex == 1 && world.HasKeyRoom2)
                    {
                        world.OpenDoors.Add((room, neighbor));
                        Console.WriteLine("You unlock the door with the key...");
                        PauseBrief();
                    }
                    else if (!open)
                    {
                        Console.WriteLine("The door is locked...");
                        PauseBrief();
                    }
                    else
                    {
                        Console.WriteLine("The door is already open.");
                        PauseBrief();
                    }
                    return;
                }
            }

            // 2) flavor... narrative objects with glyph specific text
            if (room.Flavors.Contains(pos))
            {
                Learn(world, 'O', "Flavor");
                Console.WriteLine();

                if (room.FlavorGlyphs.TryGetValue(pos, out var g))
                {
                    switch (g)
                    {
                        case 'h':
                            Console.WriteLine("A thin sleeping creature with a thousand tiny legs...");
                            break;
                        case '[':
                        case ']':
                            Console.WriteLine("A cube like beast with a large metal maw...");
                            break;
                        case 'Y':
                            Console.WriteLine("A small hairy green friend...");
                            break;
                        case '0':
                            Console.WriteLine("A portal to another world...");
                            break;
                        case 'O':
                        case 'F':
                            Console.WriteLine("A human artifact... symbols that match your species code...");
                            break;
                        default:
                            Console.WriteLine("Something unfamiliar... it hums softly...");
                            break;
                    }
                }
                else
                {
                    Console.WriteLine("Something unfamiliar... it hums softly...");
                }

                PauseBrief();
                return;
            }

            // 3) room 2 searchables... one contains the key
            if (world.CurrentRoomIndex == 1 && room.Searchables.Contains(pos))
            {
                Learn(world, 'T', "Interact");

                var correct = world.FixedKeyItemPos ??
                              room.Searchables.OrderBy(p => Math.Abs(p.x - room.Width / 2)
                                                         + Math.Abs(p.y - room.Height / 2)).First();

                Console.WriteLine();
                if (!world.HasKeyRoom2 && pos == correct)
                {
                    world.HasKeyRoom2 = true;
                    Console.WriteLine("Inside the locker... a brass key glints. You take it...");
                    PauseBrief();
                }
                else
                {
                    Console.WriteLine("Dust... notes... old coats... nothing useful...");
                    PauseBrief();
                }
                return;
            }

            // 4) room 3 levers... correct ones are those with adjacent C in source... open next door
            if (world.CurrentRoomIndex == 2 && room.Levers.Contains(pos))
            {
                Learn(world, 'T', "Interact");

                if (world.PulledLevers.Contains(pos))
                {
                    Console.WriteLine();
                    Console.WriteLine("The bone already remembered your touch...");
                    PauseBrief();
                    return;
                }

                world.PulledLevers.Add(pos);

                Console.WriteLine();
                if (room.CorrectLevers.Contains(pos))
                {
                    // pick next unopened door in left to right then top to bottom order
                    var orderedDoors = room.Doors.OrderBy(d => d.x).ThenBy(d => d.y).ToList();

                    // advance index to first still closed door
                    while (world.NextDoorIndexRoom3 < orderedDoors.Count &&
                           world.OpenDoors.Contains((room, orderedDoors[world.NextDoorIndexRoom3])))
                    {
                        world.NextDoorIndexRoom3++;
                    }

                    if (world.NextDoorIndexRoom3 < orderedDoors.Count)
                    {
                        var doorToOpen = orderedDoors[world.NextDoorIndexRoom3];
                        world.OpenDoors.Add((room, doorToOpen));
                        world.NextDoorIndexRoom3++;
                        Console.WriteLine("The bone stick hums... metal breath unlocks ahead...");
                    }
                    else
                    {
                        Console.WriteLine("A distant sigh... nothing left to open...");
                    }
                }
                else
                {
                    Console.WriteLine("The bone stick yawns... nothing happens...");
                }
                PauseBrief();
                return;
            }

            // 5) hint on plate in room 1
            if (room.Plates.Contains(pos) && world.CurrentRoomIndex == 0)
            {
                Learn(world, '*', "Plate");
                Console.WriteLine();
                Console.WriteLine("A heavy pressure plate... perhaps the right shape will trigger it...");
                PauseBrief();
                return;
            }

            // 6) stepping on exit... try to advance
            if (room.ExitPos.HasValue && pos == room.ExitPos.Value)
            {
                Learn(world, 'E', "Exit");
                TryAdvanceIfOnExit(world);
                return;
            }

            // default
            Console.WriteLine();
            Console.WriteLine("There is nothing to interact with here...");
            PauseBrief();
        }

        // room flow... advance when conditions are met
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

        // room 1 plate rule... correct shape on any plate opens the door
        // room 1 rule... door opens only when a BARREL sits on any @ plate
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


        // enter a room... reset per room state
        static void EnterRoom(World world, int index, bool reset)
        {
            world.CurrentRoomIndex = index;
            var room = world.CurrentRoom;

            if (reset) room.Reset();
            world.PlayerPos = room.PlayerStart;

            if (index == 0)
            {
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }
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

            Console.SetCursorPosition(0, 0);
            Console.WriteLine(room.Name);
            if (index == 0)
            {
                Console.WriteLine("You are an escaped alien deep inside Area 51...");
                Console.WriteLine("Move with WASD... interact with E... retry with R... quit with Q...");
                Console.WriteLine("Push the right shape onto the plate to open the door...");
            }
            PauseBrief();
            MarkDirty();
        }

        static void PauseBrief() => Thread.Sleep(120);

        // maps
        static World BuildWorld()
        {
            var world = new World();

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

            // teammate lever puzzle... '=' are walls... '[' and ']' and 'h' and 'Y' and '0' are flavor
            var room3 = new Room(
                "Room 3... Lever Gauntlet",
                new[]
                {
                    "#====================================================================================#",
                    "#h......L..0....L......D...==..h...==......==[][]==[][]...............D............E#",
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
