using System;
using System.IO;
using System.Text;
using ProtoBuf;
using Xunit;

namespace ProtoPubSub.Tests
{
    public enum SignalEnum
    {
        Connect = 1,
        Message = 2,
        Disconnect = 3
    }

    public class ProtoTests
    {
        [Fact]
        public void Magic()
        {
            for (short i = 0; i < 1000; i++)
            {
                var bytes = Bytes(i);
                Console.WriteLine(bytes.Length + " " + bytes.ToHex() + " (" + i +")");
            }
        }

        private static byte[] Bytes(short signal)
        {
            var header = new ProtocolHeader { Magic = ProtocolHeader.ProtoIo, Signal = signal };

            var stream = new MemoryStream();
            Serializer.SerializeWithLengthPrefix(stream, header, PrefixStyle.Base128);
            var bytes = stream.ToArray();
            return bytes;
        }

        [Fact]
        public void EnumOutOfRange()
        {
            Console.WriteLine(Enum.IsDefined(typeof(SignalEnum), 1));
            Console.WriteLine(Enum.IsDefined(typeof(SignalEnum), 99));
        }
    }

    public static class HexString
    {
        // http://blogs.msdn.com/b/blambert/archive/2009/02/22/blambert-codesnip-fast-byte-array-to-hex-string-conversion.aspx
        static readonly string[] HexStringTable =
        {
            "00", "01", "02", "03", "04", "05", "06", "07", "08", "09", "0A", "0B", "0C", "0D", "0E", "0F",
            "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "1A", "1B", "1C", "1D", "1E", "1F",
            "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "2A", "2B", "2C", "2D", "2E", "2F",
            "30", "31", "32", "33", "34", "35", "36", "37", "38", "39", "3A", "3B", "3C", "3D", "3E", "3F",
            "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "4A", "4B", "4C", "4D", "4E", "4F",
            "50", "51", "52", "53", "54", "55", "56", "57", "58", "59", "5A", "5B", "5C", "5D", "5E", "5F",
            "60", "61", "62", "63", "64", "65", "66", "67", "68", "69", "6A", "6B", "6C", "6D", "6E", "6F",
            "70", "71", "72", "73", "74", "75", "76", "77", "78", "79", "7A", "7B", "7C", "7D", "7E", "7F",
            "80", "81", "82", "83", "84", "85", "86", "87", "88", "89", "8A", "8B", "8C", "8D", "8E", "8F",
            "90", "91", "92", "93", "94", "95", "96", "97", "98", "99", "9A", "9B", "9C", "9D", "9E", "9F",
            "A0", "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "AA", "AB", "AC", "AD", "AE", "AF",
            "B0", "B1", "B2", "B3", "B4", "B5", "B6", "B7", "B8", "B9", "BA", "BB", "BC", "BD", "BE", "BF",
            "C0", "C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9", "CA", "CB", "CC", "CD", "CE", "CF",
            "D0", "D1", "D2", "D3", "D4", "D5", "D6", "D7", "D8", "D9", "DA", "DB", "DC", "DD", "DE", "DF",
            "E0", "E1", "E2", "E3", "E4", "E5", "E6", "E7", "E8", "E9", "EA", "EB", "EC", "ED", "EE", "EF",
            "F0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "FA", "FB", "FC", "FD", "FE", "FF"
        };

        /// <summary>
        /// Returns a hex string representation of an array of bytes.
        /// </summary>
        /// <param name="value">The array of bytes.</param>
        /// <returns>A hex string representation of the array of bytes.</returns>
        public static string ToHex(this byte[] value)
        {
            if (value == null) return null;

            var stringBuilder = new StringBuilder();

            foreach (byte b in value)
            {
                stringBuilder.Append(HexStringTable[b]);
            }

            return stringBuilder.ToString();
        }

        // http://stackoverflow.com/questions/623104/byte-to-hex-string/5919521#5919521
        static readonly int[] HexByteTable =
            { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,
              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
              0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };

        public static byte[] HexToBytes(this string hex)
        {
            if (hex == null) return null;

            var bytes = new byte[hex.Length / 2];
            
            for (int x = 0, i = 0; i < hex.Length; i += 2, x += 1)
            {
                bytes[x] = (byte)(HexByteTable[Char.ToUpper(hex[i + 0]) - '0'] << 4 |
                                  HexByteTable[Char.ToUpper(hex[i + 1]) - '0']);
            }

            return bytes;
        }
    }
}