using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace IconViewer
{
    [StructLayout(LayoutKind.Sequential)]
    struct ICOIconDir
    {
        public ushort idReserved;
        public ushort idType;
        public ushort idCount;
        public ICOIconDirEntry[] idEntries;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ICOIconDirEntry
    {
        public byte bWidth;
        public byte bHeight;
        public byte bColorCount;
        public byte bReserved;
        public ushort wPlanes;
        public ushort wBitCount;
        public uint dwBytesInRes;
        public uint dwImageOffset;
        public byte[] iconData;
    }
}
