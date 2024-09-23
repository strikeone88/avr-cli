
using System;

namespace mcutils
{
    public interface IPortIO
    {
        int GetBasePort ();
        void WriteByte (int value);
        int ReadByte ();
    }
}
