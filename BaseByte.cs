using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;

namespace BaseByte
{
    class BaseByteClass
    {
        public int CompareBytes(byte[] Data1, byte[] Data2)
        {
            int i, L1 = Data1.Length, L2 = Data2.Length;
            
            if (L1 > L2)
                return 1;

            if (L1 < L2)
                return -1;
            for (i = 0; i < L1; ++i)
            {
                if (Data1[i] > Data2[i])
                    return 1;
                if (Data1[i] < Data2[i])
                    return -1;
            }
            return 0;
        }

        public string Byte2String(byte[] Data)
        {
            return Byte2String(Data, false);
        }

        public string Byte2String(byte[] Data, bool ThinOut)
        {
            string sR = "";
            foreach (byte B in Data)
            {
                sR += string.Format("{0:X2}", B);
                if (ThinOut)
                    sR += " ";
            }
            return sR;
        }

        public int LoadDumpFromFile(string FileName, out byte[] Buffer)
        {
            string sDump;
            if (!File.Exists(FileName))
            {
                Buffer = new byte[0];
                return -1;
            }
            StreamReader sr = new StreamReader(FileName);
            sDump = sr.ReadToEnd();
            sr.Close();
            return String2Byte(sDump, out Buffer);
        }

        public bool IsHexChar(string S)
        {
            string sB;
            sB = S.Substring(0, 1).ToUpper();
            switch (sB)
            {
                case "0":
                case "1":
                case "2":
                case "3":
                case "4":
                case "5":
                case "6":
                case "7":
                case "8":
                case "9": return true;
                case "A":
                case "B":
                case "C":
                case "D":
                case "E":
                case "F": return true;
            }
            return false;
        }

        public int String2Byte(string sDump, out byte[] Data)
        {
            int i, N, K;
            byte cB;
            byte[] bB;
            string sB, sH = "";


            bB = new byte[sDump.Length];
            for (i = 0, N = 0, K = 0; i < sDump.Length; ++i)
            {
                sB = sDump.Substring(i, 1);
                if (!IsHexChar(sB))
                    continue;
                if (sB == "0" && (i + 1) < sDump.Length)
                {
                    string sX = sDump.Substring(i + 1, 1).ToLower();
                    if (sX == "x")
                        continue;
                }
                if (K == 0)
                {
                    cB = 0x00;
                    sH = sB;
                    K = 1;
                }
                else
                {
                    sH += sB;
                    if (!byte.TryParse(sH, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cB))
                        cB = 0x00;
                    bB[N] = cB;
                    N++;
                    K = 0;
                }
            }
            Data = new byte[N];
            for (i = 0; i < N; ++i)
                Data[i] = bB[i];
            return N;
        }

        public byte[] CutTrail(byte[] bSource)
        {
            int i, N;
            byte[] bResult;

            for (N = bSource.Length - 1; N >= 0; --N)
                if (bSource[N] != 0x00)
                    break;
            if (N == bSource.Length - 1)
                return bSource;
            if (bSource[N] != 0x80)
                return bSource;
            bResult = new byte[N];
            for (i = 0; i < N; ++i)
                bResult[i] = bSource[i];
            return bResult;
        }

        public void Normalize(ref byte[] key)
        {
            byte i, mask, cnt;
            for (i = 0; i < key.Length; i++)
            {
                for (mask = 0x80, cnt = 0; mask > 0; mask >>= 1)
                    if ((key[i] & mask) > 0)
                        cnt++;
                if ((cnt & 1) == 0)
                {
                    if ((key[i] & 1) > 0)
                        key[i] &= 0xFE;
                    else key[i] |= 1;
                }
            }
        }

        public bool isNormalized(byte[] key)
        {
            byte i, mask, cnt;
            for (i = 0; i < key.Length; i++)
            {
                for (mask = 0x80, cnt = 0; mask > 0; mask >>= 1)
                    if ((key[i] & mask) > 0)
                        cnt++;
                if ((cnt & 1) == 0)
                    return false;
            }
            return true;
        }
    }
}
