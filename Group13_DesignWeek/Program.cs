using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Group13_DesignWeek
{

    // ENUMS... types for tiles and room-1 shapes

    enum Tile { Empty, Wall, Exit, Door, Plate, Flavor, Interactable }
    enum ShapeId { One = 1, Two = 2, Three = 3 }


    // ROOM... parses ASCII into a grid and tracks objects

    class Room
    {
        public string Name = "";
        public int Width;
        public int Height;

        // Tile grid for static stuff like walls, floors, exits
        public Tile[,] Tiles;

        // Where the player spawns in the room
        public (int x, int y) PlayerStart;

        // Dynamic Characters... shapes, plates, doors, exit, items, levers, flavors
        public Dictionary<(int x, int y), ShapeId> Shapes = new();   // Room 1 pushables 1..3
        public HashSet<(int x, int y)> Plates = new();               // Pressure plates
        public HashSet<(int x, int y)> Doors = new();                // Closed or open via World.OpenDoors
        public (int x, int y)? ExitPos;                              // Exit tile

        public HashSet<(int x, int y)> Searchables = new();          // Room 2 items... T or I
        public HashSet<(int x, int y)> Levers = new();               // Room 3 levers... L
        public HashSet<(int x, int y)> Flavors = new();              // Flavor objects... O or F

        // Keep original ASCII for resets
        public string[] RawMap;

        public Room(string name, string[] rows)
        {
            Name = name;
            RawMap = rows;
            Height = rows.Length;

            // Use the longest row as width... shorter lines are padded later
            Width = rows.Max(r => r.Length);
            Tiles = new Tile[Width, Height];

            ParseRows(rows);
        }

        // Turn ASCII into tiles and tag dynamic objects... tolerant to ragged rows
        void ParseRows(string[] rows)
        {
            Shapes.Clear(); Plates.Clear(); Doors.Clear();
            Searchables.Clear(); Levers.Clear(); Flavors.Clear();
            ExitPos = null;

            for (int y = 0; y < Height; y++)
            {
                string row = rows[y]; // do not trim... spaces may be meaningful
                for (int x = 0; x < Width; x++)
                {
                    // If the row is shorter than Width, treat beyond-end as walls
                    char c = (x < row.Length) ? row[x] : '#';

                    switch (c)
                    {
                        case '#': Tiles[x, y] = Tile.Wall; break;
                        case '.': Tiles[x, y] = Tile.Empty; break;

                        case 'P': Tiles[x, y] = Tile.Empty; PlayerStart = (x, y); break;
                        case 'E': Tiles[x, y] = Tile.Exit; ExitPos = (x, y); break;
                        case 'D': Tiles[x, y] = Tile.Door; Doors.Add((x, y)); break;
                        case '*': Tiles[x, y] = Tile.Plate; Plates.Add((x, y)); break;

                        case 'O': Tiles[x, y] = Tile.Flavor; Flavors.Add((x, y)); break; // flavor objects
                        case 'F': Tiles[x, y] = Tile.Flavor; Flavors.Add((x, y)); break; // flavor objects

                        case '1': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.One; break;
                        case '2': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.Two; break;
                        case '3': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.Three; break;

                        case 'I': Tiles[x, y] = Tile.Interactable; Searchables.Add((x, y)); break; // items
                        case 'T': Tiles[x, y] = Tile.Interactable; Searchables.Add((x, y)); break; // items
                        case 'L': Tiles[x, y] = Tile.Interactable; Levers.Add((x, y)); break;      // levers

                        default: Tiles[x, y] = Tile.Empty; break;
                    }
                }
            }
        }

        // Reset back to original ASCII-defined state
        public void Reset() => ParseRows(RawMap);

        // Can the player walk into (x, y)... closed doors and shapes block
        public bool IsWalkable(World world, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            if (Tiles[x, y] == Tile.Wall) return false;

            // Closed door is a wall unless World.OpenDoors says it is open
            if (Doors.Contains((x, y)) && !world.OpenDoors.Contains((this, (x, y)))) return false;

            // Shapes are solid... pushing is handled before walking
            if (Shapes.ContainsKey((x, y))) return false;

            return true;
        }

        // Can we push a shape into (x, y)
        public bool IsEmptyForShape(World world, int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            if (Tiles[x, y] == Tile.Wall) return false;
            if (Doors.Contains((x, y)) && !world.OpenDoors.Contains((this, (x, y)))) return false;
            if (Shapes.ContainsKey((x, y))) return false;
            return true;
        }

        // What glyph do we show at (x, y)
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
                Tile.Flavor => 'O',
                Tile.Interactable => 'T',
                _ => '.'
            };
        }
    }


    // WORLD... cross room state like open doors, keys, lever choice

    class World
    {
        public List<Room> Rooms = new();
        public int CurrentRoomIndex = 0;
        public Room CurrentRoom => Rooms[CurrentRoomIndex];

        public (int x, int y) PlayerPos;

        // If a door is open, it will be listed here with its owning room
        public HashSet<(Room room, (int x, int y) door)> OpenDoors = new();

        // Room 1... only the correct shape on the plate opens the door
        public ShapeId CorrectShapeForPlate = ShapeId.Two;

        // Room 2... you can find a key in one item... then unlock door from adjacent
        public bool HasKeyRoom2 = false;
        public (int x, int y)? FixedKeyItemPos = null; // optional... set to pin exact item

        // Room 3... one lever is the correct one
        public (int x, int y)? CorrectLeverPos = null;
    }


    // GAME... main loop... input... rendering... puzzle rules

    static class Game
    {
        static void Main()
        {
            Console.Title = "Escaped Alien... Area 51";
            TryResizeConsole(120, 40); // try to make more room... ignored if OS denies

            var world = BuildWorld();
            EnterRoom(world, 0, reset: true);

            bool quit = false;
            while (!quit)
            {
                Render(world);    // draw centered map + legend
                ShowStatus(world);// short info line under map

                // Real-time Control WASD, E, Q 
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.W: TryMove(world, 0, -1); break;
                    case ConsoleKey.A: TryMove(world, -1, 0); break;
                    case ConsoleKey.S: TryMove(world, 0, 1); break;
                    case ConsoleKey.D: TryMove(world, 1, 0); break;
                    case ConsoleKey.E: TryInteract(world); break;
                    case ConsoleKey.Q: quit = true; break;
                }

                // Win... in room 3 and step onto its E exit
                bool lastRoom = world.CurrentRoomIndex == world.Rooms.Count - 1;
                if (lastRoom && world.CurrentRoom.ExitPos.HasValue &&
                    world.PlayerPos == world.CurrentRoom.ExitPos.Value)
                {
                    Console.Clear();
                    Render(world);
                    Console.WriteLine();
                    Console.WriteLine("You slip through the final blast door... the desert night greets you...");
                    Console.WriteLine("You made it.");

                    break;
                }
            }
        }

        // enlarge the console window and buffer
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


        // RENDER... center map and draw a legend to the right

        static void Render(World world)
        {
            Console.Clear();
            var room = world.CurrentRoom;

            // Legend text... keep short so we can center map + legend block
            string[] legend =
            {
                "LEGEND",
                "P  Player",
                "#  Wall",
                "D  Door",
                "/  Door open",
                "E  Exit",
                "*  Plate",
                "1/2/3 Shapes",
                "O  Flavor",
                "  Interact",
                "",
                "Controls",
                "WASD Move",
                "E    Interact",
                "Q    Quit"
            };

            // print each map row plus an optional legend row
            //  approximate total width to center the whole block
            int legendPadding = 3;                          // spaces between map and legend
            int legendWidth = legend.Max(s => s.Length);    // longest legend line
            int totalWidth = room.Width + legendPadding + legendWidth;

            // Left margin so map+legend are centered
            int leftMargin = Math.Max(0, (Console.WindowWidth - totalWidth) / 2);

            for (int y = 0; y < room.Height; y++)
            {
                // Left margin for centering
                Console.Write(new string(' ', leftMargin));

                // Build map row
                var chars = new char[room.Width];
                for (int x = 0; x < room.Width; x++)
                    chars[x] = room.RenderAt(world, x, y, world.PlayerPos);
                string mapRow = new string(chars);

                // Print map row
                Console.Write(mapRow);

                // Pad to legend start
                Console.Write(new string(' ', legendPadding));

                // Print legend line if available
                if (y < legend.Length)
                    Console.Write(legend[y]);

                Console.WriteLine();
            }
        }

        // Basic status under the map... helpful when demoing
        static void ShowStatus(World world)
        {
            Console.WriteLine();
            if (world.CurrentRoomIndex == 1)
                Console.WriteLine($"Room... {world.CurrentRoom.Name}    Key... {(world.HasKeyRoom2 ? "yes" : "no")}");
            else
                Console.WriteLine($"Room... {world.CurrentRoom.Name}");
        }


        // MOVEMENT... pushes shapes first, then walks if possible

        static void TryMove(World world, int dx, int dy)
        {
            var room = world.CurrentRoom;
            var (x, y) = world.PlayerPos;
            int nx = x + dx, ny = y + dy;

            // If next cell has a shape, try to push it
            if (room.Shapes.ContainsKey((nx, ny)))
            {
                int sx = nx + dx, sy = ny + dy; // target cell for the shape
                if (room.IsEmptyForShape(world, sx, sy))
                {
                    var sid = room.Shapes[(nx, ny)];
                    room.Shapes.Remove((nx, ny));
                    room.Shapes[(sx, sy)] = sid;

                    // Player moves into the shape's former spot
                    if (room.IsWalkable(world, nx, ny))
                        world.PlayerPos = (nx, ny);

                    // Plate logic only applies in room 1
                    if (world.CurrentRoomIndex == 0) EvaluateRoom1Plate(world);

                    TryAdvanceIfOnExit(world);
                }
                return;
            }

            // Otherwise attempt to walk
            if (room.IsWalkable(world, nx, ny))
            {
                world.PlayerPos = (nx, ny);
                TryAdvanceIfOnExit(world);
            }
        }


        // INTERACT... flavor, items, levers, doors from adjacent cell

        static void TryInteract(World world)
        {
            var room = world.CurrentRoom;
            var pos = world.PlayerPos;

            // 1) Doors... unlock from an adjacent cell with E
            foreach (var dir in new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) })
            {
                var neighbor = (pos.x + dir.dx, pos.y + dir.dy);
                if (room.Doors.Contains(neighbor))
                {
                    bool open = world.OpenDoors.Contains((room, neighbor));
                    Console.WriteLine();
                    if (!open && world.CurrentRoomIndex == 1 && world.HasKeyRoom2)
                    {
                        world.OpenDoors.Add((room, neighbor));
                        Console.WriteLine("You unlock the door with the key...");
                        PauseBrief();
                        PauseBrief();
                    }
                    else if (!open)
                    {
                        Console.WriteLine("The door is locked...");
                        PauseBrief();
                        PauseBrief();
                    }
                    else
                    {
                        Console.WriteLine("The door is already open.");

                    }
                    PauseBrief();
                    return;
                }
            }

            // 2) Flavor... narrative objects
            if (room.Flavors.Contains(pos))
            {
                Console.WriteLine();
                Console.WriteLine("A human artifact... you see symbols that match your species code...");
                PauseBrief();
                PauseBrief();
                PauseBrief();
                return;
            }

            // 3) Room 2... search items... one contains the key
            if (world.CurrentRoomIndex == 1 && room.Searchables.Contains(pos))
            {
                var correct = world.FixedKeyItemPos ??
                              room.Searchables.OrderBy(p => Math.Abs(p.x - room.Width / 2)
                                                         + Math.Abs(p.y - room.Height / 2)).First();

                Console.WriteLine();
                if (!world.HasKeyRoom2 && pos == correct)
                {
                    world.HasKeyRoom2 = true;
                    Console.WriteLine("Inside the locker... a brass key glints. You take it...");
                    PauseBrief();
                    PauseBrief();
                    PauseBrief();
                }
                else
                {
                    Console.WriteLine("Dust... notes... old coats... nothing useful...");
                }
                PauseBrief();
                PauseBrief();
                PauseBrief();
                return;
            }

            // 4) Room 3... pull a lever
            if (world.CurrentRoomIndex == 2 && room.Levers.Contains(pos))
            {
                if (world.CorrectLeverPos == null)
                {
                    // Deterministic pick so it is consistent across runs... topmost... then leftmost
                    world.CorrectLeverPos = room.Levers.OrderBy(p => p.y).ThenBy(p => p.x).First();
                }

                Console.WriteLine();
                if (pos == world.CorrectLeverPos.Value)
                {
                    foreach (var d in room.Doors) world.OpenDoors.Add((room, d));
                    Console.WriteLine("Heavy locks release somewhere ahead...");
                    PauseBrief();
                    PauseBrief();
                    PauseBrief();
                }
                else
                {
                    Console.WriteLine("You pull the lever... lights flicker... then nothing...");
                }
                PauseBrief();
                PauseBrief();
                PauseBrief();
                return;
            }

            // 5) Hint on plate in room 1
            if (room.Plates.Contains(pos) && world.CurrentRoomIndex == 0)
            {
                Console.WriteLine();
                Console.WriteLine("A heavy pressure plate... perhaps the right shape will trigger it...");
                PauseBrief();
                PauseBrief();
                PauseBrief();
                return;
            }

            // 6) Exit tile... try to advance
            if (room.ExitPos.HasValue && pos == room.ExitPos.Value)
            {
                TryAdvanceIfOnExit(world);
                return;
            }

            // Default... nothing here
            Console.WriteLine();
            Console.WriteLine("There is nothing to interact with here...");
            PauseBrief();
            PauseBrief();
        }


        // ROOM FLOW... move to next room when conditions are met

        static void TryAdvanceIfOnExit(World world)
        {
            var room = world.CurrentRoom;
            if (!room.ExitPos.HasValue) return;
            if (world.PlayerPos != room.ExitPos.Value) return;

            if (world.CurrentRoomIndex == 0)
            {
                // Room 1... door must be open via correct shape on plate
                foreach (var d in room.Doors)
                    if (!world.OpenDoors.Contains((room, d)))
                        return;

                EnterRoom(world, 1, reset: true);
                return;
            }

            if (world.CurrentRoomIndex == 1)
            {
                // Room 2... door must be unlocked
                foreach (var d in room.Doors)
                    if (!world.OpenDoors.Contains((room, d)))
                        return;

                EnterRoom(world, 2, reset: true);
                return;
            }
        }

        // Room 1... check if any plate has the correct shape resting on it... open or close door
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

        // Enter a room... optionally reset to original layout
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
                world.CorrectLeverPos = null;
                foreach (var d in room.Doors) world.OpenDoors.Remove((room, d));
            }

            Console.Clear();
            Console.WriteLine(room.Name);
            if (index == 0)
            {
                Console.WriteLine("You are an escaped alien deep inside Area 51...");
                Console.WriteLine("Move with WASD... interact with E...");
                Console.WriteLine("Push the right shape onto the plate to open the door...");
            }
            PauseBrief();
        }

        static void PauseBrief() => Thread.Sleep(450);

        // =======
        // MAPS...
        // =======
        static World BuildWorld()
        {
            var world = new World();

            // Room 1... shapes and a plate... only the correct shape opens the door
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

            // Room 2...  map with T items... door must be unlocked from beside after key collect
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

            // Room 3... three levers... only one opens the final door
            var room3 = new Room(
                "Room 3... Maintenance Tunnel",
                new[]
                {
                    "######################################################################################",
                    "#P....................L.................................D..........................#",
                    "#..........##############...............................###############...........#",
                    "#..........#............#L.................................#............#.........#",
                    "#..........#............#..................................#............#.........#",
                    "#..........#............#L.................................#............#.........#",
                    "#..........................................##############..#.......E#",
                    "######################################################################################",
                });

            world.Rooms.Add(room1);
            world.Rooms.Add(room2);
            world.Rooms.Add(room3);

            // Room 1 correct shape
            world.CorrectShapeForPlate = ShapeId.Two;



            return world;
        }
    }
}
