using System;
using System.Collections.Generic;
using System.Linq;

namespace BGLConverter
{
    class Bitpack
    {
        private byte[] _sourceBuffer;
        private int _sourceIndex;
        private int _nbRead;
        private int _nbRemainingBits;
        private int _nbBitsNotProcessed;
        private byte[] _outputBuffer;
        internal List<byte> Decompress(byte[] record, int outputSize, int nbRows, int nbColumns)
        {
            var targetIndex = 0;

            _sourceBuffer = record;
            _sourceIndex = 0;
            _nbRead = 0;
            _nbBitsNotProcessed = 0;
            _outputBuffer = new byte[outputSize];

            if (null == record || 0 == record.Length)
            {
                return null;
            }

            _nbRemainingBits = 8 * record.Length;
            _nbBitsNotProcessed = 8;

            Int32 nbBytesForInitialAddValue;
            var b = getNext_N_SourceBits(8, out nbBytesForInitialAddValue);

            Int32 nbShifts;
            b &= getNext_N_SourceBits(8, out nbShifts);

            Int32 initialAddValue;
            b &= getNext_N_SourceBits(8 * nbBytesForInitialAddValue, out initialAddValue);

            Int32 nbBitsToGetPerData;
            b &= getNext_N_SourceBits(4, out nbBitsToGetPerData);

            Int32 maxBitsPerValueRead;
            b &= getNext_N_SourceBits(4, out maxBitsPerValueRead);

            if (maxBitsPerValueRead == 0)
            {
                maxBitsPerValueRead = 16;
            }

            var nbRowsPerRowIteration = nbRows / 4;
            var nbColumnsPerColumnIteration = nbColumns / 4;
            var bytesInterval = nbRowsPerRowIteration * nbColumns;

            var nbColumnsForLastColumnIteration = (nbColumns % 4);
            var nbRowsForLastRowIteration = (nbRows % 4);

            for (var rowIteration = 0; rowIteration < 4; rowIteration++)
            {
                var nbRowsToProcess = nbRowsPerRowIteration;
                if (rowIteration == 3)
                {
                    // Last row iteration
                    nbRowsToProcess += nbRowsForLastRowIteration;
                }

                for (var columnIteration = 0; columnIteration < 4; columnIteration++)
                {
                    var copyCountPerRow = nbColumnsPerColumnIteration;
                    if (columnIteration == 3)
                    {
                        // Last Column Iteration
                        copyCountPerRow += nbColumnsForLastColumnIteration;
                    }

                    b &= populateDecompressedBuffer(targetIndex + columnIteration * (nbColumns / 4), nbColumns, initialAddValue,
                                                    nbBitsToGetPerData, copyCountPerRow, nbRowsToProcess, nbShifts, maxBitsPerValueRead);
                }
                targetIndex += bytesInterval;
            }
            if (_nbBitsNotProcessed != 8)
            {
                _nbRead++;
            }
            if (_nbRead != record.Length)
            {
                return null;
            }

            return _outputBuffer.ToList();
        }

        private bool populateDecompressedBuffer(int startRowCopyIndex, int nbColumnsPerRow, int addValue, int nbBitsToGetPerData, int nbBytesToOutputPerRow, int nbRowsToProcess, int nbShifts, int maxBitsPerValueRead)
        {
            int copyData, nbBitsPerValueRead;

            // Read copyData
            var b = getNext_N_SourceBits(Math.Min(nbBitsToGetPerData, 8), out copyData);

            // Read nbBitsPerValueRead
            // if 0, will copy value as is
            b &= getNext_N_SourceBits(4, out nbBitsPerValueRead);

            var nbAdditionalShift = (nbBitsToGetPerData <= 8) ? 0 : nbBitsToGetPerData - 8;

            var valueToCopy = (copyData << ((nbShifts + nbAdditionalShift) & 0xFF)) + addValue;

            if (nbBitsPerValueRead == 0)
            {
                //-------------------------------
                // Identical value to be repeated
                //-------------------------------
                targetSetIdenticalValue(startRowCopyIndex, (byte)valueToCopy, nbRowsToProcess, nbColumnsPerRow, nbBytesToOutputPerRow);
                return b;
            }

            // Copy will handle only blocks 8 x 8 otherwise recursive call
            if (nbBytesToOutputPerRow < 8 || nbRowsToProcess < 8)
            {
                if (nbBitsPerValueRead > maxBitsPerValueRead)
                {
                    nbBitsPerValueRead = maxBitsPerValueRead;
                }
                //-------------------
                // Values Read And Set
                //-------------------
                b &= targetReadAndSetValue(startRowCopyIndex, nbRowsToProcess, nbColumnsPerRow, nbBytesToOutputPerRow, valueToCopy, nbBitsPerValueRead, nbShifts);
                return b;
            }

            //--------------------------------------------
            // RECURSIVE CALL for a square 4 times smaller
            //--------------------------------------------
            var nbRowsPerRowIteration = nbRowsToProcess / 4;
            var nbRowsForLastRowIteration = nbRowsToProcess % 4;

            var bytesInterval = nbRowsPerRowIteration * nbColumnsPerRow;

            var nbColumnsPerColumnIteration = nbBytesToOutputPerRow / 4;
            var nbColumnsForLastColumnIteration = nbBytesToOutputPerRow % 4;

            for (var rowIteration = 0; rowIteration < 4; rowIteration++)
            {
                nbRowsToProcess = nbRowsPerRowIteration;
                if (rowIteration == 3)
                {
                    // Last row iteration
                    nbRowsToProcess += nbRowsForLastRowIteration;
                }

                var rowStartIndex = startRowCopyIndex;
                for (var columnIteration = 0; columnIteration < 4; columnIteration++)
                {
                    nbBytesToOutputPerRow = nbColumnsPerColumnIteration;
                    if (columnIteration == 3)
                    {
                        nbBytesToOutputPerRow += nbColumnsForLastColumnIteration;
                    }
                    b &= populateDecompressedBuffer(rowStartIndex, nbColumnsPerRow, valueToCopy, nbBitsPerValueRead,
                                                    nbBytesToOutputPerRow, nbRowsToProcess, nbShifts, maxBitsPerValueRead);

                    rowStartIndex += nbColumnsPerColumnIteration;     // Next row
                }

                startRowCopyIndex += bytesInterval;
            }
            return b;
        }

        private void targetSetIdenticalValue(int startRowCopyIndex, byte valueToCopy, int nbRowsToProcess, int nbColumnsPerRow, int nbRepeatPerRow)
        {
            if (nbRepeatPerRow > 0)
            {
                var srclist = new List<byte>(nbRepeatPerRow);
                srclist.AddRange(Enumerable.Repeat(valueToCopy, nbRepeatPerRow));
                var srcArray = srclist.ToArray();
                while (nbRowsToProcess > 0)
                {
                    // copy <valueToCopy> <cnt> times starting at the current position (current row)
                    Buffer.BlockCopy(srcArray, 0, _outputBuffer, startRowCopyIndex, nbRepeatPerRow);

                    nbRowsToProcess--;
                    startRowCopyIndex += nbColumnsPerRow;
                }
            }
        }

        private bool targetReadAndSetValue(int startRowCopyIndex, int nbRowsToProcess, int nbColumnsPerRow, int nbBytesToOutputPerRow, int addValue, int nbBitsPerRead, int nbShiftsLeft)
        {
            // nbBitsPerRead = nb bits per value to read
            // nbShiftsLeft = number of times to shift the read value to the left before adding the <addValue>
            if (nbRowsToProcess < 0)
            {
                return true;
            }

            for (var i = 0; i < nbRowsToProcess; i++, startRowCopyIndex += nbColumnsPerRow)
            {
                for (var j = 0; j < nbBytesToOutputPerRow; j++)
                {
                    Int32 srcValue;
                    if (false == getNext_N_SourceBits(nbBitsPerRead, out srcValue))
                    {
                        return false;
                    }

                    var byteValue = (byte)(((srcValue << nbShiftsLeft) + addValue) & 0xFF);

                    // copy AL to output buffer
                    _outputBuffer[startRowCopyIndex + j] = byteValue;
                }
            }
            return true;
        }

        private bool getNext_N_SourceBits(int nbBitsToGet, out Int32 retValue)
        {
            // Read always the LSB bits first
            retValue = 0;

            if (_nbRemainingBits < nbBitsToGet)
            {
                return false;
            }

            if (nbBitsToGet > 0)
            {
                _nbRemainingBits -= nbBitsToGet;

                var nbRightShift = 8 - _nbBitsNotProcessed;

                if (_nbBitsNotProcessed <= nbBitsToGet)
                {
                    retValue = _sourceBuffer[_sourceIndex] >> nbRightShift;

                    nbRightShift = _nbBitsNotProcessed; // keep for later
                    _sourceIndex++;
                    _nbRead++;

                    var nbBitsStillToGet = nbBitsToGet - _nbBitsNotProcessed;
                    _nbBitsNotProcessed = 8;

                    if (nbBitsStillToGet <= 0)
                    {
                        return true;
                    }

                    while (nbBitsStillToGet > 0)
                    {
                        var sourceByteValue = _sourceBuffer[_sourceIndex];

                        if (nbBitsStillToGet < 8)
                        {
                            retValue |= ((sourceByteValue & (1 << nbBitsStillToGet) - 1) << nbRightShift);
                            _nbBitsNotProcessed -= nbBitsStillToGet;
                            return true;
                        }
                        sourceByteValue >>= nbRightShift;
                        retValue |= sourceByteValue;
                        _sourceIndex++;
                        _nbRead++;
                        nbBitsStillToGet -= _nbBitsNotProcessed; // ESI = nbBitsStillToGet ?
                        nbRightShift += _nbBitsNotProcessed;
                        _nbBitsNotProcessed = 8;
                    }
                    return true;
                }
                retValue = (_sourceBuffer[_sourceIndex] >> nbRightShift) & ((1 << nbBitsToGet) - 1);
                _nbBitsNotProcessed -= nbBitsToGet;
                return true;
            }
            return true;
        }
    }
}
