using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using Netopia.VOIPTester.Util;

namespace Netopia.VOIPTester.Client
{
    class Program
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static Dictionary<string, string> _pingData = new Dictionary<string, string>();
        private static Config _config;
        private static System.Timers.Timer _pingTimer;
        private static System.Timers.Timer _callTimer;
        private static Ping _ping = new Ping();
        private static object _sync = new object();

        private static bool ReadAndValidateConfig()
        {
            // Load configuration file
            try
            {
                _config = new Config("config.txt");
            }
            catch (FileNotFoundException)
            {
                _log.Fatal("Configuration file not found");
                return false;
            }

            if (_config.GetStringValue("cust") == null)
            {
                _log.Fatal("Configuration entry [cust] not defined");
                return false;
            }

            return true;
        }

        private static void callEvent(object sender, ElapsedEventArgs e)
        {
            _pingTimer.Enabled = false;
            VOIPCall call = new VOIPCall(_config);
            if (call.MakeCall(_pingData) == true)
                _pingData.Clear();
            _pingTimer.Enabled = true;
        }

        private static void pingEvent(object sender, ElapsedEventArgs e)
        {
            PingReply reply = _ping.Send(_config.GetStringValue("ping.host"));
            _pingData.Add(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), reply.RoundtripTime.ToString());
            _log.Info(String.Format("Ping reply from {0} is {1}ms", _config.GetStringValue("ping.host"), reply.RoundtripTime));
        }

        static void Main(string[] args)
        {
            _log.Info("Starting client");
            if (!ReadAndValidateConfig())
                return;
            
            /*
            _pingData.Add("2014-01-05T00:33:29", "29");
            _pingData.Add("2014-01-05T00:34:29", "29");
            _pingData.Add("2014-01-05T00:35:29", "29");
            _pingData.Add("2014-01-05T00:36:29", "29");
            _pingData.Add("2014-01-05T00:37:29", "29");
             */
            VOIPCall call = new VOIPCall(_config);
            if (call.MakeCall(_pingData) == true)
                _pingData.Clear();
            return;

            // Setup the Ping Timer if defined in the configuration file
            if (_config.GetStringValue("ping.host") != null)
            {
                // Setup the timer for every minute
                _pingTimer = new System.Timers.Timer(60000);
                _pingTimer.Elapsed += pingEvent;
                _pingTimer.Start();

                // The timer doesn't run at startup. Issue a Ping right away.
                pingEvent(null, null);
            }

            // Setup the call timer every hour
            //_callTimer = new System.Timers.Timer(3600000);
            _callTimer = new System.Timers.Timer(300000);
            _callTimer.Elapsed += callEvent;
            _callTimer.Start();

            lock(_sync)
            {
                Monitor.Wait(_sync);
            }
        }
    }
}
