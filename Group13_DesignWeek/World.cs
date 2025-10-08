using System;
using System.Collections.Generic;

namespace Group13_DesignWeek
{
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
}
