using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

// Slice an AI sprite sheet (one row, white background) into frames,
// align them (X: hat centre or body centre of mass / Y: sheet ground line or each frame's feet),
// size them by hat width, downscale, and quantize to a 5-grey + red palette.
// Comments are ASCII on purpose: Windows PowerShell 5.1 Add-Type misreads non-ASCII source.
public static class SheetTool
{
    static int[] Read(Bitmap b)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var a = new int[b.Width * b.Height];
        Marshal.Copy(d.Scan0, a, 0, a.Length);
        b.UnlockBits(d);
        return a;
    }

    static Bitmap Write(int[] a, int w, int h)
    {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(a, 0, d.Scan0, a.Length);
        b.UnlockBits(d);
        return b;
    }

    static int Lum(int c) { int r = (c >> 16) & 255, g = (c >> 8) & 255, bl = c & 255; return (r * 299 + g * 587 + bl * 114) / 1000; }
    static bool Red(int c) { int r = (c >> 16) & 255, g = (c >> 8) & 255, bl = c & 255; return r > 120 && r - g > 70 && r - bl > 70; }

    static readonly int[] Pal = {
        unchecked((int)0xFF101010), unchecked((int)0xFF3A3A3A), unchecked((int)0xFF6E6E6E),
        unchecked((int)0xFFA8A8A8), unchecked((int)0xFFE6E6E6) };
    const int RedCol = unchecked((int)0xFFC0202A);

    static int LongestRun(bool[] bg, int W, int y, int x0, int x1, out int runStart)
    {
        int best = 0, cur = 0, curStart = x0; runStart = x0;
        for (int x = x0; x <= x1; x++)
        {
            if (!bg[y * W + x]) { if (cur == 0) curStart = x; cur++; if (cur > best) { best = cur; runStart = curStart; } }
            else cur = 0;
        }
        return best;
    }

    // Background = white region connected to the image border (keeps light cloth inside the figure).
    static bool[] Background(int[] px, int W, int H, int bgThreshold)
    {
        var bg = new bool[W * H];
        var q = new Queue<int>();
        for (int x = 0; x < W; x++) { q.Enqueue(x); q.Enqueue((H - 1) * W + x); }
        for (int y = 0; y < H; y++) { q.Enqueue(y * W); q.Enqueue(y * W + W - 1); }
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            if (bg[i] || Lum(px[i]) < bgThreshold) continue;
            bg[i] = true;
            int x = i % W, y = i / W;
            if (x > 0) q.Enqueue(i - 1);
            if (x < W - 1) q.Enqueue(i + 1);
            if (y > 0) q.Enqueue(i - W);
            if (y < H - 1) q.Enqueue(i + W);
        }
        return bg;
    }

    // Split a multi-row atlas into one-row sheets (rowPaths[r] = row r, top to bottom).
    // Rows are found per connected blob, not by a horizontal cut, because a raised blade can reach
    // above the bottom of the row over it. Big blobs are grouped into rows by their bottom (feet) line;
    // small blobs (dizzy stars, ink drops) join the row whose height range holds their centre.
    public static string SplitRows(string src, string[] rowPaths, int bgThreshold)
    {
        var bmp = new Bitmap(src);
        int W = bmp.Width, H = bmp.Height;
        int[] px = Read(bmp);
        bmp.Dispose();
        var bg = Background(px, W, H, bgThreshold);

        var label = new int[W * H];
        for (int i = 0; i < label.Length; i++) label[i] = -1;
        var top = new List<int>(); var bot = new List<int>(); var area = new List<int>();
        var stack = new Stack<int>();
        for (int s = 0; s < W * H; s++)
        {
            if (bg[s] || label[s] >= 0) continue;
            int id = top.Count, t = H, b = -1, a = 0;
            label[s] = id; stack.Push(s);
            while (stack.Count > 0)
            {
                int i = stack.Pop(), x = i % W, y = i / W;
                a++; if (y < t) t = y; if (y > b) b = y;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int j = ny * W + nx;
                        if (bg[j] || label[j] >= 0) continue;
                        label[j] = id; stack.Push(j);
                    }
            }
            top.Add(t); bot.Add(b); area.Add(a);
        }

        int n = top.Count, rows = rowPaths.Length;
        var big = new List<int>();
        for (int c = 0; c < n; c++) if (area[c] >= 400) big.Add(c);
        big.Sort((p, q2) => bot[p].CompareTo(bot[q2]));
        var rowOf = new int[n];
        var rowTop = new List<int>(); var rowBot = new List<int>();
        int prev = -1000;
        foreach (int c in big)
        {
            if (bot[c] - prev > 40) { rowTop.Add(top[c]); rowBot.Add(bot[c]); }
            int r = rowTop.Count - 1;
            rowOf[c] = r;
            rowTop[r] = Math.Min(rowTop[r], top[c]); rowBot[r] = Math.Max(rowBot[r], bot[c]);
            prev = bot[c];
        }
        if (rowTop.Count != rows)
            throw new Exception("atlas rows found " + rowTop.Count + ", expected " + rows);

        var outTop = new List<int>(rowTop); var outBot = new List<int>(rowBot);
        for (int c = 0; c < n; c++)
        {
            if (area[c] >= 400) continue;
            double cy = (top[c] + bot[c]) / 2.0, bestD = double.MaxValue; int best = 0;
            for (int r = 0; r < rows; r++)
            {
                double d = cy < rowTop[r] ? rowTop[r] - cy : cy > rowBot[r] ? cy - rowBot[r] : 0;
                if (d < bestD) { bestD = d; best = r; }
            }
            rowOf[c] = best;
            outTop[best] = Math.Min(outTop[best], top[c]); outBot[best] = Math.Max(outBot[best], bot[c]);
        }

        const int pad = 8;
        var sb = new StringBuilder("rows=");
        for (int r = 0; r < rows; r++)
        {
            int y0 = Math.Max(0, outTop[r] - pad), y1 = Math.Min(H - 1, outBot[r] + pad), h = y1 - y0 + 1;
            var o = new int[W * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = (y + y0) * W + x;
                    o[y * W + x] = label[i] >= 0 && rowOf[label[i]] == r ? px[i] : unchecked((int)0xFFFFFFFF);
                }
            var ob = Write(o, W, h);
            ob.Save(rowPaths[r], ImageFormat.Png);
            ob.Dispose();
            sb.Append(y0 + "-" + y1 + (r < rows - 1 ? "," : ""));
        }
        return sb.ToString();
    }

    // Attack effect sheet (one row, light ink on a BLACK background) -> frames of exactly outW x outH px,
    // i.e. the hitbox size at 45 px/unit, so the effect covers the hitbox and nothing else.
    // Every frame is cut with frame 1's box (same place in each cell) and stretched to the target ratio
    // (the AI never keeps the requested ratio). Output is grey on transparent; the game tints it.
    public static string RunFx(string src, string stripPath, string framePrefix, int expected, int outW, int outH)
    {
        var bmp = new Bitmap(src);
        int W = bmp.Width, H = bmp.Height;
        int[] px = Read(bmp);
        bmp.Dispose();
        const int ink = 50;

        var colCount = new int[W];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (Lum(px[y * W + x]) >= ink) colCount[x]++;
        var segs = new List<int[]>();
        int start = -1, gap = 0;
        for (int x = 0; x < W; x++)
        {
            if (colCount[x] > 0) { if (start < 0) start = x; gap = 0; }
            else if (start >= 0) { gap++; if (gap >= 25) { segs.Add(new[] { start, x - gap }); start = -1; gap = 0; } }
        }
        if (start >= 0) segs.Add(new[] { start, W - 1 });
        // Stray drops of the last (dissolving) frame: merge the closest neighbours.
        while (segs.Count > expected)
        {
            int bestK = 0, bestGap = int.MaxValue;
            for (int k = 0; k + 1 < segs.Count; k++)
            {
                int g = segs[k + 1][0] - segs[k][1];
                if (g < bestGap) { bestGap = g; bestK = k; }
            }
            segs[bestK] = new[] { segs[bestK][0], segs[bestK + 1][1] };
            segs.RemoveAt(bestK + 1);
        }
        int n = segs.Count;
        if (n != expected) throw new Exception("fx frames found " + n + ", expected " + expected);

        // Frames sit on an even grid; a dissolving frame's first column is not its cell's left edge.
        var diffs = new List<int>();
        for (int f = 1; f < n; f++) diffs.Add(segs[f][0] - segs[f - 1][0]);
        diffs.Sort();
        int pitch = n > 1 ? diffs[(n - 1) / 2] : 0;
        int fw = segs[0][1] - segs[0][0] + 1;
        int top = H, bot = -1;
        for (int f = 0; f < n; f++)
            for (int y = 0; y < H; y++) for (int x = segs[f][0]; x <= segs[f][1]; x++)
                if (Lum(px[y * W + x]) >= ink) { if (y < top) top = y; if (y > bot) bot = y; }
        int fh = bot - top + 1;
        double sx = (double)fw / outW, sy = (double)fh / outH;

        int[] levels = { 120, 155, 190, 225, 255 };
        var strip = new int[outW * n * outH];
        for (int f = 0; f < n; f++)
        {
            int left = segs[0][0] + f * pitch;
            for (int ty = 0; ty < outH; ty++)
                for (int tx = 0; tx < outW; tx++)
                {
                    int ax = left + (int)(tx * sx), bx = left + (int)((tx + 1) * sx);
                    int ay = top + (int)(ty * sy), by = top + (int)((ty + 1) * sy);
                    int tot = 0, cnt = 0; long sum = 0;
                    for (int y = ay; y < Math.Max(by, ay + 1); y++)
                        for (int x = ax; x < Math.Max(bx, ax + 1); x++)
                        {
                            tot++;
                            if (x < 0 || x >= W || y < 0 || y >= H) continue;
                            int l = Lum(px[y * W + x]);
                            if (l >= ink) { cnt++; sum += l; }
                        }
                    if (cnt * 2 < tot) continue;
                    int avg = (int)(sum / cnt);
                    int idx = avg < 100 ? 0 : avg < 140 ? 1 : avg < 180 ? 2 : avg < 220 ? 3 : 4;
                    int v = levels[idx];
                    strip[ty * (outW * n) + f * outW + tx] = unchecked((int)0xFF000000) | (v << 16) | (v << 8) | v;
                }
        }
        var outBmp = Write(strip, outW * n, outH);
        if (!string.IsNullOrEmpty(stripPath)) outBmp.Save(stripPath, ImageFormat.Png);
        outBmp.Dispose();
        if (!string.IsNullOrEmpty(framePrefix))
            for (int f = 0; f < n; f++)
            {
                var one = new int[outW * outH];
                for (int y = 0; y < outH; y++) Array.Copy(strip, y * (outW * n) + f * outW, one, y * outW, outW);
                var fb = Write(one, outW, outH);
                fb.Save(framePrefix + "_" + f + ".png", ImageFormat.Png);
                fb.Dispose();
            }

        var sb = new StringBuilder();
        sb.Append("frames=" + n + " src=" + fw + "x" + fh + " (ratio " + ((double)fw / fh).ToString("F2") + ") -> " + outW + "x" + outH +
            " (ratio " + ((double)outW / outH).ToString("F2") + ") fill=");
        for (int f = 0; f < n; f++)
        {
            int c = 0;
            for (int y = 0; y < outH; y++) for (int x = 0; x < outW; x++) if (strip[y * (outW * n) + f * outW + x] != 0) c++;
            sb.Append((100 * c / (outW * outH)) + "%" + (f < n - 1 ? "," : ""));
        }
        return sb.ToString();
    }

    public static string Run(string src, string stripPath, string framePrefix, int targetH, double targetHat, int cw, int ch, int anchorX, int footY, int bgThreshold,
        int expected, bool massX, bool feetY, int firstFrameH, double fixedScale)
    {
        var bmp = new Bitmap(src);
        int W = bmp.Width, H = bmp.Height;
        int[] px = Read(bmp);
        bmp.Dispose();

        var bg = Background(px, W, H, bgThreshold);

        // Column projection -> frame segments.
        var colCount = new int[W];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (!bg[y * W + x]) colCount[x]++;
        var segs = new List<int[]>();
        int start = -1, gap = 0;
        for (int x = 0; x < W; x++)
        {
            bool on = colCount[x] >= 3;
            if (on) { if (start < 0) start = x; gap = 0; }
            else if (start >= 0)
            {
                gap++;
                if (gap >= 12) { segs.Add(new[] { start, x - gap }); start = -1; gap = 0; }
            }
        }
        if (start >= 0) segs.Add(new[] { start, W - 1 });
        segs.RemoveAll(s => s[1] - s[0] < 60);

        if (expected > 0)
        {
            // Too many pieces (a dropped sword or hat apart from the body): merge the closest neighbours.
            while (segs.Count > expected)
            {
                int bestK = 0, bestGap = int.MaxValue;
                for (int k = 0; k + 1 < segs.Count; k++)
                {
                    int g = segs[k + 1][0] - segs[k][1];
                    if (g < bestGap) { bestGap = g; bestK = k; }
                }
                segs[bestK] = new[] { segs[bestK][0], segs[bestK + 1][1] };
                segs.RemoveAt(bestK + 1);
            }
            // Too few (frames touching, e.g. sword tips): split the widest at its emptiest middle column.
            while (segs.Count < expected)
            {
                int k = 0;
                for (int i = 1; i < segs.Count; i++) if (segs[i][1] - segs[i][0] > segs[k][1] - segs[k][0]) k = i;
                int a = segs[k][0], b = segs[k][1];
                int best = (a + b) / 2, bestCount = int.MaxValue;
                for (int x = a + (b - a) / 4; x <= b - (b - a) / 4; x++)
                    if (colCount[x] < bestCount) { bestCount = colCount[x]; best = x; }
                segs[k] = new[] { a, best - 1 };
                segs.Insert(k + 1, new[] { best + 1, b });
            }
        }

        // Per frame: top, bottom, hat center.
        int n = segs.Count;
        var top = new int[n]; var bot = new int[n]; var brim = new int[n]; var hatX = new double[n]; var hatW = new int[n];
        for (int f = 0; f < n; f++)
        {
            int x0 = segs[f][0], x1 = segs[f][1], t = H, b = -1;
            for (int y = 0; y < H; y++) for (int x = x0; x <= x1; x++)
                if (!bg[y * W + x]) { if (y < t) t = y; if (y > b) b = y; }
            top[f] = t; bot[f] = b;
            // Hat brim = first row from the top whose longest run is wide (a raised sword tip is narrow).
            int minW = (int)((b - t) * 0.28);
            brim[f] = t;
            for (int y = t; y <= b; y++)
            {
                int rs, rl = LongestRun(bg, W, y, x0, x1, out rs);
                if (rl >= minW) { brim[f] = y; break; }
            }
            double sx = 0; int cnt = 0;
            for (int y = brim[f]; y <= brim[f] + 5; y++)
            {
                int rs, rl = LongestRun(bg, W, y, x0, x1, out rs);
                if (rl > 0) { sx += rs + rl / 2.0; cnt++; }
            }
            // Hat width: the hat is rigid, so its width is the same in every pose -> size reference.
            for (int y = brim[f]; y <= brim[f] + 10 && y < H; y++)
            {
                int rs, rl = LongestRun(bg, W, y, x0, x1, out rs);
                if (rl > hatW[f]) hatW[f] = rl;
            }
            hatX[f] = cnt > 0 ? sx / cnt : (x0 + x1) / 2.0;

            if (massX)
            {
                // Body centre of mass (the hat may have fallen off, e.g. death).
                double mx = 0; long mc = 0;
                for (int y = t; y <= b; y++) for (int x = x0; x <= x1; x++)
                    if (!bg[y * W + x]) { mx += x; mc++; }
                if (mc > 0) hatX[f] = mx / mc;
            }
        }

        // One ground line per sheet, so jump/airborne frames keep their height.
        int baseline = 0;
        for (int f = 0; f < n; f++) if (bot[f] > baseline) baseline = bot[f];
        var heights = new List<int>();
        for (int f = 0; f < n; f++) heights.Add(baseline - brim[f] + 1);
        var sorted = new List<int>(heights); sorted.Sort();
        var hw = new List<int>(hatW); hw.Sort();
        // targetHat > 0: size by hat width (consistent across sheets); otherwise by brim-to-feet height.
        double scale = targetHat > 0 ? hw[n / 2] / targetHat : (double)sorted[n / 2] / targetH;
        // firstFrameH > 0: size by the first frame's full height instead (hat tilted or fallen off, e.g. death).
        if (firstFrameH > 0) scale = (double)(bot[0] - top[0] + 1) / firstFrameH;
        // fixedScale > 0: reuse another sheet's scale (rows of one atlas are drawn at one size).
        if (fixedScale > 0) scale = fixedScale;

        // Build frames.
        var strip = new int[cw * n * ch];
        for (int f = 0; f < n; f++)
        {
            for (int ty = 0; ty < ch; ty++)
                for (int tx = 0; tx < cw; tx++)
                {
                    double sx0 = hatX[f] + (tx - anchorX) * scale;
                    // feetY: each frame's lowest pixel on the ground line (jump height comes from physics, not the sheet).
                    double sy0 = (feetY ? bot[f] : baseline) + 1 + (ty - footY - 1) * scale;
                    int ax = (int)Math.Floor(sx0), bx = (int)Math.Floor(sx0 + scale);
                    int ay = (int)Math.Floor(sy0), by = (int)Math.Floor(sy0 + scale);
                    int tot = 0, ink = 0, red = 0; long sum = 0;
                    for (int y = ay; y < by; y++)
                    {
                        if (y < 0 || y >= H) { tot += Math.Max(0, bx - ax); continue; }
                        for (int x = ax; x < bx; x++)
                        {
                            tot++;
                            if (x < segs[f][0] || x > segs[f][1] || x < 0 || x >= W) continue;
                            int i = y * W + x;
                            if (bg[i]) continue;
                            ink++; sum += Lum(px[i]); if (Red(px[i])) red++;
                        }
                    }
                    if (tot == 0 || ink * 2 < tot) continue;
                    int col;
                    if (red * 5 >= ink) col = RedCol;
                    else
                    {
                        int avg = (int)(sum / ink);
                        int idx = avg < 40 ? 0 : avg < 90 ? 1 : avg < 140 ? 2 : avg < 195 ? 3 : 4;
                        col = Pal[idx];
                    }
                    strip[ty * (cw * n) + f * cw + tx] = col;
                }
        }
        var outBmp = Write(strip, cw * n, ch);
        if (!string.IsNullOrEmpty(stripPath)) outBmp.Save(stripPath, ImageFormat.Png);
        outBmp.Dispose();

        // One PNG per frame (what Unity imports).
        // PowerShell passes $null as "" for string parameters.
        if (!string.IsNullOrEmpty(framePrefix))
            for (int f = 0; f < n; f++)
            {
                var one = new int[cw * ch];
                for (int y = 0; y < ch; y++) Array.Copy(strip, y * (cw * n) + f * cw, one, y * cw, cw);
                var fb = Write(one, cw, ch);
                fb.Save(framePrefix + "_" + f + ".png", ImageFormat.Png);
                fb.Dispose();
            }

        var sb = new StringBuilder();
        sb.Append("frames=" + n + " scale=" + scale.ToString("F3") + " heights=");
        for (int f = 0; f < n; f++) sb.Append(heights[f] + (f < n - 1 ? "," : ""));
        sb.Append(" hatW=" + hw[n / 2] + " (=" + (hw[n / 2] / scale).ToString("F1") + "px)");
        sb.Append(" hatX-in-seg=");
        for (int f = 0; f < n; f++) sb.Append((hatX[f] - segs[f][0]).ToString("F0") + (f < n - 1 ? "," : ""));
        return sb.ToString();
    }
}
