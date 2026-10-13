using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

// Slice an AI sprite sheet (one row of frames on a flat background) into game frames:
//   1. remove the background (white flood fill / chroma-key flood fill / black = glow effect),
//   2. split into frames (column gaps, then merge/split to the expected frame count),
//   3. align every frame on an anchor (hat centre / body mass / foot mass / cell centre),
//   4. scale (hat width / median height / one frame's height / fixed),
//   5. downscale to pixel size (grey palette for the old ink style, or "mode colour" for colour art).
// Comments are ASCII on purpose: Windows PowerShell 5.1 Add-Type misreads non-ASCII source.

public class SheetOptions
{
    // Output frame canvas and where the anchor lands in it.
    public int CellW = 192, CellH = 112, AnchorX = 96, FootY = 104;
    public int Frames = 0;                 // expected frame count (0 = trust the gaps)

    // Background: "white" | "key" | "black"
    public string Bg = "key";
    public int KeyR = 255, KeyG = 0, KeyB = 255;
    public int KeyTolerance = 120;         // |dr|+|dg|+|db| still counted as background
    public int WhiteThreshold = 238;
    public bool Despill = true;            // drop magenta-tinted edge pixels

    // Anchor X: "hat" | "mass" | "feet" | "cell"    Anchor Y: "ground" | "feet" | "mass" | "cell"
    public string AlignX = "hat", AlignY = "ground";
    public int MassY = 0;                  // target row for the mass centre when AlignY = "mass"/"cell"

    // Scale: "hat" (TargetHat px) | "median" (median frame height -> TargetH) | "frame" (RefFrame height -> TargetH) | "fixed" (Fixed source px per output px)
    public string ScaleMode = "hat";
    public double TargetHat = 0;           // 0 = derive from TargetBrimToFeet on this sheet (reference sheet)
    public int TargetBrimToFeet = 52;
    public int TargetH = 64;
    public int RefFrame = 0;
    public double Fixed = 8;
    public double TargetArea = 1500;      // output px^2 for "area" (median frame) / "areaFrame" (RefFrame)

    // Colour: "color" | "grey"
    public string Color = "color";
    public double Coverage = 0.34;         // min share of non-background source pixels for an output pixel
}

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

    static int R(int c) { return (c >> 16) & 255; }
    static int G(int c) { return (c >> 8) & 255; }
    static int B(int c) { return c & 255; }
    static int Lum(int c) { return (R(c) * 299 + G(c) * 587 + B(c) * 114) / 1000; }
    static bool RedInk(int c) { return R(c) > 120 && R(c) - G(c) > 70 && R(c) - B(c) > 70; }

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

    // ---------------------------------------------------------------- background

    static bool[] Background(int[] px, int W, int H, SheetOptions o)
    {
        var bg = new bool[W * H];
        if (o.Bg == "black")
        {
            // Glow effects on black: background = (almost) black; the colour itself carries the glow.
            for (int i = 0; i < px.Length; i++) bg[i] = Math.Max(R(px[i]), Math.Max(G(px[i]), B(px[i]))) < 24;
            return bg;
        }

        if (o.Bg == "key")
        {
            // A key colour never appears in the art, so remove it everywhere — including pockets enclosed by
            // arms, sleeves or the hat brim that a flood fill from the border cannot reach.
            for (int i = 0; i < px.Length; i++) bg[i] = IsBgColor(px[i], o);
            if (o.Despill) DespillEdges(px, bg, W, H, o);
            return bg;
        }

        var q = new Queue<int>();
        for (int x = 0; x < W; x++) { q.Enqueue(x); q.Enqueue((H - 1) * W + x); }
        for (int y = 0; y < H; y++) { q.Enqueue(y * W); q.Enqueue(y * W + W - 1); }
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            if (bg[i] || !IsBgColor(px[i], o)) continue;
            bg[i] = true;
            int x = i % W, y = i / W;
            if (x > 0) q.Enqueue(i - 1);
            if (x < W - 1) q.Enqueue(i + 1);
            if (y > 0) q.Enqueue(i - W);
            if (y < H - 1) q.Enqueue(i + W);
        }

        return bg;
    }

    // Edge pixels blended with the key colour (e.g. pinkish fringe on magenta) -> background. Two passes for 2px fringes.
    static void DespillEdges(int[] px, bool[] bg, int W, int H, SheetOptions o)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            var drop = new List<int>();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    if (bg[i]) continue;
                    if (!(bg[i - 1] || bg[i + 1] || bg[i - W] || bg[i + W])) continue;
                    if (KeyTint(px[i], o)) drop.Add(i);
                }
            foreach (int i in drop) bg[i] = true;
        }
    }

    static bool IsBgColor(int c, SheetOptions o)
    {
        if (o.Bg == "white") return Lum(c) >= o.WhiteThreshold;
        int d = Math.Abs(R(c) - o.KeyR) + Math.Abs(G(c) - o.KeyG) + Math.Abs(B(c) - o.KeyB);
        return d <= o.KeyTolerance;
    }

    static bool KeyTint(int c, SheetOptions o)
    {
        if (o.KeyR > 200 && o.KeyB > 200 && o.KeyG < 60) // magenta
            return R(c) - G(c) > 50 && B(c) - G(c) > 50;
        if (o.KeyG > 200 && o.KeyR < 60 && o.KeyB < 60)   // green
            return G(c) - R(c) > 60 && G(c) - B(c) > 60;
        return false;
    }

    // ---------------------------------------------------------------- slicing

    static List<int[]> Segments(bool[] bg, int W, int H, int expected, out int[] colCount)
    {
        colCount = new int[W];
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
        segs.RemoveAll(s => s[1] - s[0] < 16);

        if (expected > 0)
        {
            // Too many pieces (dropped weapon, hat, smoke puff): merge the closest neighbours.
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
            // Too few (frames touching, e.g. a staff reaching into the next frame): split the widest at its emptiest middle column.
            var cc = colCount;
            while (segs.Count < expected)
            {
                int k = 0;
                for (int i = 1; i < segs.Count; i++) if (segs[i][1] - segs[i][0] > segs[k][1] - segs[k][0]) k = i;
                int a = segs[k][0], b = segs[k][1];
                int best = (a + b) / 2, bestCount = int.MaxValue;
                for (int x = a + (b - a) / 4; x <= b - (b - a) / 4; x++)
                    if (cc[x] < bestCount) { bestCount = cc[x]; best = x; }
                segs[k] = new[] { a, best - 1 };
                segs.Insert(k + 1, new[] { best + 1, b });
            }
        }
        return segs;
    }

    // Label 8-connected blobs and give each to the frame whose column range contains the blob's centre
    // (or the nearest frame). Returns per-pixel frame index, -1 for background.
    static int[] AssignBlobs(bool[] bg, int W, int H, List<int[]> segs)
    {
        var owner = new int[W * H];
        for (int i = 0; i < owner.Length; i++) owner[i] = -1;
        var label = new int[W * H];
        var stack = new Stack<int>();
        var members = new List<int>();
        int next = 1;
        for (int s = 0; s < label.Length; s++)
        {
            if (bg[s] || label[s] != 0) continue;
            members.Clear();
            label[s] = next; stack.Push(s);
            double sumX = 0;
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                members.Add(i);
                int x = i % W, y = i / W;
                sumX += x;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int j = ny * W + nx;
                        if (bg[j] || label[j] != 0) continue;
                        label[j] = next; stack.Push(j);
                    }
            }
            double cx = sumX / members.Count;
            int best = 0; double bestD = double.MaxValue;
            for (int k = 0; k < segs.Count; k++)
            {
                double d = cx < segs[k][0] ? segs[k][0] - cx : cx > segs[k][1] ? cx - segs[k][1] : 0;
                if (d < bestD) { bestD = d; best = k; }
            }
            foreach (int i in members) owner[i] = best;
            next++;
        }
        return owner;
    }

    static long Median(List<long> v) { v.Sort(); return v[v.Count / 2]; }

    // ---------------------------------------------------------------- main

    public static string Run(string src, string stripPath, string framePrefix, SheetOptions o)
    {
        var bmp = new Bitmap(src);
        int W = bmp.Width, H = bmp.Height;
        int[] px = Read(bmp);
        bmp.Dispose();

        var bg = Background(px, W, H, o);
        int[] colCount;
        var segs = Segments(bg, W, H, o.Frames, out colCount);
        int n = segs.Count;
        if (n == 0) return "no frames found";

        // Each connected blob belongs to the frame whose column range holds its centre, so a staff reaching
        // across into the neighbouring frame's columns still stays with its owner.
        int[] owner = AssignBlobs(bg, W, H, segs);
        var fx0 = new int[n]; var fx1 = new int[n];
        for (int f = 0; f < n; f++) { fx0[f] = W; fx1[f] = -1; }
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            int f = owner[y * W + x];
            if (f < 0) continue;
            if (x < fx0[f]) fx0[f] = x; if (x > fx1[f]) fx1[f] = x;
        }

        var top = new int[n]; var bot = new int[n]; var brim = new int[n];
        var ax = new double[n]; var my = new double[n]; var hatW = new int[n]; var area = new long[n];
        var mine = new bool[W * H];
        for (int f = 0; f < n; f++)
        {
            int x0 = Math.Max(0, fx0[f]), x1 = Math.Max(x0, fx1[f]), t = H, b = -1;
            for (int i = 0; i < mine.Length; i++) mine[i] = owner[i] != f;   // "background" for this frame
            double sx = 0, sy = 0; long cnt = 0;
            for (int y = 0; y < H; y++) for (int x = x0; x <= x1; x++)
                if (!mine[y * W + x]) { if (y < t) t = y; if (y > b) b = y; sx += x; sy += y; cnt++; }
            if (b < 0) { t = 0; b = 1; }
            top[f] = t; bot[f] = b; area[f] = cnt;
            double massX = cnt > 0 ? sx / cnt : (x0 + x1) / 2.0;
            my[f] = cnt > 0 ? sy / cnt : (t + b) / 2.0;

            // Hat brim = first row from the top whose longest run is wide (a raised staff tip is narrow).
            int minW = (int)((b - t) * 0.28);
            brim[f] = t;
            for (int y = t; y <= b; y++)
            {
                int rs, rl = LongestRun(mine, W, y, x0, x1, out rs);
                if (rl >= minW) { brim[f] = y; break; }
            }
            double hx = 0; int hc = 0;
            for (int y = brim[f]; y <= brim[f] + 5 && y < H; y++)
            {
                int rs, rl = LongestRun(mine, W, y, x0, x1, out rs);
                if (rl > 0) { hx += rs + rl / 2.0; hc++; }
            }
            for (int y = brim[f]; y <= brim[f] + 10 && y < H; y++)
            {
                int rs, rl = LongestRun(mine, W, y, x0, x1, out rs);
                if (rl > hatW[f]) hatW[f] = rl;
            }

            if (o.AlignX == "hat") ax[f] = hc > 0 ? hx / hc : massX;
            else if (o.AlignX == "feet")
            {
                // Mass of the lowest quarter of the figure: stays put while arms and weapons swing.
                int y0 = b - (b - t) / 4; double fx = 0; long fc = 0;
                for (int y = y0; y <= b; y++) for (int x = x0; x <= x1; x++)
                    if (!mine[y * W + x]) { fx += x; fc++; }
                ax[f] = fc > 0 ? fx / fc : massX;
            }
            else if (o.AlignX == "cell") ax[f] = (f + 0.5) * W / n;
            else ax[f] = massX;
        }

        int baseline = 0;
        for (int f = 0; f < n; f++) if (bot[f] > baseline) baseline = bot[f];

        var hw = new List<int>(hatW); hw.Sort();
        var hs = new List<int>(); for (int f = 0; f < n; f++) hs.Add(bot[f] - top[f] + 1); hs.Sort();
        double scale;
        string scaleNote;
        if (o.ScaleMode == "hat")
        {
            if (o.TargetHat <= 0)
            {
                var b2f = new List<int>(); for (int f = 0; f < n; f++) b2f.Add(baseline - brim[f] + 1); b2f.Sort();
                scale = (double)b2f[n / 2] / o.TargetBrimToFeet;
            }
            else scale = hw[n / 2] / o.TargetHat;
            scaleNote = "hatW=" + hw[n / 2] + " (=" + (hw[n / 2] / scale).ToString("F1") + "px)";
        }
        else if (o.ScaleMode == "median") { scale = (double)hs[n / 2] / o.TargetH; scaleNote = "medianH=" + hs[n / 2]; }
        else if (o.ScaleMode == "frame")
        {
            int r = Math.Min(Math.Max(o.RefFrame, 0), n - 1);
            scale = (double)(bot[r] - top[r] + 1) / o.TargetH; scaleNote = "frame" + r + "H=" + (bot[r] - top[r] + 1);
        }
        else if (o.ScaleMode == "area" || o.ScaleMode == "areaFrame")
        {
            // Pixel area is steadier than height for shapeless things (a flame whose tail length changes every frame).
            var areas = new List<long>(); for (int f = 0; f < n; f++) areas.Add(area[f]);
            long a = o.ScaleMode == "area" ? Median(areas) : area[Math.Min(Math.Max(o.RefFrame, 0), n - 1)];
            scale = Math.Sqrt(a / o.TargetArea); scaleNote = "area=" + a;
        }
        else { scale = o.Fixed; scaleNote = "fixed"; }

        var strip = new int[o.CellW * n * o.CellH];
        for (int f = 0; f < n; f++)
        {
            double originY;
            if (o.AlignY == "feet") originY = bot[f] + 1 - (o.FootY + 1) * scale;
            else if (o.AlignY == "mass") originY = my[f] - o.MassY * scale;
            else if (o.AlignY == "cell") originY = H / 2.0 - o.MassY * scale;
            else originY = baseline + 1 - (o.FootY + 1) * scale;
            double originX = ax[f] - o.AnchorX * scale;

            for (int ty = 0; ty < o.CellH; ty++)
                for (int tx = 0; tx < o.CellW; tx++)
                {
                    int sx0 = (int)Math.Floor(originX + tx * scale), sx1 = (int)Math.Floor(originX + (tx + 1) * scale);
                    int sy0 = (int)Math.Floor(originY + ty * scale), sy1 = (int)Math.Floor(originY + (ty + 1) * scale);
                    if (sx1 <= sx0) sx1 = sx0 + 1;
                    if (sy1 <= sy0) sy1 = sy0 + 1;
                    int col = Sample(px, owner, f, W, H, sx0, sx1, sy0, sy1, o);
                    if (col != 0) strip[ty * (o.CellW * n) + f * o.CellW + tx] = col;
                }
        }

        var outBmp = Write(strip, o.CellW * n, o.CellH);
        if (!string.IsNullOrEmpty(stripPath)) outBmp.Save(stripPath, ImageFormat.Png);
        outBmp.Dispose();

        // One PNG per frame (what Unity imports). PowerShell passes $null as "" for string parameters.
        if (!string.IsNullOrEmpty(framePrefix))
            for (int f = 0; f < n; f++)
            {
                var one = new int[o.CellW * o.CellH];
                for (int y = 0; y < o.CellH; y++) Array.Copy(strip, y * (o.CellW * n) + f * o.CellW, one, y * o.CellW, o.CellW);
                var fb = Write(one, o.CellW, o.CellH);
                fb.Save(framePrefix + "_" + f + ".png", ImageFormat.Png);
                fb.Dispose();
            }

        var sb = new StringBuilder();
        sb.Append("frames=" + n + " scale=" + scale.ToString("F1") + " " + scaleNote + " heights=");
        for (int f = 0; f < n; f++) sb.Append((bot[f] - top[f] + 1) + (f < n - 1 ? "," : ""));
        return sb.ToString();
    }

    // One output pixel from a block of source pixels.
    static int Sample(int[] px, int[] owner, int f, int W, int H, int sx0, int sx1, int sy0, int sy1, SheetOptions o)
    {
        int tot = 0, ink = 0;
        var buckets = new Dictionary<int, long[]>();
        long greySum = 0; int red = 0;
        for (int y = sy0; y < sy1; y++)
            for (int x = sx0; x < sx1; x++)
            {
                tot++;
                if (y < 0 || y >= H || x < 0 || x >= W) continue;
                int i = y * W + x;
                if (owner[i] != f) continue;
                ink++;
                int c = px[i];
                if (o.Color == "grey") { greySum += Lum(c); if (RedInk(c)) red++; continue; }
                // Mode colour: bucket by 3 bits per channel, keep the most common bucket's average.
                int key = ((R(c) >> 5) << 6) | ((G(c) >> 5) << 3) | (B(c) >> 5);
                long[] acc;
                if (!buckets.TryGetValue(key, out acc)) { acc = new long[4]; buckets[key] = acc; }
                acc[0]++; acc[1] += R(c); acc[2] += G(c); acc[3] += B(c);
            }
        if (tot == 0 || ink < tot * o.Coverage) return 0;

        if (o.Color == "grey")
        {
            if (red * 5 >= ink) return RedCol;
            int avg = (int)(greySum / ink);
            return Pal[avg < 40 ? 0 : avg < 90 ? 1 : avg < 140 ? 2 : avg < 195 ? 3 : 4];
        }

        long[] best = null;
        foreach (var acc in buckets.Values) if (best == null || acc[0] > best[0]) best = acc;
        int r = (int)(best[1] / best[0]), g = (int)(best[2] / best[0]), b = (int)(best[3] / best[0]);
        if (o.Bg == "key" && o.Despill && o.KeyR > 200 && o.KeyB > 200 && o.KeyG < 60)
        {
            // Semi-transparent parts (smoke, glow) mixed with magenta: remove the pink cast.
            int spill = Math.Min(r, b) - g;
            if (spill > 0) { r -= spill; b -= spill; }
        }
        else if (o.Bg == "key" && o.Despill && o.KeyG > 200 && o.KeyR < 60 && o.KeyB < 60)
        {
            // same for a green key: green cast on smoky edges
            int spill = g - Math.Max(r, b);
            if (spill > 0) g -= spill;
        }
        int a = 255;
        if (o.Bg == "black")
        {
            // Glow: alpha from brightness, colour normalised so additive/alpha blending keeps the hue.
            int m = Math.Max(r, Math.Max(g, b));
            a = Math.Min(255, m);
            if (m > 0) { r = r * 255 / m; g = g * 255 / m; b = b * 255 / m; }
        }
        return (a << 24) | (r << 16) | (g << 8) | b;
    }
}
