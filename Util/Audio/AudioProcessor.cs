using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using log4net;

namespace Netopia.VOIPTester.Util.Audio
{
    public class AudioProcessor
    {
        // Number of frequencies that are half of the sample rate to scan
        private int _frequencyGranularity = 2000;

        // Number of frames to use to create a sample for the filter
        private int _sampleSize = 4000;

        public double Process(String fileName, ILog logger)
        {
            // Read the wave file
            WaveReader wave = new WaveReader(fileName);
            wave.Read();
            short[] frameData = wave.FrameData;

            // Log data about the WAV file
            logger.Debug("Audio: Filename: " + fileName);
            logger.Debug("Audio: file size: " + wave.FileSize.ToString());
            logger.Debug("Audio: sample rate: " + wave.SampleRate.ToString());
            logger.Debug("Audio: channels: " + wave.Channels.ToString());
            logger.Debug("Audio: bit depth: " + wave.BitDepth.ToString());
            logger.Debug("Audio: data size: " + wave.DataSize.ToString());
            logger.Debug("Audio: frames: " + wave.Frames.ToString());
            logger.Debug("Audio: frame size: " + wave.FrameSize.ToString());
            logger.Debug("Audio: Time length: " + wave.TimeLength.ToString());

            double noisePercentange = 0;

            // Loop over the data from the wave file in increments defined by the sampleSize
            int frameCount = 0;
            while(frameCount + _sampleSize < frameData.Length)
            {
                // Dictionary to store the power level at a particular frequency
                Dictionary<int, double> vals = new Dictionary<int, double>(_frequencyGranularity);
                double totalPower = 0;
                for (int i = 1; i <= _frequencyGranularity; i++)
                {
                    // Only process up to half of the sample rate as this is the Nyquist limit
                    // http://stackoverflow.com/questions/20864651/calculating-the-amount-of-noise-in-a-wav-file-compared-to-a-source-file
                    int freq = i * wave.SampleRate / 2 / _frequencyGranularity;
                    vals[freq] = Goertzel.Calculate(frameData, frameCount, _sampleSize, wave.SampleRate, freq);
                    totalPower += vals[freq];
                }

                // Calculate the percentange of noise by subtracting the percentage of power at the desided frequency of 3000 from 100.
                double frameNoisePercentange = (100 - (vals[3000] / totalPower * 100));
                logger.Debug("Frame: " + frameCount + " Noise: " + frameNoisePercentange);
                noisePercentange += frameNoisePercentange;
                frameCount += _sampleSize;
            }
            double averageNoise = (noisePercentange / (int)(frameCount/_sampleSize));
            logger.Info("Average Noise: " + averageNoise);
            return averageNoise;
        }
    }
}
