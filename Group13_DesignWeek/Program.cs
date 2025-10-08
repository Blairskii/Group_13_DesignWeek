using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Group13_DesignWeek
{
    // enums... types for tiles and room-1 shapes
    enum Tile { Empty, Wall, Exit, Door, Plate, Flavor, Interactable }
    enum ShapeId { One = 1, Two = 2, Three = 3 }

    // room... parses ASCII into a grid and tracks objects
    class Room
    {
        public string Name = "";
        public int Width;
        public int Height;

        public Tile[,] Tiles;
        public (int x, int y) PlayerStart;

        // dynamic collections
        public Dictionary<(int x, int y), ShapeId> Shapes = new();   // room 1 pushables 1..3
        public HashSet<(int x, int y)> Plates = new();               // pressure plates
        public HashSet<(int x, int y)> Doors = new();                // doors
        public (int x, int y)? ExitPos;                              // exit tile

        public HashSet<(int x, int y)> Searchables = new();          // room 2 items... T or I
        public HashSet<(int x, int y)> Levers = new();               // room 3 levers... L

        public HashSet<(int x, int y)> Flavors = new();              // flavor interactables
        public Dictionary<(int x, int y), char> FlavorGlyphs = new();// which glyph was placed

        public HashSet<(int x, int y)> CorrectLevers = new();        // levers with adjacent 'C' in source
        public string[] RawMap;

        public Room(string name, string[] rows)
        {
            Name = name;
            RawMap = rows;
            Height = rows.Length;
            Width = rows.Max(r => r.Length);
            Tiles = new Tile[Width, Height];
            ParseRows(rows);
        }

        // turn ASCII into tiles and tag dynamic objects... tolerant to ragged rows
        void ParseRows(string[] rows)
        {
            Shapes.Clear(); Plates.Clear(); Doors.Clear();
            Searchables.Clear(); Levers.Clear(); Flavors.Clear();
            FlavorGlyphs.Clear(); CorrectLevers.Clear();
            ExitPos = null;

            for (int y = 0; y < Height; y++)
            {
                string row = rows[y]; // do not trim... spaces may be meaningful
                for (int x = 0; x < Width; x++)
                {
                    char c = (x < row.Length) ? row[x] : '#'; // pad short rows as walls

                    switch (c)
                    {
                        case '#': Tiles[x, y] = Tile.Wall; break;
                        case '=': Tiles[x, y] = Tile.Wall; break;            // alt wall
                        case '.': Tiles[x, y] = Tile.Empty; break;

                        case 'P': Tiles[x, y] = Tile.Empty; PlayerStart = (x, y); break;
                        case 'E': Tiles[x, y] = Tile.Exit; ExitPos = (x, y); break;
                        case 'D': Tiles[x, y] = Tile.Door; Doors.Add((x, y)); break;
                        case '*': Tiles[x, y] = Tile.Plate; Plates.Add((x, y)); break;

                        // design marker for levers... treated as floor
                        case 'C': Tiles[x, y] = Tile.Empty; break;

                        // flavor glyphs... all interactables with custom text
                        case 'O': case 'F': case 'h': case 'Y': case '0': case '[': case ']':
                            Tiles[x, y] = Tile.Flavor; Flavors.Add((x, y)); FlavorGlyphs[(x, y)] = c; break;

                        // shapes
                        case '1': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.One; break;
                        case '2': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.Two; break;
                        case '3': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.Three; break;

                        // items and generic interactables
                        case 'I': case 'T': Tiles[x, y] = Tile.Interactable; Searchables.Add((x, y)); break;

                        // levers use L
                        case 'L': Tiles[x, y] = Tile.Interactable; Levers.Add((x, y)); break;

                        default: Tiles[x, y] = Tile.Empty; break;
                    }
                }
            }

            // mark levers that have a design marker 'C' adjacent in the source ASCII
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!Levers.Contains((x, y))) continue;

                    bool hasC =
                        (x > 0               && x - 1 < RawMap[y].Length && RawMap[y][x - 1] == 'C') ||
                        (x + 1 < RawMap[y].Length && RawMap[y][x + 1] == 'C') ||
                        (y > 0               && x < RawMap[y - 1].Length && RawMap[y - 1][x] == 'C') ||
                        (y + 1 < RawMap.Length && x < RawMap[y + 1].Length && RawMap[y + 1][x] == 'C');

                    if (hasC) CorrectLevers.Add((x, y));
                }
            }
        }

        public void Reset() => ParseRows(RawMap);

        // can the player walk into x...y
        public bool IsWalkable(World world, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            if (Tiles[x, y] == Tile.Wall) return false;
            if (Doors.Contains((x, y)) && !world.OpenDoors.Contains((this, (x, y)))) return false;
            if (Shapes.ContainsKey((x, y))) return false; // shapes are solid
            return true;
        }

        // can a shape be pushed into x...y
        public bool IsEmptyForShape(World world, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            if (Tiles[x, y] == Tile.Wall) return false;
            if (Doors.Contains((x, y)) && !world.OpenDoors.Contains((this, (x, y)))) return false;
            if (Shapes.ContainsKey((x, y))) return false;
            return true;
        }

        // what glyph do we render at x...y
        public char RenderAt(World world, int x, int y, (int x, int y) playerPos)
        {
            if (playerPos == (x, y)) return 'P';

            if (Shapes.TryGetValue((x, y), out var sid))
                return sid switch { ShapeId.One => '1', ShapeId.Two => '2', ShapeId.Three => '3', _ => 'S' };

            if (Doors.Contains((x, y)))
                return world.OpenDoors.Contains((this, (x, y))) ? '/' : 'D';

            return Tiles[x, y] switch
            {
                Tile.Empty => '.',
                Tile.Wall => '#',
                Tile.Exit => 'E',
                Tile.Plate => '*',
                Tile.Flavor => FlavorGlyphs.TryGetValue((x, y), out var g) ? g : 'O',
                Tile.Interactable => 'T',
                _ => '.'
            };
        }
    }

    // world... cross room state
    class World
    {
        public List<Room> Rooms = new();
        public int CurrentRoomIndex = 0;
        public Room CurrentRoom => Rooms[CurrentRoomIndex];

        public (int x, int y) PlayerPos;

        public HashSet<(Room room, (int x, int y) door)> OpenDoors = new();

        // room 1
        public ShapeId CorrectShapeForPlate = ShapeId.Two;

        // room 2
        public bool HasKeyRoom2 = false;
        public (int x, int y)? FixedKeyItemPos = null;

        // room 3... lever flow
        public HashSet<(int x, int y)> PulledLevers = new();
        public int NextDoorIndexRoom3 = 0; // which door to open next

        // progressive legend
        public Dictionary<char, string> DiscoveredLegend = new();
    }

    // game... main loop... rendering... rules
    static class Game
    {
        // redraw only when needed
        static bool _dirty = true;
        static void MarkDirty() => _dirty = true;

        // clear leftovers
        static int _lastFrameWidth = 0;
        static int _lastFrameHeight = 0;

        static void Main()
        {
            Console.Title = "Alienation";
            TryResizeConsole(120, 40);
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
                        case ConsoleKey.E: TryInteract(world);     MarkDirty(); break;
                        case ConsoleKey.Q: quit = true;            break;
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
                    Render(world);
                    _dirty = false;
                }

                Thread.Sleep(1);
            }
        }

        static void TryResizeConsole(int width, int height)
        {
            try
            {
                Console.SetWindowSize(Math.Min(width, Console.LargestWindowWidth),
                                      Math.Min(height, Console.LargestWindowHeight));
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
            }
            catch { }
        }

        // render... centered map with progressive legend... fixed width lines
        static void Render(World world)
        {
            var room = world.CurrentRoom;

            var legendList = new List<string>();
            legendList.Add("LEGEND");
            foreach (var kv in world.DiscoveredLegend.OrderBy(k => k.Key))
                legendList.Add($"{kv.Key}  {kv.Value}");
            legendList.Add("");
            legendList.Add("Controls");
            legendList.Add("WASD Move");
            legendList.Add("E    Interact");
            legendList.Add("R    Retry");
            legendList.Add("Q    Quit");

            string[] legend = legendList.ToArray();

            int legendPadding = 3;
            int legendWidth = legend.Length == 0 ? 0 : legend.Max(s => s.Length);
            int totalWidth = room.Width + legendPadding + legendWidth;
            int leftMargin = Math.Max(0, (Console.WindowWidth - totalWidth) / 2);

            var sb = new StringBuilder(room.Height * (room.Width + legendWidth + leftMargin + 8));

            // title
            string title = room.Name;
            sb.Append(' ', leftMargin);
            sb.AppendLine(title.PadRight(totalWidth));

            // map rows with fixed width and legend lines
            for (int y = 0; y < room.Height; y++)
            {
                var chars = new char[room.Width];
                for (int x = 0; x < room.Width; x++)
                    chars[x] = room.RenderAt(world, x, y, world.PlayerPos);
                string mapRow = new string(chars);

                string legendRow = (y < legend.Length) ? legend[y] : string.Empty;

                string line = mapRow + new string(' ', legendPadding) + legendRow;
                if (line.Length < totalWidth) line = line.PadRight(totalWidth);

                sb.Append(' ', leftMargin);
                sb.AppendLine(line);
            }

            // status
            string status = (world.CurrentRoomIndex == 1)
                ? $"Room... {room.Name}    Key... {(world.HasKeyRoom2 ? "yes" : "no")}"
                : $"Room... {room.Name}";
            sb.Append(' ', leftMargin);
            sb.AppendLine(status.PadRight(totalWidth));

            Console.SetCursorPosition(0, 0);
            Console.Write(sb.ToString());

            // clear extra lines from previous frame if any
            int currentFrameHeight = 1 + room.Height + 1;
            if (_lastFrameHeight > currentFrameHeight)
            {
                for (int i = 0; i < _lastFrameHeight - currentFrameHeight; i++)
                {
                    Console.Write(new string(' ', leftMargin + Math.Max(_lastFrameWidth, totalWidth)));
                    Console.WriteLine();
                }
            }

            _lastFrameWidth = Math.Max(_lastFrameWidth, totalWidth);
            _lastFrameHeight = currentFrameHeight;
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
                        case '[': case ']':
                            Console.WriteLine("A cube like beast with a large metal maw...");
                            break;
                        case 'Y':
                            Console.WriteLine("A small hairy green friend...");
                            break;
                        case '0':
                            Console.WriteLine("A portal to another world...");
                            break;
                        case 'O': case 'F':
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
        static void EvaluateRoom1Plate(World world)
        {
            var room = world.Rooms[0];
            bool correctOnPlate = room.Plates.Any(plate =>
                room.Shapes.TryGetValue(plate, out var sid) && sid == world.CorrectShapeForPlate);

            foreach (var d in room.Doors)
            {
                if (correctOnPlate) world.OpenDoors.Add((room, d));
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
                    "######################################################################################",
                    "#P...............O..................1.......................................#....E..#",
                    "#.......................##############......................................#.......#",
                    "#.......................#..........#..............*.........................D.......#",
                    "#.....O.................#..........#........................................#########",
                    "#.......................#..........#................................................#",
                    "#.......................##############..............................................#",
                    "#.........2...........................................3.............................#",
                    "#.................................................................O.................#",
                    "######################################################################################",
                });

            var room2 = new Room(
                "Room 2... Lab Ward",
                new[]
                {
                    "######################################################################################",
                    "#P....T..........T.............T...........T..............T...................D...E#",
                    "#........##############..................##############.......................######",
                    "#........#............#..................#............#............................#",
                    "#....T...#............#..................#............#..........T.................#",
                    "#........#............#..................#............#............................#",
                    "#........##############..................##############............................#",
                    "#......................T..........................................................#",
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
