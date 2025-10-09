using System;
using System.Linq;
using System.Text;

namespace Group13_DesignWeek
{
    static class Renderer
    {
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

        // ==============================
        // NEW POPUP SYSTEM
        // ==============================
        public static void ShowPopup(params string[] lines)
        {
            int innerWidth = Math.Min(
                Math.Max(28, (lines?.Length ?? 0) == 0 ? 0 : lines.Max(s => s?.Length ?? 0)),
                Math.Max(28, Console.WindowWidth - 6)
            );

            int boxWidth = innerWidth + 2;
            int boxHeight = (lines?.Length ?? 0) + 2;

            int left = Math.Max(0, (Console.WindowWidth - (boxWidth + 2)) / 2);
            int top = Math.Max(0, (Console.WindowHeight - (boxHeight + 2)) / 2);

            // clear background behind box
            for (int r = top - 1; r <= top + boxHeight + 1 && r < Console.WindowHeight; r++)
            {
                if (r < 0) continue;
                Console.SetCursorPosition(0, r);
                Console.Write(new string(' ', Console.WindowWidth));
            }

            string horiz = new string('-', innerWidth);

            Console.SetCursorPosition(left, top);
            Console.Write("+" + horiz + "+");

            for (int i = 0; i < (lines?.Length ?? 0); i++)
            {
                string line = lines[i] ?? "";
                if (line.Length < innerWidth) line = line.PadRight(innerWidth);
                Console.SetCursorPosition(left, top + 1 + i);
                Console.Write("|" + line + "|");
            }

            Console.SetCursorPosition(left, top + 1 + (lines?.Length ?? 0));
            Console.Write("+" + horiz + "+");

            
           
            while (Console.KeyAvailable) Console.ReadKey(true);
            Console.ReadKey(true);
            while (Console.KeyAvailable) Console.ReadKey(true);
        }

        // ==============================
        // RENDER MAP + LEGEND
        // ==============================
        public static void Render(World world)
        {
            var room = world.CurrentRoom;

            var legendList = new System.Collections.Generic.List<string>
            {
                "LEGEND"
            };

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

            string title = room.Name;
            sb.Append(' ', leftMargin);
            sb.AppendLine(title.PadRight(totalWidth));

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

            string status = (world.CurrentRoomIndex == 1)
                ? $"Room... {room.Name}    Key... {(world.HasKeyRoom2 ? "yes" : "no")}"
                : $"Room... {room.Name}";
            sb.Append(' ', leftMargin);
            sb.AppendLine(status.PadRight(totalWidth));

            Console.SetCursorPosition(0, 0);
            Console.Write(sb.ToString());

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
