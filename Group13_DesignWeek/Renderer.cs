using System;
using System.Linq;
using System.Text;

namespace Group13_DesignWeek
{
    static class Renderer
    {
        // redraw bookkeeping
        public static int LastFrameWidth = 0;
        public static int LastFrameHeight = 0;

        public static void TryResizeConsole(int width, int height)
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
        public static void Render(World world)
        {
            var room = world.CurrentRoom;

            var legendList = new System.Collections.Generic.List<string>();
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
            int legendWidth = legend.Length == 0 ? 0 : legend.Max(s => s.Length); // max width of legend lines
            int totalWidth = room.Width + legendPadding + legendWidth;// total width of map + legend
            int leftMargin = Math.Max(0, (Console.WindowWidth - totalWidth) / 2);     // center horizontally

            var sb = new StringBuilder(room.Height * (room.Width + legendWidth + leftMargin + 8)); //

            // title
            string title = room.Name;//
            sb.Append(' ', leftMargin);
            sb.AppendLine(title.PadRight(totalWidth));      

            // map rows with fixed width and legend lines
            for (int y = 0; y < room.Height; y++)// for each row in the room
            {
                var chars = new char[room.Width];// create a char array for the row
                for (int x = 0; x < room.Width; x++)// for each column in the row
                    chars[x] = room.RenderAt(world, x, y, world.PlayerPos);// render the tile at (x, y)
                string mapRow = new string(chars);// convert the char array to a string

                string legendRow = (y < legend.Length) ? legend[y] : string.Empty;// get the corresponding legend line or empty if none

                string line = mapRow + new string(' ', legendPadding) + legendRow;// combine map row and legend line with padding
                if (line.Length < totalWidth) line = line.PadRight(totalWidth);// ensure fixed width

                sb.Append(' ', leftMargin);// add left margin
                sb.AppendLine(line);// append the line to the string builder
            }

            // status
            string status = (world.CurrentRoomIndex == 1)// if in room 2
                ? $"Room... {room.Name}    Key... {(world.HasKeyRoom2 ? "yes" : "no")}"// show key status
                : $"Room... {room.Name}";// otherwise just show room name
            sb.Append(' ', leftMargin);//   add left margin
            sb.AppendLine(status.PadRight(totalWidth));// append status line

            Console.SetCursorPosition(0, 0);
            Console.Write(sb.ToString());

            // clear extra lines from previous frame if any
            int currentFrameHeight = 1 + room.Height + 1;
            if (LastFrameHeight > currentFrameHeight)
            {
                for (int i = 0; i < LastFrameHeight - currentFrameHeight; i++)
                {
                    Console.Write(new string(' ', leftMargin + Math.Max(LastFrameWidth, totalWidth)));
                    Console.WriteLine();
                }
            }

            LastFrameWidth = Math.Max(LastFrameWidth, totalWidth);
            LastFrameHeight = currentFrameHeight;
        }
    }
}
