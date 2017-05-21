using System;
using System.Collections;
using System.Linq;

namespace Stribog
{
    /// <summary>
    /// Основной класс библиотеки. Содержит функции хеширования по ГОСТ 34.11-2012
    /// </summary>
    public class StribogObj
    {
        private byte[] N = new byte[64];
        private byte[] N0 = new byte[64];
        private byte[] N512 = BitConverter.GetBytes(512).Reverse().ToArray();
        private byte[] h = new byte[64];
        private byte[] Sigma = new byte[64];
        private byte[] m = new byte[64];

        /// <summary>
        /// Хеширует входную строку, предтавленную в виде массива байт, и возвращает хеш-код в виде строки символов, 
        /// приобразуя числовое значение каждого байта результирующего массива в эквивалентное ему шестнадцатиричное строековое представление
        /// </summary>
        /// <param name="message">Входная строка</param>
        /// <param name="_512">Если true - возвращает 512-разрадное число, иначе возвращает 256-разрадное число</param>
        /// <returns>Хеш код в виде строки символов</returns>
        public string GetHashStringFormat (byte[] message, bool _512)
        {
            byte[] hash = GetHash(message, _512);
            string result = BitConverter.ToString(hash);
            result = result.Replace("-", "");
            return result;
        }

        /// <summary>
        /// Хеширует входную строку, предтавленную в виде массива байт, и возвращает хеш-код в виде массива байт 
        /// </summary>
        /// <param name="message">Входная строка</param>
        /// <param name="_512">Если true - возвращает массив из 64 элементов, иначе возвращает массив из 32 элементов</param>
        /// <returns></returns>
        public byte[] GetHash(byte[] message, bool _512)
        {
            ////////////////////////////////
            // 1-й этап 
            ////////////////////////////////
            InitValues(!_512);

            ////////////////////////////////
            // 2-й этап 
            ////////////////////////////////
            int inc = 0;

            for (int len = message.Length; len >= 64; len -= 64)
            {
                inc++;
                byte[] tempMes = new byte[64];
                Array.Copy(message, message.Length - inc * 64, tempMes, 0, 64);
                h = G_n(N, h, tempMes);
                N = AddModulo512(N, N512);
                Sigma = AddModulo512(Sigma, tempMes);
            }

            ////////////////////////////////
            // 3-й этап 
            ////////////////////////////////
            byte[] M = new byte[message.Length - inc * 64];
            Array.Copy(message, 0, M, 0, message.Length - inc * 64);
            if (M.Length < 64)
            {
                for (int i = 0; i < (64 - M.Length - 1); i++)
                {
                    m[i] = 0;
                }
                m[64 - M.Length - 1] = 0x01;
                Array.Copy(M, 0, m, 64 - M.Length, M.Length);
            }
            h = G_n(N, h, m);
            byte[] MLen = BitConverter.GetBytes(M.Length * 8);
            N = AddModulo512(N, MLen.Reverse().ToArray());
            Sigma = AddModulo512(Sigma, m);
            h = G_n(N0, h, N);
            h = G_n(N0, h, Sigma);

            ////////////////////////////////
            // Получить результат
            ////////////////////////////////
            if (_512)
                return h;

            byte[] h256 = new byte[32];
            Array.Copy(h, 0, h256, 0, 32);
            return h256;
        }

        private void InitValues(bool _256)
        {
            h = new byte[64];
            byte ivValue = BitConverter.GetBytes(_256)[0];
            for (int i = 0; i < 64; i++)
            {
                h[i] = ivValue;
                N[i] = 0;
                N0[i] = 0;
                Sigma[i] = 0;
                m[i] = 0;
            }
        }

        private byte[] AddModulo512(byte[] a, byte[] b)
        {
            byte[] temp = new byte[64];
            int i = 0, t = 0;
            byte[] tempA = new byte[64];
            byte[] tempB = new byte[64];
            Array.Copy(a, 0, tempA, 64 - a.Length, a.Length);
            Array.Copy(b, 0, tempB, 64 - b.Length, b.Length);
            for (i = 63; i >= 0; i--)
            {
                t = tempA[i] + tempB[i] + (t >> 8);
                temp[i] = (byte)(t & 0xFF);
            }
            return temp;
        }

        #region Функции преобразования

        private byte[] X(byte[] a, byte[] b)
        {
            byte[] res = new byte[64];
            for (int i = 0; i < 64; i++)
                res[i] = (byte)(a[i] ^ b[i]);
            return res;
        }

        private byte[] S(byte[] a)
        {
            for (int i = 0; i < 64; i++)
                a[i] = Constants.Pi[a[i]];
            return a;
        }

        private byte[] P(byte[] a)
        {
            byte temp;
            for (int i = 0; i < 8; i++)
                for (int j = i + 1; j < 8; j++)
                {
                    temp = a[i * 8 + j];
                    a[i * 8 + j] = a[j * 8 + i];
                    a[j * 8 + i] = temp;
                }
            return a;
        }

        private byte[] L(byte[] a)
        {
            byte[] tempArray = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                ulong t = 0;
                Array.Copy(a, i * 8, tempArray, 0, 8);
                tempArray = tempArray.Reverse().ToArray();
                BitArray tempBits1 = new BitArray(tempArray);
                bool[] tempBits = new bool[64];
                tempBits1.CopyTo(tempBits, 0);
                tempBits = tempBits.Reverse().ToArray();
                for (int j = 0; j < 64; j++)
                {
                    if (tempBits[j] != false)
                        t = t ^ Constants.A[j];
                }
                byte[] ResPart = BitConverter.GetBytes(t).Reverse().ToArray();
                Array.Copy(ResPart, 0, a, i * 8, 8);
            }
            return a;
        }

        private byte[] SPL(byte[] a)
        {
            S(a);
            P(a);
            L(a);
            return a;
        }

        #endregion

        #region Функция сжатия

        private byte[] G_n(byte[] N, byte[] h, byte[] m)
        {
            byte[] K = X(h, N);
            K = SPL(K);
            byte[] t = E(K, m);
            t = X(t, h);
            byte[] newh = X(t, m);
            return newh;
        }

        private byte[] E(byte[] K, byte[] m)
        {
            byte[] step = X(K, m);
            for (int i = 0; i < 12; i++)
            {
                step = SPL(step);
                K = GetK(K, i);
                step = X(K, step);
            }
            return step;
        }

        private byte[] GetK(byte[] K, int i)
        {
            i = i * 64;
            for (int j = 0; j < 64; j++)
                K[j] = (byte)(K[j] ^ Constants.C[i + j]);

            return SPL(K);
        }

        #endregion

    }
}
