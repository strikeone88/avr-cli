
using System;
using System.Runtime.InteropServices;

namespace mcutils
{
    public class ParallelPort : IPortIO
    {
        [DllImport("inpout32.dll")]
        public static extern int Inp32 (int addr);

        [DllImport("inpout32.dll")]
        public static extern void Out32 (int addr, int val);

        private int basePort;

        public ParallelPort (int basePort)
        {
            this.basePort = basePort;
        }

        public int GetBasePort ()
        {
            return basePort;
        }

        public void WriteByte (int value)
        {
            Out32 (basePort, value & 0xFF);
        }

        public int ReadByte ()
        {
            return Inp32 (basePort+1) & 0xFF;
        }
    }
}
