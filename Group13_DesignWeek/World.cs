class World
{
    public System.Collections.Generic.List<Room> Rooms = new();
    public int CurrentRoomIndex = 0;
    public Room CurrentRoom => Rooms[CurrentRoomIndex];

    public (int x, int y) PlayerPos;

    public System.Collections.Generic.HashSet<(Room room, (int x, int y) door)> OpenDoors = new();

    // room 1
    public ShapeId CorrectShapeForPlate = ShapeId.Two; // keep if you still use 1/2/3 elsewhere

    // room 2
    public bool HasKeyRoom2 = false;
    public (int x, int y)? FixedKeyItemPos = null;

    // room 3
    public System.Collections.Generic.HashSet<(int x, int y)> PulledLevers = new();
    public int NextDoorIndexRoom3 = 0;

    // progressive legend
    public System.Collections.Generic.Dictionary<char, string> DiscoveredLegend = new();
}
