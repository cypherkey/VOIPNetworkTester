using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Netopia.VOIPTester.Util.Audio;
using Netopia.VOIPTester.Server;

namespace Netopia.VOIPTester.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            Service s = new Service();

            try
            {
                //AudioProcessor ap = new AudioProcessor();
                //ap.Process("audiocheck.net_sin_3000Hz_-3dBFS_5s.wav");
                //ap.Process("6f9635bc-97b4-47f8-b80e-3b8d8783de3a.wav");
                s.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception " + e.GetType().ToString());
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
