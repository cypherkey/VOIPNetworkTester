using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using Ozeki.VoIP.Media;
using Ozeki.VoIP.SDK;
using Ozeki.VoIP;
using Ozeki.Network.Nat;
using Ozeki.Media;
using Ozeki.Media.MediaHandlers;
using Netopia.VOIPTester.Util;
using Netopia.VOIPTester.Util.Audio;

namespace Netopia.VOIPTester.Client
{
    class VOIPCall
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private ISoftPhone _softPhone;
        private IPhoneLine _phoneLine;
        private PhoneCallAudioSender _callSender = new PhoneCallAudioSender();
        private MediaConnector _connector = new MediaConnector();
        private IPhoneCall _call;
        private WaveStreamPlayback _wavPlayback;
        private Config _config;
        private object _sync = new object();
        private bool _sendDataComplete = false;
        private bool _sendToneComplete = false;
        private bool _complete = false;
        private Dictionary<string, string> _pingData;

        // Maximum length in seconds to allow the call to run
        private static int MAX_CALL_LENGTH = 30;

        public VOIPCall(Config config)
        {
            _config = config;
        }

        private void PhoneLineStateChanged(object sender, VoIPEventArgs<PhoneLineState> e)
        {
            _log.Debug("Line state changed to " + e.Item.ToString());
            if (e.Item == PhoneLineState.RegistrationSucceeded)
            {
                _log.Info("Line Registered");
                Dial();
            }
            else if (e.Item == PhoneLineState.UnregSucceeded)
            {
                _log.Info("Line Unregistered");
                _phoneLine = null;
                _softPhone = null;
                _complete = true;
                lock (_sync)
                {
                    Monitor.Pulse(_sync);
                }
            }
            else if (e.Item == PhoneLineState.RegistrationFailed)
            {
                _log.Info("Registration failed. Try again");
                Register();
            }
        }

        private void CallStateChangedHandler(object sender, VoIPEventArgs<CallState> e)
        {
            _log.Debug("Call state changed to " + e.Item.ToString());
            if (e.Item == CallState.InCall)
            {
                _log.Info("Call Answered");
                StartStreaming();
                SendData();
            }
            else if (e.Item == CallState.Completed)
            {
                _call = null;
                UnRegisterBegin();
            }
        }

        private void Register()
        {
            _softPhone.RegisterPhoneLine(_phoneLine);
        }

        private void UnRegisterBegin()
        {
            _log.Info("Unregistering");
            _softPhone.UnregisterPhoneLine(_phoneLine);
        }

        private void StartStreaming()
        {
            _log.Info("Playing tone file");
            _callSender.AttachToCall(_call);
            _wavPlayback.DataSent += WavRecorderDataSent;
            _wavPlayback.StartStreaming();
            
        }

        private void WavRecorderDataSent(object sender, VoIPEventArgs<AudioData> e)
        {
            _log.Debug("Audio position at " + _wavPlayback.Stream.Position);
        }

        private void StreamingStopped(object sender, EventArgs e)
        {
            _log.Info("Done playing file");
            _connector.Disconnect(_wavPlayback, _callSender);
            _callSender.Detach();
            _sendToneComplete = true;
            CheckAndComplete();
        }

        private void SendData()
        {
            string msg;
            foreach (string date in _pingData.Keys)
            {
                msg = String.Format("PING {0} {1}", date, _pingData[date]);
                _log.Info("Sending " + msg);
                _call.SendInstantMessage(MimeType.Text_Plain, msg);
                System.Threading.Thread.Sleep(100);
            }
            msg = String.Format("CUST {0}", _config.GetStringValue("cust"));
            _log.Info("Sending " + msg);
            _call.SendInstantMessage(MimeType.Text_Plain, msg);
            _sendDataComplete = true;
            CheckAndComplete();
        }

        private void CheckAndComplete()
        {
            if (_sendDataComplete && _sendToneComplete)
            {
                _log.Info("Hanging up");
                _call.HangUp();
            }
        }

        private void MessageSendError(object sender, VoIPEventArgs<MessageErrorPackage> e)
        {
            _log.Error("Sender: " + sender.GetType().ToString());
            _log.Error(String.Format("Unable to send message. Error is {0}", e.Item.Error));
        }

        private void Dial()
        {
            _log.Info(String.Format("Calling {0}", _config.GetStringValue("call")));
            _call = _softPhone.CreateCallObject(_phoneLine, _config.GetStringValue("call"));
            _call.CallStateChanged += CallStateChangedHandler;
            _call.InstantMessageSendError += MessageSendError;
            _call.Start();
        }

        public bool MakeCall(Dictionary<string,string> pingData)
        {
            _log.Info("Beginning MakeCall");
            _pingData = pingData;

            // Ensure the specified audio file is in the manifest
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = String.Format("Netopia.VOIPTester.Client.Resources.{0}", _config.GetStringValue("tonefile"));
            Stream resourceStream = assembly.GetManifestResourceStream(resourceName);
            // Console.WriteLine(String.Join("\n", assembly.GetManifestResourceNames()));
            if (resourceStream == null)
            {
                _log.Fatal(String.Format("Can't find embedded audio file {0}", resourceName));
                return false;
            }

            // Create the SIP client and register to the server
            _softPhone = SoftPhoneFactory.CreateSoftPhone(SoftPhoneFactory.GetLocalIP(), _config.GetIntValue("sip.min"), _config.GetIntValue("sip.max"), _config.GetIntValue("sip.port"));
            _phoneLine = _softPhone.CreatePhoneLine(new SIPAccount(true, _config.GetStringValue("sip.displayname"), _config.GetStringValue("sip.username"), _config.GetStringValue("sip.username"), _config.GetStringValue("sip.password"), _config.GetStringValue("sip.host")), new NatConfiguration(_config.GetStringValue("externalip"), true));
            _phoneLine.PhoneLineStateChanged += PhoneLineStateChanged;
            _wavPlayback = new WaveStreamPlayback(resourceStream);
            _wavPlayback.Stopped += StreamingStopped;
            _connector.Connect(_wavPlayback, _callSender);

            // Register the SIP connection
            Register();

            // Wait 3 seconds and check if the line is registered. If not, shut down.
            System.Threading.Thread.Sleep(3000);
            if (_phoneLine.LineState != PhoneLineState.RegistrationSucceeded)
            {
                _log.Error(String.Format("Phone Line is not registered. State is {0}. Exiting.", _phoneLine.LineState.ToString()));
                return false;
            }


            lock (_sync)
            {
                // Wait for the defined MAX_CALL_LENGTH
                Monitor.Wait(_sync,MAX_CALL_LENGTH*1000);
            }
            _log.Info("Completed call");

            return _complete;
        }
    }
}
