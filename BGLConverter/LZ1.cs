using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BGLConverter
{
    class LZ1
    {
        private byte[] _sourceBuffer;
        private int _sourceIndex;
        private List<bool> _bitsPool;
        private byte[] _outputBuffer;
        private int _outputCount;
        private int _existingSequenceOffset;
        private int _sequenceLength;

        internal List<byte> Decompress(byte[] data, int outputSize)
        {
            _sourceBuffer = data;
            _sourceIndex = 0;
            _bitsPool = new List<bool>();
            _outputBuffer = new byte[outputSize];
            _outputCount = 0;

            while (_outputCount < outputSize)
            {
                var flag = BitListToInt(ReadBits(2));

                if (flag == 1)
                {
                    var output = BitListToByte(ReadBits(7)) + 0x80;
                }
                else if (flag == 2)
                {
                    var output = BitListToByte(ReadBits(7));
                }
                else
                {
                    // flag = 0 or 3 
                    // This is a sequence already stored in the output buffer
                    // Retrieve the offset of where this sequence is stored.

                    if (flag == 0)
                    {
                        _existingSequenceOffset = BitListToInt(ReadBits(6));
                    }
                    else
                    {
                        if (BitListToInt(ReadBits(1)) == 0)
                        {
                            _existingSequenceOffset = 0x40 + BitListToInt(ReadBits(8));
                        }
                        else
                        {
                            _existingSequenceOffset = 0x140 + BitListToInt(ReadBits(12));
                        }

                        if (_existingSequenceOffset != 0x113F)
                        {
                            // Now get existing sequence length
                            var nbBitsToRead = 0;
                            while (0 == BitListToInt(ReadBits(1)))
                            {
                                nbBitsToRead++;
                            }

                            if (nbBitsToRead == 0)
                            {
                                _sequenceLength = 2;
                            }
                            else
                            {
                                _sequenceLength = BitListToInt(ReadBits(nbBitsToRead)) + 1 + (2 ^ nbBitsToRead);
                            }

                            backwardOutput();
                        }
                    }
                }
            }

            return new List<byte>();
        }

        private List<bool> ReadBits(int numBits)
        {
            while (_bitsPool.Count < numBits)
            {
                _bitsPool = ByteToBitList(_sourceBuffer[_sourceIndex++]).Concat(_bitsPool).ToList();
            }

            var result = _bitsPool.GetRange(_bitsPool.Count - numBits, numBits);
            result.Reverse();
            _bitsPool.RemoveRange(_bitsPool.Count - numBits, numBits);

            return result;
        }

        private void backwardOutput()
        {
            var nbBytesToCopy = _sequenceLength;
            var index = _outputCount - _existingSequenceOffset;

            while (nbBytesToCopy > 0)
            {
                _outputBuffer[index] = BitListToByte(ReadBits(8));
                index++;
                nbBytesToCopy--;
                _outputCount++;
            }
        }
        private static List<bool> ByteToBitList(byte b)
        {
            // prepare the return result
            List<bool> result = new List<bool>();

            // check each bit in the byte. if 1 set to true, if 0 set to false
            for (int i = 0; i < 8; i++)
                result.Add((b & (1 << i)) == 0 ? false : true);

            // reverse the array
            result.Reverse();

            return result;
        }
        private int BitListToInt(List<bool> bitList)
        {
            int[] array = new int[1];
            new BitArray(bitList.ToArray()).CopyTo(array, 0);
            return array[0];
        }

        private byte BitListToByte(List<bool> bitList)
        {
            byte[] array = new byte[1];
            new BitArray(bitList.ToArray()).CopyTo(array, 0);
            return array[0];
        }
    }
}
