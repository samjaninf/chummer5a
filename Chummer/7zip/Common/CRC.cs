/*  This file is part of Chummer5a.
 *
 *  Chummer5a is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  Chummer5a is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with Chummer5a.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  You can obtain the full source code for Chummer5a at
 *  https://github.com/chummer5a/chummer5a
 */
// Common/CRC.cs

namespace SevenZip
{
    internal class CRC
    {
        public static readonly uint[] Table;

        static CRC()
        {
            Table = new uint[256];
            const uint kPoly = 0xEDB88320;
            unchecked
            {
                for (uint i = 0; i < 256; i++)
                {
                    uint r = i;
                    for (int j = 0; j < 8; j++)
                        if ((r & 1) != 0)
                            r = (r >> 1) ^ kPoly;
                        else
                            r >>= 1;
                    Table[i] = r;
                }
            }
        }

        private uint _value = 0xFFFFFFFF;

        public void Init()
        { _value = 0xFFFFFFFF; }

        public void UpdateByte(byte b)
        {
            unchecked
            {
                _value = Table[(byte)_value ^ b] ^ (_value >> 8);
            }
        }

        public unsafe void Update(byte[] data, uint offset, uint size)
        {
            unchecked
            {
                fixed (byte* pchrData = &data[offset])
                {
                    for (uint i = 0; i < size; i++)
                        _value = Table[(byte)_value ^ *(pchrData + i)] ^ (_value >> 8);
                }
            }
        }

        public uint GetDigest()
        { return _value ^ 0xFFFFFFFF; }

        private static uint CalculateDigest(byte[] data, uint offset, uint size)
        {
            CRC crc = new CRC();
            // crc.Init();
            crc.Update(data, offset, size);
            return crc.GetDigest();
        }

        private static bool VerifyDigest(uint digest, byte[] data, uint offset, uint size)
        {
            return CalculateDigest(data, offset, size) == digest;
        }
    }
}
