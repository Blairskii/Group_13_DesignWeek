using System;
using System.Linq;
using System.Text;

namespace Group13_DesignWeek
{
    static class Renderer
    {
        // Keep track of window to detect resizes (e.g., going fullscreen)
        private static (int w, int h) _lastWindow = (0, 0);

        // ---------------------------
        // Window / buffer helpers
        // ---------------------------
        public static void TryResizeConsole(int width, int height)
        {
            try
            {
                int w = Math.Min(width, Console.LargestWindowWidth);
                int h = Math.Min(height, Console.LargestWindowHeight);
                Console.SetWindowSize(w, h);
                Console.SetBufferSize(w, h); // keep buffer == window to avoid scroll
            }
            catch { /* ignore best-effort errors */ }
        }

        private static bool WindowChanged()
        {
            var now = (Console.WindowWidth, Console.WindowHeight);
            bool changed = now != _lastWindow;
            _lastWindow = now;
            return changed;
        }

        private static void ClearWholeWindow()
        {
            try
            {
                Console.SetCursorPosition(0, 0);
                string blank = new string(' ', Console.WindowWidth);
                for (int r = 0; r < Console.WindowHeight; r++)
                    Console.WriteLine(blank);
                Console.SetCursorPosition(0, 0);
            }
            catch { }
        }

        // ---------------------------
        // POPUP (centered, modal)
        // ---------------------------
        public static void ShowPopup(params string[] lines)
        {
            // Drain any pending keys so the popup waits properly
            while (Console.KeyAvailable) Console.ReadKey(true);

            // On any size change, do a full clear first
            if (WindowChanged()) ClearWholeWindow();

            // We’ll draw on a fully blank background area
            ClearWholeWindow();

            int innerWidth = Math.Min(
                Math.Max(30, (lines?.Length ?? 0) == 0 ? 0 : lines.Max(s => s?.Length ?? 0)),
                Math.Max(30, Console.WindowWidth - 6)
            );

            int boxWidth = innerWidth + 2;
            int boxHeight = (lines?.Length ?? 0) + 2;

            int left = Math.Max(0, (Console.WindowWidth - boxWidth) / 2);
            int top = Math.Max(0, (Console.WindowHeight - boxHeight) / 2);

            string horiz = new string('-', innerWidth);

            SafeWriteAt(left, top, "+" + horiz + "+");
            for (int i = 0; i < (lines?.Length ?? 0); i++)
            {
                string line = lines![i] ?? "";
                if (line.Length > innerWidth) line = line.Substring(0, innerWidth);
                line = line.PadRight(innerWidth);
                SafeWriteAt(left, top + 1 + i, "|" + line + "|");
            }
            SafeWriteAt(left, top + 1 + (lines?.Length ?? 0), "+" + horiz + "+");

            string footer = "Press any key";
            int fx = Math.Max(0, (Console.WindowWidth - footer.Length) / 2);
            int fy = Math.Min(Console.WindowHeight - 1, top + boxHeight + 1);
            SafeWriteAt(fx, fy, footer);

            Console.ReadKey(true);
            while (Console.KeyAvailable) Console.ReadKey(true);

            // After popup, hard-clear so nothing lingers into next frame
            ClearWholeWindow();
        }

        // ---------------------------
        // MAIN RENDER (full clear each frame)
        // ---------------------------
        public static void Render(World world)
        {
            // If size changed, full clear first
            if (WindowChanged()) ClearWholeWindow();

            // Always full clear each frame to prevent leftovers (fixes the screenshot issue)
            ClearWholeWindow();

            var room = world.CurrentRoom;

            // Build legend (only discovered entries)
            var legendList = new System.Collections.Generic.List<string> { "LEGEND" };
            foreach (var kv in world.DiscoveredLegend.OrderBy(k => k.Key))
                legendList.Add($"{kv.Key}  {kv.Value}");
            legendList.Add("");
            legendList.Add("Controls");
            legendList.Add("WASD Move");
            legendList.Add("E    Interact");
            legendList.Add("R    Retry");
            legendList.Add("Q    Quit");
            string[] legend = legendList.ToArray();

            // Layout
            int legendPadding = 3;
            int legendWidth = legend.Length == 0 ? 0 : legend.Max(s => s.Length);
            int totalWidth = room.Width + legendPadding + legendWidth;
            int leftMargin = Math.Max(0, (Console.WindowWidth - totalWidth) / 2);

            var sb = new StringBuilder((room.Height + 3) * (totalWidth + leftMargin + 2));

            // Title
            sb.Append(' ', leftMargin);
            sb.AppendLine(PadRightExact(room.Name, totalWidth));

            // Map + legend rows
            for (int y = 0; y < room.Height; y++)
            {
                var row = new char[room.Width];
                for (int x = 0; x < room.Width; x++)
                    row[x] = room.RenderAt(world, x, y, world.PlayerPos);

                string mapRow = new string(row);
                string legendRow = (y < legend.Length) ? legend[y] : string.Empty;
                string line = mapRow + new string(' ', legendPadding) + legendRow;
                sb.Append(' ', leftMargin);
                sb.AppendLine(PadRightExact(line, totalWidth));
            }

            // Status
            string status = (world.CurrentRoomIndex == 2)
                ? $"Room... {room.Name}    Key... {(world.HasKeyRoom2 ? "yes" : "no")}"
                : $"Room... {room.Name}";
            sb.Append(' ', leftMargin);
            sb.AppendLine(PadRightExact(status, totalWidth));

            // Write the composed frame
            SafeWriteAt(0, 0, sb.ToString());
        }

        // ---------------------------
        // Helpers
        // ---------------------------
        private static void SafeWriteAt(int x, int y, string text)
        {
            try
            {
                if (y < 0 || y >= Console.WindowHeight) return;
                if (x < 0) x = 0;

                // Clip to visible width
                int maxLen = Math.Max(0, Console.WindowWidth - x);
                string toWrite = text ?? string.Empty;

                // Multi-line support: write chunk by chunk to keep clipping simple
                int line = 0, col = x, row = y;
                int idx = 0;
                while (idx < toWrite.Length && row < Console.WindowHeight)
                {
                    // find end of this line (or string)
                    int nl = toWrite.IndexOf('\n', idx);
                    string seg = (nl == -1 ? toWrite.Substring(idx) : toWrite.Substring(idx, nl - idx));

                    // clip segment
                    if (seg.Length > maxLen) seg = seg.Substring(0, maxLen);

                    Console.SetCursorPosition(col, row);
                    Console.Write(seg);

                    // move to next line
                    row++;
                    col = 0;
                    maxLen = Console.WindowWidth; // from col 0 now

                    if (nl == -1) break; // no more lines
                    idx = nl + 1;
                }
            }
            catch
            {
                // ignore cursor exceptions (race with resize, etc.)
            }
        }

        private static string PadRightExact(string s, int width)
        {
            if (s == null) return new string(' ', width);
            if (s.Length >= width) return s.Substring(0, width);
            return s.PadRight(width);
        }
    }
}
