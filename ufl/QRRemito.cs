// QRRemito.cs - Funcion propia (UFL) para Crystal Reports XI R2
// Version 0.3: agrega la generacion de codigos QR.
//
// Funciones que aparecen en Crystal (Funciones adicionales -> UFLs de Visual Basic):
//   QRRemitoMainProbar(texto)     -> devuelve "OK-UFL: " + texto (prueba)
//   QRRemitoMainGenerarQR(texto)  -> genera un .bmp con el QR del texto y
//                                    devuelve la ruta del archivo (o "" si falla)
//
// Crystal busca componentes cuyo ProgID empiece con "CRUFL".
// Nombre en Crystal = ProgID sin "CRUFL" + clase + metodo.
//
// Codificador QR propio (modo byte, correccion de errores nivel M), escrito
// siguiendo la especificacion ISO/IEC 18004 y basado en la estructura del
// generador de codigo abierto de Project Nayuki (licencia MIT).
// No depende de librerias externas ni de System.Drawing.
// Compatible con el compilador C# 5 que trae Windows (csc.exe de .NET 4.x).

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CRUFLQRRemito
{
    [ComVisible(true)]
    [Guid("6E3B2C1A-4F7D-4B8E-9A21-5C0D7E8F9A02")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IMain
    {
        [DispId(1)]
        string Probar(string texto);

        [DispId(2)]
        string GenerarQR(string texto);
    }

    [ComVisible(true)]
    [Guid("6E3B2C1A-4F7D-4B8E-9A21-5C0D7E8F9A01")]
    [ProgId("CRUFLQRRemito.Main")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(IMain))]
    public class Main : IMain
    {
        public string Probar(string texto)
        {
            return "OK-UFL: " + texto;
        }

        // Genera (o reutiliza) un BMP con el QR del texto y devuelve su ruta.
        // Nunca lanza errores hacia Crystal: si algo falla devuelve "" y deja un log.
        public string GenerarQR(string texto)
        {
            try
            {
                if (texto == null || texto.Length == 0) return "";
                string carpeta = Path.Combine(Path.GetTempPath(), "QRRemito");
                Directory.CreateDirectory(carpeta);
                LimpiarViejos(carpeta);
                string ruta = Path.Combine(carpeta, "qr_" + Hash(texto) + ".bmp");
                if (!File.Exists(ruta))
                {
                    bool[,] matriz = QrEncoder.Encode(Encoding.UTF8.GetBytes(texto));
                    string tmp = ruta + ".tmp";
                    BmpWriter.Write(tmp, matriz, 8, 4);
                    if (File.Exists(ruta)) File.Delete(tmp); else File.Move(tmp, ruta);
                }
                return ruta;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return "";
            }
        }

        private static string Hash(string s)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 10; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        // Borra los QR de mas de 2 dias para que la carpeta no crezca sin limite.
        private static void LimpiarViejos(string carpeta)
        {
            try
            {
                DateTime limite = DateTime.Now.AddDays(-2);
                foreach (string f in Directory.GetFiles(carpeta, "qr_*.bmp"))
                {
                    if (File.GetLastWriteTime(f) < limite) File.Delete(f);
                }
            }
            catch { }
        }

        private static void Log(string msg)
        {
            try
            {
                string f = Path.Combine(Path.GetTempPath(), "QRRemito_error.log");
                File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------------
    // Escritor de BMP de 24 bits (negro sobre blanco), sin System.Drawing.
    // ---------------------------------------------------------------------
    public static class BmpWriter
    {
        public static void Write(string path, bool[,] m, int escala, int borde)
        {
            int n = m.GetLength(0);
            int w = (n + borde * 2) * escala;
            int h = w;
            int rowSize = ((w * 3) + 3) & ~3;
            int dataSize = rowSize * h;
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((byte)'B'); bw.Write((byte)'M');
                bw.Write(54 + dataSize); bw.Write(0); bw.Write(54);
                bw.Write(40); bw.Write(w); bw.Write(h);
                bw.Write((short)1); bw.Write((short)24); bw.Write(0); bw.Write(dataSize);
                bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
                byte[] row = new byte[rowSize];
                for (int py = h - 1; py >= 0; py--)
                {
                    int my = py / escala - borde;
                    for (int px = 0; px < w; px++)
                    {
                        int mx = px / escala - borde;
                        bool oscuro = mx >= 0 && my >= 0 && mx < n && my < n && m[my, mx];
                        byte c = oscuro ? (byte)0 : (byte)255;
                        row[px * 3] = c; row[px * 3 + 1] = c; row[px * 3 + 2] = c;
                    }
                    bw.Write(row);
                }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Codificador QR (modo byte, nivel de correccion M, versiones 1 a 40).
    // ---------------------------------------------------------------------
    public static class QrEncoder
    {
        // Tablas para nivel M, indice = version (0 sin uso).
        private static readonly int[] EccPorBloque = {
            -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26,
            26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 };
        private static readonly int[] CantBloques = {
            -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16,
            17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 };
        private const int EccFormatBitsM = 0;

        private static int size;
        private static bool[,] mods;
        private static bool[,] isFunc;

        public static bool[,] Encode(byte[] data)
        {
            lock (typeof(QrEncoder))
            {
                int version = 0;
                int capBits = 0;
                for (int v = 1; v <= 40; v++)
                {
                    capBits = DataCodewords(v) * 8;
                    int ccBits = v <= 9 ? 8 : 16;
                    if (4 + ccBits + data.Length * 8 <= capBits) { version = v; break; }
                }
                if (version == 0) throw new ArgumentException("Texto demasiado largo para un QR.");

                // Bits de datos: modo byte (0100) + longitud + bytes + terminador + relleno.
                List<bool> bits = new List<bool>();
                AppendBits(bits, 4, 4);
                AppendBits(bits, data.Length, version <= 9 ? 8 : 16);
                foreach (byte b in data) AppendBits(bits, b, 8);
                AppendBits(bits, 0, Math.Min(4, capBits - bits.Count));
                AppendBits(bits, 0, (8 - bits.Count % 8) % 8);
                for (int pad = 0xEC; bits.Count < capBits; pad ^= 0xEC ^ 0x11) AppendBits(bits, pad, 8);

                byte[] dataCw = new byte[bits.Count / 8];
                for (int i = 0; i < bits.Count; i++)
                    if (bits[i]) dataCw[i >> 3] |= (byte)(1 << (7 - (i & 7)));

                byte[] allCw = AddEccAndInterleave(dataCw, version);

                size = version * 4 + 17;
                mods = new bool[size, size];
                isFunc = new bool[size, size];
                DrawFunctionPatterns(version);
                DrawCodewords(allCw);

                int mejorMask = 0;
                long mejorPenal = long.MaxValue;
                for (int mask = 0; mask < 8; mask++)
                {
                    ApplyMask(mask);
                    DrawFormatBits(mask);
                    long p = Penalty();
                    if (p < mejorPenal) { mejorPenal = p; mejorMask = mask; }
                    ApplyMask(mask); // deshace (XOR)
                }
                ApplyMask(mejorMask);
                DrawFormatBits(mejorMask);
                return (bool[,])mods.Clone();
            }
        }

        private static void AppendBits(List<bool> bits, int val, int len)
        {
            for (int i = len - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0);
        }

        private static int RawDataModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7) result -= 36;
            }
            return result;
        }

        private static int DataCodewords(int ver)
        {
            return RawDataModules(ver) / 8 - EccPorBloque[ver] * CantBloques[ver];
        }

        private static byte[] AddEccAndInterleave(byte[] data, int ver)
        {
            int numBlocks = CantBloques[ver];
            int blockEccLen = EccPorBloque[ver];
            int rawCodewords = RawDataModules(ver) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLen = rawCodewords / numBlocks;

            byte[] divisor = RsDivisor(blockEccLen);
            List<byte[]> blocks = new List<byte[]>();
            int k = 0;
            for (int i = 0; i < numBlocks; i++)
            {
                int datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                byte[] dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;
                byte[] ecc = RsRemainder(dat, divisor);
                byte[] block = new byte[shortBlockLen + 1];
                Array.Copy(dat, 0, block, 0, datLen);
                // En bloques cortos queda un byte vacio antes del ECC (se saltea al intercalar).
                Array.Copy(ecc, 0, block, shortBlockLen + 1 - blockEccLen, blockEccLen);
                blocks.Add(block);
            }

            List<byte> result = new List<byte>();
            for (int i = 0; i < shortBlockLen + 1; i++)
            {
                for (int j = 0; j < blocks.Count; j++)
                {
                    if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                        result.Add(blocks[j][i]);
                }
            }
            return result.ToArray();
        }

        private static byte[] RsDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = (byte)RsMul(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = RsMul(root, 0x02);
            }
            return result;
        }

        private static byte[] RsRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            foreach (byte b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++)
                    result[i] ^= (byte)RsMul(divisor[i], factor);
            }
            return result;
        }

        private static int RsMul(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return z & 0xFF;
        }

        // m[y, x]
        private static void SetFunc(int x, int y, bool dark)
        {
            mods[y, x] = dark;
            isFunc[y, x] = true;
        }

        private static void DrawFunctionPatterns(int ver)
        {
            for (int i = 0; i < size; i++)
            {
                SetFunc(6, i, i % 2 == 0);
                SetFunc(i, 6, i % 2 == 0);
            }
            DrawFinder(3, 3);
            DrawFinder(size - 4, 3);
            DrawFinder(3, size - 4);

            int[] pos = AlignmentPositions(ver);
            int n = pos.Length;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (!((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0)))
                        DrawAlignment(pos[i], pos[j]);

            DrawFormatBits(0); // reserva el lugar (se redibuja despues)
            DrawVersion(ver);
        }

        private static void DrawFinder(int x, int y)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (0 <= xx && xx < size && 0 <= yy && yy < size)
                        SetFunc(xx, yy, dist != 2 && dist != 4);
                }
            }
        }

        private static void DrawAlignment(int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    SetFunc(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        private static int[] AlignmentPositions(int ver)
        {
            if (ver == 1) return new int[0];
            int numAlign = ver / 7 + 2;
            int step = (ver == 32) ? 26 : (ver * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
            int[] result = new int[numAlign];
            result[0] = 6;
            for (int i = result.Length - 1, p = size - 7; i >= 1; i--, p -= step)
                result[i] = p;
            return result;
        }

        private static void DrawFormatBits(int mask)
        {
            int data = EccFormatBitsM << 3 | mask;
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;

            for (int i = 0; i <= 5; i++) SetFunc(8, i, Bit(bits, i));
            SetFunc(8, 7, Bit(bits, 6));
            SetFunc(8, 8, Bit(bits, 7));
            SetFunc(7, 8, Bit(bits, 8));
            for (int i = 9; i < 15; i++) SetFunc(14 - i, 8, Bit(bits, i));

            for (int i = 0; i < 8; i++) SetFunc(size - 1 - i, 8, Bit(bits, i));
            for (int i = 8; i < 15; i++) SetFunc(8, size - 15 + i, Bit(bits, i));
            SetFunc(8, size - 8, true);
        }

        private static void DrawVersion(int ver)
        {
            if (ver < 7) return;
            int rem = ver;
            for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = ver << 12 | rem;
            for (int i = 0; i < 18; i++)
            {
                bool bit = Bit(bits, i);
                int a = size - 11 + i % 3;
                int b = i / 3;
                SetFunc(a, b, bit);
                SetFunc(b, a, bit);
            }
        }

        private static bool Bit(int x, int i)
        {
            return ((x >> i) & 1) != 0;
        }

        private static void DrawCodewords(byte[] data)
        {
            int i = 0;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!isFunc[y, x] && i < data.Length * 8)
                        {
                            mods[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
                }
            }
        }

        private static void ApplyMask(int mask)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        default: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                    }
                    if (invert && !isFunc[y, x]) mods[y, x] = !mods[y, x];
                }
            }
        }

        // Puntaje de penalidad (reglas N1 a N4 de la norma) para elegir la mejor mascara.
        private static long Penalty()
        {
            long result = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int a = 0; a < size; a++)
                {
                    int run = 1;
                    for (int b = 1; b < size; b++)
                    {
                        bool cur = pass == 0 ? mods[a, b] : mods[b, a];
                        bool prev = pass == 0 ? mods[a, b - 1] : mods[b - 1, a];
                        if (cur == prev)
                        {
                            run++;
                            if (run == 5) result += 3;
                            else if (run > 5) result++;
                        }
                        else run = 1;
                    }
                    // patron tipo localizador 1:1:3:1:1 con 4 claros a un lado
                    for (int b = 0; b + 10 < size; b++)
                    {
                        bool[] s = new bool[11];
                        for (int t = 0; t < 11; t++) s[t] = pass == 0 ? mods[a, b + t] : mods[b + t, a];
                        if (Patron(s, true) || Patron(s, false)) result += 40;
                    }
                }
            }
            for (int y = 0; y < size - 1; y++)
                for (int x = 0; x < size - 1; x++)
                {
                    bool c = mods[y, x];
                    if (c == mods[y, x + 1] && c == mods[y + 1, x] && c == mods[y + 1, x + 1]) result += 3;
                }
            int dark = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (mods[y, x]) dark++;
            int total = size * size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += k * 10;
            return result;
        }

        private static bool Patron(bool[] s, bool clarosDespues)
        {
            // 1 0 1 1 1 0 1 + 0 0 0 0   (o 0 0 0 0 + 1 0 1 1 1 0 1)
            bool[] p = { true, false, true, true, true, false, true };
            int off = clarosDespues ? 0 : 4;
            for (int i = 0; i < 7; i++) if (s[off + i] != p[i]) return false;
            int lo = clarosDespues ? 7 : 0;
            for (int i = 0; i < 4; i++) if (s[lo + i]) return false;
            return true;
        }
    }
}
