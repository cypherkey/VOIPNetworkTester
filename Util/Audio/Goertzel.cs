using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Netopia.VOIPTester.Util.Audio
{
    public class Goertzel
    {
        // Thanks to http://netwerkt.wordpress.com/2011/08/25/goertzel-filter/
        public static double Calculate(short[] sampleData, int offset, int length, int sampleRate, double searchFreq)
        {
            double s_prev = 0.0;
            double s_prev2 = 0.0;    
            double coeff,normalizedfreq,power,s;
            int i;
            normalizedfreq = searchFreq / (double)sampleRate;
            coeff = 2.0*Math.Cos(2.0*Math.PI*normalizedfreq);
            for (i=0; i<length; i++)
            {
                s = sampleData[i+offset] + coeff * s_prev - s_prev2;
                s_prev2 = s_prev;
                s_prev = s;
            }
            power = s_prev2*s_prev2+s_prev*s_prev-coeff*s_prev*s_prev2;
            return power;
        }
    }
}
