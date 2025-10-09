using Group13_DesignWeek;

class Room
{
    public string Name = "";
    public int Width;
    public int Height;

    public Tile[,] Tiles;
    public (int x, int y) PlayerStart;

    // dynamic collections
    public Dictionary<(int x, int y), ShapeId> Shapes = new();   // pushables 1..3..Barrel
    public HashSet<(int x, int y)> Plates = new();               // pressure plates
    public HashSet<(int x, int y)> Doors = new();                // doors
    public (int x, int y)? ExitPos;                              // exit tile

    public HashSet<(int x, int y)> Searchables = new();          // T or I
    public HashSet<(int x, int y)> Levers = new();               // L

    public HashSet<(int x, int y)> Flavors = new();              // flavor interactables
    public Dictionary<(int x, int y), char> FlavorGlyphs = new();// which glyph was placed

    public HashSet<(int x, int y)> CorrectLevers = new();        // lever helpers (room 3)
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

    // turn ASCII into tiles and tag dynamic objects
    void ParseRows(string[] rows)
    {
        Shapes.Clear(); Plates.Clear(); Doors.Clear();
        Searchables.Clear(); Levers.Clear(); Flavors.Clear();
        FlavorGlyphs.Clear(); CorrectLevers.Clear();
        ExitPos = null;

        for (int y = 0; y < Height; y++)
        {
            string row = rows[y]; // keep spaces if any
            for (int x = 0; x < Width; x++)
            {
                char c = (x < row.Length) ? row[x] : '#'; // pad short rows as walls

                switch (c)
                {
                    case '#': Tiles[x, y] = Tile.Wall; break;
                    case '=': Tiles[x, y] = Tile.Wall2; break;
                    case '.': Tiles[x, y] = Tile.Empty; break;

                    case 'P': Tiles[x, y] = Tile.Empty; PlayerStart = (x, y); break;
                    case 'E': Tiles[x, y] = Tile.Exit; ExitPos = (x, y); break;
                    case 'D': Tiles[x, y] = Tile.Door; Doors.Add((x, y)); break;

                    // plates now use @
                    case '@': Tiles[x, y] = Tile.Plate; Plates.Add((x, y)); break;

                    // pushables... include barrel as &
                    case '&': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.Barrel; break;
                    case '1': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.One; break;
                    case '2': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.Two; break;
                    case '3': Tiles[x, y] = Tile.Empty; Shapes[(x, y)] = ShapeId.Three; break;

                    // items and generic interactables
                    case 'I':
                    case 'T': Tiles[x, y] = Tile.Interactable; Searchables.Add((x, y)); break;

                    // levers
                    case 'L': Tiles[x, y] = Tile.Interactable; Levers.Add((x, y)); break;

                    // flavor glyphs... 
                    case 'C':
                    case 'O':
                    case 'F':
                    case 'h':
                    case 'Y':
                    case '0':
                    case '[':
                    case ']':
                        Tiles[x, y] = Tile.Flavor; Flavors.Add((x, y)); FlavorGlyphs[(x, y)] = c; break;

                    default: Tiles[x, y] = Tile.Empty; break;
                }
            }
        }

        
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!Levers.Contains((x, y))) continue;

                bool hasC =
                    (x > 0 && x - 1 < RawMap[y].Length && RawMap[y][x - 1] == 'C') ||
                    (x + 1 < RawMap[y].Length && RawMap[y][x + 1] == 'C') ||
                    (y > 0 && x < RawMap[y - 1].Length && RawMap[y - 1][x] == 'C') ||
                    (y + 1 < RawMap.Length && x < RawMap[y + 1].Length && RawMap[y + 1][x] == 'C');

                if (hasC) CorrectLevers.Add((x, y));
            }
        }
    }

    public void Reset() => ParseRows(RawMap);

    public bool IsWalkable(World world, int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
        if (Tiles[x, y] == Tile.Wall) return false;
        if (Doors.Contains((x, y)) && !world.OpenDoors.Contains((this, (x, y)))) return false;
        if (Shapes.ContainsKey((x, y))) return false; // shapes are solid
        return true;
    }

    public bool IsEmptyForShape(World world, int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
        if (Tiles[x, y] == Tile.Wall) return false;
        if (Doors.Contains((x, y)) && !world.OpenDoors.Contains((this, (x, y)))) return false;
        if (Shapes.ContainsKey((x, y))) return false;
        return true;
    }

    // choose glyph to render
    public char RenderAt(World world, int x, int y, (int x, int y) playerPos)
    {
        if (playerPos == (x, y)) return 'P';

        if (Shapes.TryGetValue((x, y), out var sid))
            return sid switch
            {
                ShapeId.One => '1',
                ShapeId.Two => '2',
                ShapeId.Three => '3',
                ShapeId.Barrel => '&',
                _ => 'S'
            };

        if (Doors.Contains((x, y)))
            return world.OpenDoors.Contains((this, (x, y))) ? '/' : 'D';

        return Tiles[x, y] switch
        {
            Tile.Wall2 => '=',
            Tile.Empty => '.',
            Tile.Wall => '#',
            Tile.Exit => 'E',
            Tile.Plate => '@', // plates draw as @ now
            Tile.Flavor => FlavorGlyphs.TryGetValue((x, y), out var g) ? g : 'O',
            Tile.Interactable => 'T',
            Tile.Lever => 'L',
            _ => '.'
        };
    }
}
