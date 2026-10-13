// Background layers from AI images -> game-ready PNGs (used by process_bg.ps1).
//   Layer: key out the chroma colour, optionally erase a circle (duplicate moon), make the left/right edges tile
//          seamlessly with a minimum-error vertical cut (no ghosting, unlike a crossfade).
//   Cut:   key out, optionally close the thin grid lines the AI drew between tiles, then save every separate
//          piece (props / tile chunks) as its own PNG.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public static class BgTool
{
    static int[] Read(Bitmap b)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var a = new int[b.Width * b.Height];
        Marshal.Copy(d.Scan0, a, 0, a.Length);
        b.UnlockBits(d);
        return a;
    }

    static void Save(int[] a, int w, int h, string path)
    {
        using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(a, 0, d.Scan0, a.Length);
            b.UnlockBits(d);
            b.Save(path, ImageFormat.Png);
        }
    }

    static int A(int c) { return (c >> 24) & 255; }
    static int R(int c) { return (c >> 16) & 255; }
    static int G(int c) { return (c >> 8) & 255; }
    static int B(int c) { return c & 255; }
    static int Lum(int c) { return (R(c) * 299 + G(c) * 587 + B(c) * 114) / 1000; }
    static int Argb(int a, int r, int g, int b) { return (a << 24) | (r << 16) | (g << 8) | b; }

    static int[] Load(string src, out int W, out int H)
    {
        using (var bmp = new Bitmap(src)) { W = bmp.Width; H = bmp.Height; return Read(bmp); }
    }

    // ---------------------------------------------------------------- key

    static bool IsKey(int c, int kr, int kg, int kb, int tol)
    {
        return Math.Abs(R(c) - kr) + Math.Abs(G(c) - kg) + Math.Abs(B(c) - kb) <= tol;
    }

    static bool KeyTint(int c, int kr, int kg, int kb)
    {
        if (kr > 200 && kb > 200 && kg < 60) return R(c) - G(c) > 50 && B(c) - G(c) > 50;
        if (kg > 200 && kr < 60 && kb < 60) return G(c) - R(c) > 60 && G(c) - B(c) > 60;
        return false;
    }

    // Key colour -> transparent everywhere, tinted fringe dropped, remaining edge spill pulled out of the colour.
    static void KeyOut(int[] px, int W, int H, int kr, int kg, int kb, int tol)
    {
        var bg = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) bg[i] = A(px[i]) < 8 || IsKey(px[i], kr, kg, kb, tol);
        for (int pass = 0; pass < 2; pass++)
        {
            var drop = new List<int>();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    int i = y * W + x;
                    if (bg[i] || !(bg[i - 1] || bg[i + 1] || bg[i - W] || bg[i + W])) continue;
                    if (KeyTint(px[i], kr, kg, kb)) drop.Add(i);
                }
            foreach (int i in drop) bg[i] = true;
        }
        bool magenta = kr > 200 && kb > 200 && kg < 60;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                if (bg[i]) { px[i] = 0; continue; }
                if (!magenta) continue;
                bool edge = (x > 0 && bg[i - 1]) || (x < W - 1 && bg[i + 1]) || (y > 0 && bg[i - W]) || (y < H - 1 && bg[i + W]);
                if (!edge) continue;
                int c = px[i], spill = Math.Min(R(c), B(c)) - G(c);
                if (spill > 0) px[i] = Argb(255, Math.Max(0, R(c) - spill / 2), G(c), Math.Max(0, B(c) - spill / 2));
            }
    }

    // ---------------------------------------------------------------- layer

    // eraseCircle "cx,cy,r,minLum": inside the circle, pixels at least that bright become transparent (a moon that
    // already exists in the far layer); darker scenery in front of it stays.
    public static string Layer(string src, string dst, bool keyed, int kr, int kg, int kb, int tol, int seam, string eraseCircle)
    {
        int W, H;
        var px = Load(src, out W, out H);
        var info = new StringBuilder();
        // erase first (paint with the key colour) so the moon's magenta-blended rim is cleaned up by the key pass
        if (!string.IsNullOrEmpty(eraseCircle)) info.Append("erased=" + EraseCircle(px, W, H, eraseCircle, Argb(255, kr, kg, kb)) + " ");
        if (keyed)
        {
            KeyOut(px, W, H, kr, kg, kb, tol);
            var solid = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) solid[i] = A(px[i]) > 0;
            info.Append("noise=" + DropSpecks(px, solid, W, H, 24) + " ");
        }

        int outW = W;
        if (seam > 0)
        {
            int[] cut;
            long cost = SeamCut(px, W, H, seam, out cut);
            outW = W - seam;
            var o = new int[outW * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < outW; x++)
                {
                    int c;
                    if (x >= seam) c = px[y * W + x];
                    else
                    {
                        // left of the cut: continue the image's right end; right of it: the image's start.
                        int a = px[y * W + W - seam + x], b = px[y * W + x];
                        float t = Math.Max(0f, Math.Min(1f, (x - cut[y] + 2) / 4f));
                        c = Mix(a, b, t);
                    }
                    o[y * outW + x] = c;
                }
            px = o;
            info.Append("seam=" + seam + " cost/row=" + (cost / H) + " ");
        }

        Save(px, outW, H, dst);
        return info.ToString() + outW + "x" + H;
    }

    static int EraseCircle(int[] px, int W, int H, string spec, int fill)
    {
        var p = spec.Split(',');
        int cx = int.Parse(p[0]), cy = int.Parse(p[1]), r = int.Parse(p[2]), minLum = int.Parse(p[3]);
        int erased = 0;
        for (int y = Math.Max(0, cy - r); y <= Math.Min(H - 1, cy + r); y++)
            for (int x = Math.Max(0, cx - r); x <= Math.Min(W - 1, cx + r); x++)
            {
                int dx = x - cx, dy = y - cy, i = y * W + x;
                if (dx * dx + dy * dy > r * r) continue;
                if (Lum(px[i]) >= minLum && !KeyTint(px[i], 255, 0, 255)) { px[i] = fill; erased++; }
            }
        return erased;
    }

    static int Mix(int a, int b, float t)
    {
        if (t <= 0f) return a;
        if (t >= 1f) return b;
        float aa = A(a) / 255f * (1 - t), ba = A(b) / 255f * t, al = aa + ba;
        if (al <= 0.001f) return 0;
        int r = (int)((R(a) * aa + R(b) * ba) / al), g = (int)((G(a) * aa + G(b) * ba) / al), bl = (int)((B(a) * aa + B(b) * ba) / al);
        return Argb((int)Math.Round(al * 255), r, g, bl);
    }

    static int Diff(int a, int b)
    {
        bool ta = A(a) < 128, tb = A(b) < 128;
        if (ta && tb) return 0;
        if (ta != tb) return 600;
        return Math.Abs(R(a) - R(b)) + Math.Abs(G(a) - G(b)) + Math.Abs(B(a) - B(b));
    }

    // Overlap the image's last `seam` columns with its first `seam` columns and find the top-to-bottom path where
    // they differ least (dynamic programming, one column step per row).
    static long SeamCut(int[] px, int W, int H, int seam, out int[] cut)
    {
        const int margin = 6;
        var cost = new long[H, seam];
        var from = new int[H, seam];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < seam; x++)
            {
                long c = Diff(px[y * W + W - seam + x], px[y * W + x]);
                if (x < margin || x >= seam - margin) c += 100000;
                if (y == 0) { cost[y, x] = c; continue; }
                long best = cost[y - 1, x]; int bx = x;
                if (x > 0 && cost[y - 1, x - 1] < best) { best = cost[y - 1, x - 1]; bx = x - 1; }
                if (x < seam - 1 && cost[y - 1, x + 1] < best) { best = cost[y - 1, x + 1]; bx = x + 1; }
                cost[y, x] = c + best; from[y, x] = bx;
            }
        cut = new int[H];
        int end = 0;
        for (int x = 1; x < seam; x++) if (cost[H - 1, x] < cost[H - 1, end]) end = x;
        long total = cost[H - 1, end];
        for (int y = H - 1; y >= 0; y--) { cut[y] = end; end = from[y, end]; }
        return total;
    }

    // ---------------------------------------------------------------- cut into pieces

    public static string Cut(string src, string dstPrefix, int kr, int kg, int kb, int tol, int gap, int minArea, bool fillGrid)
    {
        int W, H;
        var px = Load(src, out W, out H);
        var info = new StringBuilder();
        if (fillGrid) info.Append(FillGridLines(px, W, H, kr, kg, kb, tol));
        KeyOut(px, W, H, kr, kg, kb, tol);

        // pieces = connected regions of the art grown by `gap` px, so grass tufts and loose leaves within that
        // distance stay with their prop while props further apart are separate
        var solid = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) solid[i] = A(px[i]) > 0;
        info.Append("noise=" + DropSpecks(px, solid, W, H, 24) + " ");
        var grown = Dilate(solid, W, H, gap);
        var label = new int[px.Length];
        for (int i = 0; i < label.Length; i++) label[i] = -1;
        var gb = new Dictionary<int, int[]>(); // x0,y0,x1,y1,area(of real art)
        var stack = new Stack<int>();
        int ids = 0;
        for (int i = 0; i < px.Length; i++)
        {
            if (!grown[i] || label[i] >= 0) continue;
            int id = ids++; var bx = new[] { W, H, -1, -1, 0 };
            label[i] = id; stack.Push(i);
            while (stack.Count > 0)
            {
                int j = stack.Pop(), x = j % W, y = j / W;
                if (solid[j])
                {
                    bx[0] = Math.Min(bx[0], x); bx[1] = Math.Min(bx[1], y); bx[2] = Math.Max(bx[2], x); bx[3] = Math.Max(bx[3], y); bx[4]++;
                }
                if (x > 0 && grown[j - 1] && label[j - 1] < 0) { label[j - 1] = id; stack.Push(j - 1); }
                if (x < W - 1 && grown[j + 1] && label[j + 1] < 0) { label[j + 1] = id; stack.Push(j + 1); }
                if (y > 0 && grown[j - W] && label[j - W] < 0) { label[j - W] = id; stack.Push(j - W); }
                if (y < H - 1 && grown[j + W] && label[j + W] < 0) { label[j + W] = id; stack.Push(j + W); }
            }
            gb[id] = bx;
        }
        Func<int, int> find = k => k;

        var groups = new List<KeyValuePair<int, int[]>>();
        foreach (var kv in gb) if (kv.Value[4] >= minArea) groups.Add(kv);
        // reading order: rows (by top, 60px bands), then left to right
        groups.Sort((p, q) => { int r = (p.Value[1] / 60).CompareTo(q.Value[1] / 60); return r != 0 ? r : p.Value[0].CompareTo(q.Value[0]); });

        const int pad = 2;
        int n = 0;
        foreach (var kv in groups)
        {
            int[] g = kv.Value;
            int x0 = Math.Max(0, g[0] - pad), y0 = Math.Max(0, g[1] - pad), x1 = Math.Min(W - 1, g[2] + pad), y1 = Math.Min(H - 1, g[3] + pad);
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var o = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y0 + y) * W + x0 + x;
                    if (label[i] >= 0 && find(label[i]) == kv.Key) o[y * w + x] = px[i];
                }
            string name = dstPrefix + "_" + n.ToString("00") + ".png";
            Save(o, w, h, name);
            info.Append(n.ToString("00") + ":" + x0 + "," + y0 + "," + w + "x" + h + " ");
            n++;
        }
        return "pieces=" + n + " " + info.ToString();
    }

    // Off-key noise left in the background (a few px each) would bridge separate props once the mask is grown.
    static int DropSpecks(int[] px, bool[] solid, int W, int H, int maxArea)
    {
        var seen = new bool[px.Length];
        var comp = new List<int>();
        var stack = new Stack<int>();
        int dropped = 0;
        for (int i = 0; i < px.Length; i++)
        {
            if (!solid[i] || seen[i]) continue;
            comp.Clear(); seen[i] = true; stack.Push(i);
            while (stack.Count > 0)
            {
                int j = stack.Pop(), x = j % W, y = j / W;
                comp.Add(j);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int k = ny * W + nx;
                        if (solid[k] && !seen[k]) { seen[k] = true; stack.Push(k); }
                    }
            }
            if (comp.Count > maxArea) continue;
            foreach (int j in comp) { solid[j] = false; px[j] = 0; }
            dropped++;
        }
        return dropped;
    }

    static bool[] Dilate(bool[] m, int W, int H, int r)
    {
        if (r <= 0) return (bool[])m.Clone();
        var h = new bool[m.Length];
        for (int y = 0; y < H; y++)
        {
            int last = -100000;
            for (int x = 0; x < W; x++) { if (m[y * W + x]) last = x; if (x - last <= r) h[y * W + x] = true; }
            last = 100000;
            for (int x = W - 1; x >= 0; x--) { if (m[y * W + x]) last = x; if (last - x <= r) h[y * W + x] = true; }
        }
        var v = new bool[m.Length];
        for (int x = 0; x < W; x++)
        {
            int last = -100000;
            for (int y = 0; y < H; y++) { if (h[y * W + x]) last = y; if (y - last <= r) v[y * W + x] = true; }
            last = 100000;
            for (int y = H - 1; y >= 0; y--) { if (h[y * W + x]) last = y; if (last - y <= r) v[y * W + x] = true; }
        }
        return v;
    }

    // The AI sometimes draws 1-3px key-coloured lines between tile cells. Find columns/rows where key pixels sit
    // between art on both sides along most of a run, and paint them with the neighbouring art colour.
    static string FillGridLines(int[] px, int W, int H, int kr, int kg, int kb, int tol)
    {
        Func<int, int, bool> key = (x, y) => IsKey(px[y * W + x], kr, kg, kb, tol + 60) || KeyTint(px[y * W + x], kr, kg, kb);
        var cols = new List<int>(); var rows = new List<int>();
        for (int x = 4; x < W - 4; x++)
        {
            int c = 0;
            for (int y = 0; y < H; y++)
                if (key(x, y) && !key(x - 4, y) && !key(x + 4, y)) c++;
            if (c >= 40) cols.Add(x);
        }
        for (int y = 4; y < H - 4; y++)
        {
            int c = 0;
            for (int x = 0; x < W; x++)
                if (key(x, y) && !key(x, y - 4) && !key(x, y + 4)) c++;
            if (c >= 40) rows.Add(y);
        }
        // A line is the key pixel plus 1-2px of dark purple where it was blended into the art, so repaint a band
        // around each run of detected positions by interpolating between the art just outside the band.
        int filled = 0;
        var colBands = Bands(cols); var rowBands = Bands(rows);
        foreach (var bd in colBands)
        {
            int l = bd[0] - 3, r = bd[1] + 3;
            if (l < 0 || r >= W) continue;
            for (int y = 0; y < H; y++)
            {
                if (key(l, y) || key(r, y)) continue;
                bool onLine = false;
                for (int x = bd[0]; x <= bd[1]; x++) if (key(x, y)) onLine = true;
                if (!onLine) continue;
                int a = px[y * W + l], b = px[y * W + r];
                for (int x = l + 1; x < r; x++) { px[y * W + x] = Mix(a, b, (x - l) / (float)(r - l)) | unchecked((int)0xFF000000); filled++; }
            }
        }
        foreach (var bd in rowBands)
        {
            int u = bd[0] - 3, d = bd[1] + 3;
            if (u < 0 || d >= H) continue;
            for (int x = 0; x < W; x++)
            {
                if (key(x, u) || key(x, d)) continue;
                bool onLine = false;
                for (int y = bd[0]; y <= bd[1]; y++) if (key(x, y)) onLine = true;
                if (!onLine) continue;
                int a = px[u * W + x], b = px[d * W + x];
                for (int y = u + 1; y < d; y++) { px[y * W + x] = Mix(a, b, (y - u) / (float)(d - u)) | unchecked((int)0xFF000000); filled++; }
            }
        }
        return "gridCols=" + colBands.Count + " gridRows=" + rowBands.Count + " filled=" + filled + " ";
    }

    static List<int[]> Bands(List<int> pos)
    {
        var res = new List<int[]>();
        foreach (int p in pos)
        {
            if (res.Count > 0 && p - res[res.Count - 1][1] <= 2) res[res.Count - 1][1] = p;
            else res.Add(new[] { p, p });
        }
        return res;
    }
}
