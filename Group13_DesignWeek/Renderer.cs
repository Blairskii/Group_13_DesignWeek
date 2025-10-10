using System;
using System.Linq;
using System.Text;

namespace Group13_DesignWeek
{
    static class Renderer
    {
        // cached frame to avoid rewriting identical rows
        private static string[] _lastFrame = Array.Empty<string>();
        private static int _lastLeftMargin = -1;
        private static int _lastTotalWidth = -1;
        private static int _lastConsoleW = -1;
        private static int _lastConsoleH = -1;

        public static int LastFrameWidth = 0;
        public static int LastFrameHeight = 0;

        public static void TryResizeConsole(int width, int height)
        {
            try
            {
                Console.SetWindowSize(Math.Min(width, Console.LargestWindowWidth),
                                      Math.Min(height, Console.LargestWindowHeight));
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
                Console.CursorVisible = false;
            }
            catch { }
        }

        /// <summary>Force next Render() to re-draw everything.</summary>
        public static void Invalidate()
        {
            _lastFrame = Array.Empty<string>();
            _lastLeftMargin = -1;
            _lastTotalWidth = -1;
        }

        // ===========================
        // POPUP
        // ===========================
        public static void ShowPopup(params string[] lines)
        {
            SafeClear();
            DrawBox(lines);
            Invalidate(); // ensure next map render repaints fully
        }

        private static void DrawBox(string[] lines)
        {
            int innerWidth = Math.Min(
                Math.Max(28, (lines?.Length ?? 0) == 0 ? 0 : lines.Max(s => s?.Length ?? 0)),
                Math.Max(28, Console.WindowWidth - 6)
            );

            int left = Math.Max(0, (Console.WindowWidth - innerWidth - 2) / 2);
            int top = Math.Max(0, (Console.WindowHeight - ((lines?.Length ?? 0) + 4)) / 2);

            string horiz = new string('-', innerWidth);

            Console.SetCursorPosition(left, top);
            Console.Write("+" + horiz + "+");

            for (int i = 0; i < (lines?.Length ?? 0); i++)
            {
                string text = (lines[i] ?? "").PadRight(innerWidth);
                Console.SetCursorPosition(left, top + 1 + i);
                Console.Write("|" + text + "|");
            }

            Console.SetCursorPosition(left, top + 1 + (lines?.Length ?? 0));
            Console.Write("+" + horiz + "+");

            Console.SetCursorPosition(left, top + 3 + (lines?.Length ?? 0));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[ Press any key to continue ]");
            Console.ResetColor();

            while (Console.KeyAvailable) Console.ReadKey(true);
            Console.ReadKey(true);
            while (Console.KeyAvailable) Console.ReadKey(true);
        }

        // ===========================
        // ASCII CARD (TITLE/END)
        // ===========================
        public static void ShowAsciiCard(string top, string mid, string bottom, string prompt)
        {
            SafeClear();

            int w = Console.WindowWidth;
            int h = Console.WindowHeight;
            int centerY = Math.Max(0, h / 2 - 3);

            void Center(string text, int offset, ConsoleColor color = ConsoleColor.Gray)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                int x = Math.Max(0, (w - text.Length) / 2);
                Console.SetCursorPosition(x, centerY + offset);
                Console.ForegroundColor = color;
                Console.Write(text);
                Console.ResetColor();
            }

            Center("==============================", -3, ConsoleColor.DarkGray);
            Center(top, 0, ConsoleColor.White);
            Center(mid, 1, ConsoleColor.Gray);
            Center(bottom, 3, ConsoleColor.Gray);
            Center("==============================", 5, ConsoleColor.DarkGray);
            Center(prompt, 7, ConsoleColor.DarkGray);

            while (Console.KeyAvailable) Console.ReadKey(true);
            Console.ReadKey(true);
            while (Console.KeyAvailable) Console.ReadKey(true);

            Invalidate();
        }

        public static void ShowAsciiCard(string top, string bottom, string prompt)
            => ShowAsciiCard(top, null, bottom, prompt);

        // ===========================
        // MAP RENDERING (diff-based)
        // ===========================
        public static void Render(World world)
        {
            // Invalidate on window size changes
            if (Console.WindowWidth != _lastConsoleW || Console.WindowHeight != _lastConsoleH)
            {
                _lastConsoleW = Console.WindowWidth;
                _lastConsoleH = Console.WindowHeight;
                Invalidate();
            }

            var room = world.CurrentRoom;

            var legendList = new System.Collections.Generic.List<string> { "LEGEND" };
            foreach (var kv in world.DiscoveredLegend.OrderBy(k => k.Key))
                legendList.Add($"{kv.Key}  {kv.Value}");
            legendList.Add("");
            legendList.Add("Controls");
            legendList.Add("WASD Move");
            legendList.Add("E    Interact");
            legendList.Add("X    Restart Room");
            legendList.Add("R    Restart Game");
            legendList.Add("Q    Quit");

            string[] legend = legendList.ToArray();

            int legendPadding = 3;
            int legendWidth = legend.Length == 0 ? 0 : legend.Max(s => s.Length);
            int totalWidth = room.Width + legendPadding + legendWidth;
            int leftMargin = Math.Max(0, (Console.WindowWidth - totalWidth) / 2);

            // Build current frame text lines (no colors) for diffing
            int totalRows = 1 + room.Height + 1; // title + map rows + status
            string[] cur = new string[totalRows];

            // Title
            cur[0] = (room.Name ?? "").PadRight(totalWidth);

            // Map + Legend rows
            for (int y = 0; y < room.Height; y++)
            {
                var chars = new char[room.Width];
                for (int x = 0; x < room.Width; x++)
                    chars[x] = room.RenderAt(world, x, y, world.PlayerPos);
                string mapRow = new string(chars);

                string legendRow = (y < legend.Length) ? legend[y] : string.Empty;
                string combined = mapRow + new string(' ', legendPadding) + legendRow;
                if (combined.Length < totalWidth) combined = combined.PadRight(totalWidth);
                cur[1 + y] = combined;
            }

            // Status
            string status = (world.CurrentRoomIndex == 2)
                ? $"Room... {room.Name}    Key... {(world.HasKeyRoom2 ? "yes" : "no")}"
                : $"Room... {room.Name}";
            cur[totalRows - 1] = status.PadRight(totalWidth);

            // If layout changed (margins/width), force full diff
            bool layoutChanged = (leftMargin != _lastLeftMargin) || (totalWidth != _lastTotalWidth);
            if (layoutChanged || _lastFrame.Length != cur.Length)
                _lastFrame = new string[cur.Length]; // all null -> full repaint

            // Write only changed rows
            for (int row = 0; row < cur.Length; row++)
            {
                if (_lastFrame[row] == cur[row] && !layoutChanged)
                    continue; // identical, skip

                // clear the full console row once
                int screenY = row; // title at 0, map 1..H, status at H+1
                if (screenY >= Console.WindowHeight) break; // safety
                Console.SetCursorPosition(0, screenY);
                Console.Write(new string(' ', Console.WindowWidth));

                // now draw starting at leftMargin
                Console.SetCursorPosition(leftMargin, screenY);

                if (row == 0 || row == cur.Length - 1)
                {
                    // Title or status: plain text
                    Console.ForegroundColor = (row == 0) ? ConsoleColor.White : ConsoleColor.DarkGray;
                    Console.Write(cur[row]);
                    Console.ResetColor();
                }
                else
                {
                    // Map row with colors + legend
                    int y = row - 1; // map row index

                    // left: map chars
                    for (int x = 0; x < room.Width; x++)
                    {
                        var ch = cur[row][x];
                        WriteColored(ch);
                    }

                    // padding
                    Console.Write(new string(' ', legendPadding));

                    // legend part (gray)
                    Console.ForegroundColor = ConsoleColor.Gray;
                    int legendStart = room.Width + legendPadding;
                    if (legendStart < cur[row].Length)
                        Console.Write(cur[row].Substring(legendStart));
                    Console.ResetColor();
                }

                _lastFrame[row] = cur[row];
            }

            _lastLeftMargin = leftMargin;
            _lastTotalWidth = totalWidth;

            LastFrameWidth = totalWidth;
            LastFrameHeight = totalRows;
        }

        private static void SafeClear()
        {
            try
            {
                Console.Clear();
                Console.CursorVisible = false;
            }
            catch { }
        }

        private static void WriteColored(char ch)
        {
            switch (ch)
            {
                case 'P': Console.ForegroundColor = ConsoleColor.Green; break;
                case '#':
                case '=': Console.ForegroundColor = ConsoleColor.Blue; break;
                case 'D': Console.ForegroundColor = ConsoleColor.Yellow; break;
                case '/': Console.ForegroundColor = ConsoleColor.Cyan; break;
                case '@': Console.ForegroundColor = ConsoleColor.Magenta; break;
                case '&': Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                case 'L': Console.ForegroundColor = ConsoleColor.White; break;
                case 'E': Console.ForegroundColor = ConsoleColor.Green; break;
                default:  Console.ForegroundColor = ConsoleColor.Gray; break;
            }
            Console.Write(ch);
            Console.ResetColor();
        }
    }
}
