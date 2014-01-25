using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Netopia.VOIPTester.Util.Audio
{
    public class WaveReader
    {
        private string _fileName;
        private int _chunkID;
        private int _fileSize;
        private int _riffType;
        private int _fmtID;
        private int _fmtSize;
        private int _fmtExtraSize;
        private int _fmtCode;
        private int _channels;
        private int _sampleRate;
        private int _fmtAvgBPS;
        private int _fmtBlockAlign;
        private int _bitDepth;
        private int _dataID;
        private int _dataSize;
        private double _timeLength;
        private int _frames;
        private int _frameSize;
        private short[] _frameData;

        public WaveReader(String fileName)
        {
            this._fileName = fileName;
        }

        public void Read()
        {
            // Open the file
            FileStream stream = File.Open(_fileName, FileMode.Open, FileAccess.Read);
            BinaryReader reader = new BinaryReader(stream);

            // Read the WAV header
            // Thanks to http://stackoverflow.com/questions/13390472/how-to-get-from-wav-sound-into-double-c-sharp
            _chunkID = reader.ReadInt32();
            _fileSize = reader.ReadInt32();
            _riffType = reader.ReadInt32();
            _fmtID = reader.ReadInt32();
            _fmtSize = reader.ReadInt32();
            _fmtCode = reader.ReadInt16();
            _channels = reader.ReadInt16();
            _sampleRate = reader.ReadInt32();
            _fmtAvgBPS = reader.ReadInt32();
            _fmtBlockAlign = reader.ReadInt16();
            _bitDepth = reader.ReadInt16();

            if (_fmtSize == 18)
            {
                // Read any extra values
                _fmtExtraSize = reader.ReadInt16();
                reader.ReadBytes(_fmtExtraSize);
            }

            _dataID = reader.ReadInt32();
            _dataSize = reader.ReadInt32();

            if (_dataSize == 0)
            {
                throw new Exception("Data size is 0");
            }

            _frames = 8 * (_dataSize / _bitDepth) / _channels;
            _frameSize = _dataSize / _frames;
            _timeLength = ((double)_frames / (double)_sampleRate);

            if (_bitDepth == 16)
            {
                // 16-bit rate. Convert to shorts using two-complement.
                // Thanks to http://www.codeproject.com/Articles/19590/WAVE-File-Processor-in-C
                _frameData = new short[_frames];
                for (int i = 0; i < _frames; i++)
                {
                    short snd = reader.ReadInt16();
                    if (snd != 0)
                        snd = Convert.ToInt16((~snd | 1));
                    _frameData[i] = snd;
                }
            }
            else
            {
                reader.Close();
                throw new Exception("Unsupported bit depth");
            }

            reader.Close();
        }

        #region Properties
        public int FileSize { get { return _fileSize; } }
        public int Channels { get { return _channels; } }
        public int BitDepth { get { return _bitDepth; } }
        public int SampleRate { get { return _sampleRate; } }
        public int DataSize { get { return _dataSize; } }
        public int Frames { get { return _frames; } }
        public int FrameSize { get { return _frameSize; } }
        public double TimeLength { get { return _timeLength; } }
        public short[] FrameData { get { return _frameData; } }
        #endregion
    }
}
