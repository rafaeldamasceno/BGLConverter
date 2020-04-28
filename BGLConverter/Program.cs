using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace BGLConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            const string fileName = "worldlc.bgl";
            if (File.Exists(fileName))
            {
                using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
                {
                    Console.WriteLine("---BEGIN HEADER---");
                    var buf = reader.ReadBytes(4); // magic number
                    if (BitConverter.ToString(buf) != "01-02-92-19") {
                        return;
                    }

                    var headerSize = reader.ReadInt32(); // header size, should be 0x38
                    Console.WriteLine("header size: {0}", headerSize);

                    FILETIME ft;
                    ft.dwLowDateTime = reader.ReadInt32(); // date
                    ft.dwHighDateTime = reader.ReadInt32(); // date

                    buf = reader.ReadBytes(4); // magic number
                    if (BitConverter.ToString(buf) != "03-18-05-08")
                    {
                        return;
                    }

                    var secNumber = reader.ReadInt32(); // sections after header
                    Console.WriteLine("number of sections: {0}", secNumber);

                    uint[] QMIDs = new uint[8]; // array defining bounding coordinates

                    for (int x = 0; x < QMIDs.Length; ++x)
                    {
                        QMIDs[x] = reader.ReadUInt32(); 
                    }

                    double minLat = double.MaxValue,
                        maxLat = double.MinValue,
                        minLong = double.MaxValue,
                        maxLong = double.MinValue;

                    foreach (var QMID in QMIDs)
                    {
                        if (QMID == 0)
                        {
                            break;
                        }

                        var boundingCoords = GetBoundingCoordinates(QMID);
                        
                        if (boundingCoords[0] < minLat) {
                            minLat = boundingCoords[0];
                        }
                        if (boundingCoords[1] > maxLat)
                        {
                            maxLat = boundingCoords[1];
                        }
                        if (boundingCoords[2] < minLong)
                        {
                            minLong = boundingCoords[2];
                        }
                        if (boundingCoords[3] > maxLong)
                        {
                            maxLong = boundingCoords[3];
                        }
                    }
                    Console.WriteLine("{0}, {1}, {2}, {3}", minLat, maxLat, minLong, maxLong);

                    Console.WriteLine("---END HEADER---");

                    for (int i = 0; i < secNumber; i++)
                    {
                        Console.WriteLine("---BEGIN SECTION---");
                        var secType = reader.ReadInt32(); // section type
                        if (secType == 0x68) // TerrainLandClass
                        {
                            Console.WriteLine("TerrainLandClass section");
                        } else if (secType == 0x6E) {
                            Console.WriteLine("TerrainIndex section");
                        } else
                        {
                            return;
                        }

                        var subsecSize = reader.ReadInt32(); // subsection size
                        subsecSize = ((subsecSize & 0x10000) | 0x40000) >> 0x0E; // size compute formula
                        Console.WriteLine("subsection size: {0}", subsecSize);

                        var subsecNumber = reader.ReadInt32(); // number of subsections
                        Console.WriteLine("number of subsections: {0}", subsecNumber);

                        var fileOffset = reader.ReadInt32(); // file offset
                        Console.WriteLine("file offset: {0}", fileOffset);

                        var totalSecSize = reader.ReadInt32(); // total size
                        Console.WriteLine("total section size: {0}", totalSecSize);

                        var secPos = reader.BaseStream.Position;

                        reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

                        for (int j = 0; j < subsecNumber; j++)
                        {
                            Console.WriteLine("---BEGIN SUBSECTION---");

                            QMIDs = new uint[2];

                            for (int x = 0; x < QMIDs.Length; ++x)
                            {
                                QMIDs[x] = reader.ReadUInt32();
                            }

                            minLat = double.MaxValue;
                            maxLat = double.MinValue;
                            minLong = double.MaxValue;
                            maxLong = double.MinValue;

                            foreach (var QMID in QMIDs)
                            {
                                if (QMID == 0)
                                {
                                    break;
                                }

                                var boundingCoords = GetBoundingCoordinates(QMID);

                                if (boundingCoords[0] < minLat)
                                {
                                    minLat = boundingCoords[0];
                                }
                                if (boundingCoords[1] > maxLat)
                                {
                                    maxLat = boundingCoords[1];
                                }
                                if (boundingCoords[2] < minLong)
                                {
                                    minLong = boundingCoords[2];
                                }
                                if (boundingCoords[3] > maxLong)
                                {
                                    maxLong = boundingCoords[3];
                                }
                            }
                            Console.WriteLine("{0}, {1}, {2}, {3}", minLat, maxLat, minLong, maxLong);

                            fileOffset = reader.ReadInt32();
                            Console.WriteLine("file offset: {0}", fileOffset);

                            var subsecDataSize = reader.ReadInt32();
                            Console.WriteLine("subsection data size: {0}", subsecDataSize);

                            Console.WriteLine("---BEGIN SUBSECTION DATA---");

                            var subsecPos = reader.BaseStream.Position;

                            reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

                            buf = reader.ReadBytes(4);

                            if (BitConverter.ToString(buf) != "54-52-51-31")
                            {
                                return;
                            }

                            var recordSize = reader.ReadInt32();
                            Console.WriteLine("record size: {0}", recordSize);

                            var parentSecID = reader.ReadInt16();
                            Console.WriteLine("parent section ID: {0}", parentSecID);

                            var compressionTypeFst = Convert.ToInt32(reader.ReadChar());
                            Console.WriteLine("compression type first: {0}", compressionTypeFst);

                            var compressionTypeSnd = Convert.ToInt32(reader.ReadChar());
                            Console.WriteLine("compression type second: {0}", compressionTypeSnd);

                            reader.ReadBytes(8);

                            var monthMask = reader.ReadBytes(4);
                            Console.WriteLine("month mask: {0}", BitConverter.ToString(monthMask));

                            var numRows = reader.ReadInt32();
                            Console.WriteLine("number of rows: {0}", numRows);

                            var numCols = reader.ReadInt32();
                            Console.WriteLine("number of columns: {0}", numCols);

                            var dataSize = reader.ReadInt32();
                            Console.WriteLine("data size: {0}", dataSize);

                            var maskSize = reader.ReadInt32();
                            Console.WriteLine("mask size: {0}", maskSize);

                            buf = reader.ReadBytes(dataSize);

                            switch(compressionTypeFst)
                            {
                                case 1:
                                    var dataLZ77 = new LZ1().Decompress(buf, numRows * numCols);
                                    break;
                                case 6:
                                    var dataBitpack = new Bitpack().Decompress(buf, numRows * numCols, numRows, numCols);
                                    break;
                                default:
                                    Console.WriteLine("compression algorithm unavailable");
                                    break;
                            }

                            Console.WriteLine("---END SUBSECTION DATA---");
                            Console.WriteLine("---END SUBSECTION---");

                            reader.BaseStream.Seek(subsecPos, SeekOrigin.Begin);
                        }

                        Console.WriteLine("---END SECTION---");

                        reader.BaseStream.Seek(secPos, SeekOrigin.Begin);
                    }
                }

            }
        }

        public static List<double> GetBoundingCoordinates(uint boundingValue)
        {
            var list = new List<double>();
            var shiftValue = 15;
            var work = boundingValue;
            var latitudeData = (uint)0;
            var longitudeData = (uint)0;

            while (work < 0x80000000 && shiftValue >= 0)
            {
                shiftValue--;
                work *= 4;
            }
            work &= 0x7FFFFFFF;    // Remove negative flag, if any
            var powerOfTwo = shiftValue;

            while (shiftValue >= 0)
            {
                if ((work & 0x80000000) != 0)
                {
                    latitudeData += (uint)(1 << shiftValue);
                }

                if ((work & 0x40000000) != 0)
                {
                    longitudeData += (uint)(1 << shiftValue);
                }
                work *= 4;
                shiftValue--;
            }

            // factor = 1.0 / (2^i)
            var factor = 1.0 / (1 << powerOfTwo);

            // Calc bounding coordinates
            var minLatitudeDeg = 90.0 - ((latitudeData + 1.0) * factor * 360.0);
            var maxLatitudeDeg = 90.0 - (latitudeData * factor * 360.0);
            var minLongitude = (longitudeData * factor * 480.0) - 180.0;
            var maxLongitude = ((longitudeData + 1.0) * factor * 480.0) - 180.0;

            list.Add(minLatitudeDeg);
            list.Add(maxLatitudeDeg);
            list.Add(minLongitude);
            list.Add(maxLongitude);
            return list;
        }

        public List<byte> DeltaDecompress(byte[] source, int destinationSize)
        {
            var output = new List<byte>();
            var sourceIndex = 0;

            if ((destinationSize % 2) == 1)
            {
                // Destination size is odd
                output.Add(source[0]);
                sourceIndex++;
                destinationSize--;
            }

            if (destinationSize > 0)
            {
                var curSourceValue = BitConverter.ToUInt16(source, sourceIndex);    // Read WORD from source buffer

                // Copy WORD to destination buffer
                output.Add(source[sourceIndex]);
                output.Add(source[sourceIndex + 1]);
                sourceIndex += 2;

                var cnt = (destinationSize >> 1) - 1;
                if (cnt != 0)
                {
                    for (; ; )
                    {
                        UInt16 addedValue;
                        if (source[sourceIndex] == 0x80)
                        {
                            addedValue = BitConverter.ToUInt16(source, sourceIndex + 1);
                            sourceIndex += 3;
                        }
                        else if (source[sourceIndex] == 0x81)
                        {
                            addedValue = (UInt16)(curSourceValue - source[sourceIndex + 1] - 0x7E);
                            sourceIndex += 2;
                        }
                        else if (source[sourceIndex] == 0x82)
                        {
                            addedValue = (UInt16)(curSourceValue + source[sourceIndex + 1] + 0x80);
                            sourceIndex += 2;
                        }
                        else
                        {
                            if (source[sourceIndex] > 0x7F)
                            {
                                addedValue = (UInt16)(curSourceValue + (UInt16)(source[sourceIndex] + 0xFF00));
                            }
                            else
                            {
                                addedValue = (UInt16)(curSourceValue + source[sourceIndex]);
                            }
                            sourceIndex++;
                        }
                        output.AddRange(BitConverter.GetBytes(addedValue));

                        curSourceValue = addedValue;
                        cnt--;

                        if (0 == cnt)
                        {
                            return output;
                        }
                    }
                }
            }
            return output;
        }
    }
}
