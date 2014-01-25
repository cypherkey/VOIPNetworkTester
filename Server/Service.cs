using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Ozeki.VoIP.SDK;
using Ozeki.VoIP;
using Ozeki.VoIP.Media;
using Ozeki.Network.Nat;
using Ozeki.Media;
using Ozeki.Media.MediaHandlers;
using Ozeki.Media.Audio.Waveform.Formats;
using Netopia.VOIPTester.Util;
using Netopia.VOIPTester.Util.Audio;

namespace Netopia.VOIPTester.Server
{
    class Service
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private object _sync = new object();
        private ISoftPhone _softPhone;
        private IPhoneLine _phoneLine;
        private Config _config;

        private void phoneLine_PhoneLineStateChanged(object sender, VoIPEventArgs<PhoneLineState> e)
        {
            _log.Debug("Line state changed to " + e.Item.ToString());
            if (e.Item == PhoneLineState.RegistrationSucceeded)
                _log.Info("Line Registered");
        }

        private void softPhone_IncomingCall(object sender, VoIPEventArgs<IPhoneCall> e)
        {
            _log.Debug("Incoming call handler start");
            CallHandler handler = new CallHandler();
            handler.HandleCall(e.Item);
            _log.Debug("Incoming call handler end");
        }

        private void ProcessExitHandler(object sender, EventArgs e)
        {
            ShutDown();
        }

        private void ShutDown()
        {
            _log.Info("Shutting down");
            _softPhone.UnregisterPhoneLine(_phoneLine);
        }

        public void Run()
        {
            _log.Info("Starting server");

            try
            {
                _config = new Config("config.txt");
            }
            catch (FileNotFoundException)
            {
                _log.Fatal("Configuration file not found");
                return;
            }

            _softPhone = SoftPhoneFactory.CreateSoftPhone(SoftPhoneFactory.GetLocalIP(), _config.GetIntValue("sip.min"), _config.GetIntValue("sip.max"), _config.GetIntValue("sip.port"));
            _phoneLine = _softPhone.CreatePhoneLine(new SIPAccount(true, _config.GetStringValue("sip.displayname"), _config.GetStringValue("sip.username"), _config.GetStringValue("sip.username"), _config.GetStringValue("sip.password"), _config.GetStringValue("sip.host")), new NatConfiguration(_config.GetStringValue("externalip"), true));
            _phoneLine.PhoneLineStateChanged += phoneLine_PhoneLineStateChanged;
            _softPhone.RegisterPhoneLine(_phoneLine);
            _softPhone.IncomingCall += softPhone_IncomingCall;

            // Wait 3 seconds and check if the line is registered. If not, shut down.
            System.Threading.Thread.Sleep(3000);
            if (_phoneLine.LineState != PhoneLineState.RegistrationSucceeded)
            {
                _log.Error(String.Format("Phone Line is not registered. State is {0}. Exiting.", _phoneLine.LineState.ToString()));
                return;
            }

            lock (_sync)
                System.Threading.Monitor.Wait(_sync);
        }
    }
}
