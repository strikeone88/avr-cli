
using System;
using System.IO;

namespace mcutils
{
    public class IntelHex
    {
        protected static string hexDigits = "0123456789ABCDEF";

        protected static byte[] buffer = new byte[262144];

        public static int ByteValue (string str, int index)
        {
            return hexDigits.IndexOf(str.Substring(index, 1))*16 + hexDigits.IndexOf(str.Substring(index+1, 1));
        }

        public static int WordValue (string str, int index)
        {
            return ByteValue(str, index)*256 + ByteValue(str, index+2);
        }

        public static byte[] Load (string filename)
        {
            if (Path.GetExtension(filename) == "")
                filename += ".hex";

            if (!File.Exists(filename))
                return null;

            StreamReader sr = new StreamReader (filename, false);
            string line;
            int size = 0;

            while ((line = sr.ReadLine()) != null)
            {
                if (!line.StartsWith(":"))
                    continue;

                int length = ByteValue(line, 1);
                int offset = WordValue(line, 3);
                int type = ByteValue(line, 7);
                int checksum = ByteValue(line, line.Length-2);

                if (type == 1)
                    break;

                if (type != 0)
                {
                    Console.WriteLine("Unknown line code: " + type);

                    sr.Close();
                    return null;
                }

                int sum = length + type + (offset & 255) + (offset >> 8);
                size += length;

                for (int i = 0; i < length; i++)
                {
                    int value = ByteValue(line, 2*i+9);
                    sum += value;

                    buffer[offset++] = (byte)value;
                }

                if (((sum + checksum) & 255) != 0)
                {
                    Console.WriteLine("Checksum failed");

                    sr.Close();
                    return null;
                }
            }

            sr.Close();

            byte[] data = new byte[size];

            for (int i = 0; i < size; i++)
                data[i] = buffer[i];

            return data;
        }

        public static void Save (string filename, byte[] data, int blockSize)
        {
            if (Path.GetExtension(filename) == "")
                filename += ".hex";

            if (File.Exists(filename))
                File.Delete(filename);

            StreamWriter sw = new StreamWriter (filename, false);
            string line;

            int bytesLeft = data.Length;
            int numBytes;
            int offset = 0;
            int checksum;

            while (bytesLeft > 0)
            {
                numBytes = bytesLeft > blockSize ? blockSize : bytesLeft;

                line = ":" + numBytes.ToString("X2") + offset.ToString("X4") + "00";

                checksum = numBytes + (offset & 255) + (offset >> 8);

                for (int i = 0; i < numBytes; i++)
                {
                    line += data[offset+i].ToString("X2");
                    checksum += data[offset+i];
                }

                line += (-checksum & 255).ToString("X2");

                offset += numBytes;
                bytesLeft -= numBytes;

                sw.WriteLine(line);
            }

            sw.WriteLine(":00000001FF");

            sw.Close();
        }
    }
}
