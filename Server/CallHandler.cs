using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Ozeki.VoIP.SDK;
using Ozeki.VoIP;
using Ozeki.VoIP.Media;
using Ozeki.Network.Nat;
using Ozeki.Media;
using Ozeki.Media.MediaHandlers;
using Ozeki.Media.Audio.Waveform.Formats;
using Netopia.VOIPTester.Util.Audio;
using log4net;

namespace Netopia.VOIPTester.Server
{
    class CallHandler
    {
        private string _guid;
        private string _cust = "";
        private double _noise = 0.0;
        private Dictionary<string, string> _pingData = new Dictionary<string, string>();
        private ILog _log;
        private string _logFileName;
        private string _wavFileName;
        private PhoneCallAudioReceiver _audioReceiver;
        private WaveStreamRecorder _wavRecorder;
        private MediaConnector _connector;
        private AudioMixerMediaHandler _mixer;
        private ICall _call;
        private string _callID;
        private string _dataDirectory = String.Format("data{0}", Path.DirectorySeparatorChar);
        private object _sync = new object();
        private bool _complete = false;

        // Maximum length in seconds to allow the call to run
        private static int MAX_CALL_LENGTH = 30;

        public CallHandler()
        {
            _guid = Guid.NewGuid().ToString();

            // Setup logging
            _logFileName = String.Format("{0}{1}.log", _dataDirectory, _guid);
            GlobalContext.Properties["LogFileName"] = _logFileName;
            GlobalContext.Properties["CallGUID"] = _guid;
            log4net.Config.XmlConfigurator.Configure();
            _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

            _wavFileName = String.Format("{0}{1}.wav", _dataDirectory, _guid);

            _mixer = new AudioMixerMediaHandler(); 
            _audioReceiver = new PhoneCallAudioReceiver();
            _wavRecorder = new WaveStreamRecorder(_wavFileName, new WaveFormat());
            _connector = new MediaConnector();
            _connector.Connect(_audioReceiver, _mixer);
            _connector.Connect(_mixer, _wavRecorder);
        }

        private void InfoDataReceivedHandler(object sender, VoIPEventArgs<InfoData> e)
        {
            ProcessMessageData(e.Item.Content);
        }

        private void MessageDataReceivedHandler(object sender, VoIPEventArgs<MessageDataPackage> e)
        {
            ProcessMessageData(e.Item.Data);
        }

        private void ProcessMessageData(string msg)
        {
            _log.Debug(String.Format("Message Received: [{0}]", msg));

            if (msg == String.Empty)
                return;

            string[] tokens = msg.Trim().Split(' ');
            if (tokens.Length < 1)
                return;

            if (tokens[0] == "CUST" && tokens.Length == 2)
            {
                _log.Info(String.Format("CUST = {0}", tokens[1]));
                _cust = tokens[1].ToUpper();
            }
            else if (tokens[0] == "PING" && tokens.Length == 3)
            {
                _log.Info(String.Format("Ping data: Date={0} Value={1}", tokens[1], tokens[2]));
                _pingData[tokens[1]] = tokens[2];
            }
            else
                _log.Debug(String.Format("Unknown message: [{0}]", msg));
        }

        private void CallStateChangedHandler(object sender, VoIPEventArgs<CallState> e)
        {
            CallState callState = e.Item;
            _log.Debug("State change to " + callState.ToString());
            if (callState == CallState.InCall)
                CallAnswered();
            else if (callState == CallState.Completed)
                CallEnded();
            else
                _log.Debug("Unhandled call state " + callState.ToString());
        }

        private void CallAnswered()
        {
            _callID = _call.CallID;
            _log.Info("Call Answered. Call ID: " + _callID);

            _log.Info(String.Format("Recording to {0}", _wavFileName));
            _audioReceiver.AttachToCall(_call);
            _wavRecorder.DataSent += WavRecorderDataReceived;
            _wavRecorder.StartStreaming();
        }

        private void WavRecorderDataReceived(object sender, VoIPEventArgs<AudioData> e)
        {
            _log.Debug("Audio position at " + _wavRecorder.Stream.Position);
        }

        private void CallEnded()
        {
            _log.Info("Audio position at " + _wavRecorder.Stream.Position);
            _log.Info("Call Completed");
            _wavRecorder.StopStreaming();
            _connector.Disconnect(_audioReceiver, _mixer);
            _connector.Disconnect(_mixer, _wavRecorder);
            _wavRecorder.Stream.Flush();
            _wavRecorder.Dispose();
            _wavRecorder = null;
            _audioReceiver.Detach();
            _log.Info("Call tear down done");

            // Wake up the main thread
            _complete = true;
            lock (_sync)
            {
                Monitor.Pulse(_sync);
            }
        }

        private void PostProcess()
        {
            _log.Info("Starting post processing");

            // If the require data isn't available, just exit
            if (_cust == "" || _noise == 0.0)
            {
                _log.Error(String.Format("Customer or noise not defined. Cust={0}, Noise={1}",_cust,_noise));
                return;
            }

            // Open the log file for this call
            string mySqlFileName = String.Format("{0}{1}.mysql", _dataDirectory, _guid);
            FileStream mySqlFile = File.Open(mySqlFileName, FileMode.CreateNew, FileAccess.Write);
            StreamWriter mySqlWriter = new StreamWriter(mySqlFile);

            string base64LogFile = Convert.ToBase64String(File.ReadAllBytes(_logFileName));
            string base64WavFile = Convert.ToBase64String(File.ReadAllBytes(_wavFileName));
            mySqlWriter.WriteLine(String.Format("INSERT INTO callstats (cust,guid,noise,logfile,wavfile) VALUES (\"{0}\",\"{1}\",{2},\"{3}\",\"{4}\");", _cust, _guid, _noise.ToString(),base64LogFile,base64WavFile));
            foreach (string datetime in _pingData.Keys)
                mySqlWriter.WriteLine(String.Format("INSERT INTO pingstats (cust,guid,date,value) VALUES (\"{0}\",\"{1}\",\"{2}\",{3});", _cust, _guid, datetime, _pingData[datetime]));
            
            mySqlWriter.Flush();
            mySqlWriter.Close();

            _log.Info("Completed post processing");
        }

        public void HandleCall(ICall call)
        {
            _log.Info("Answering call");

            _call = call;
            _call.InstantMessageDataReceived += MessageDataReceivedHandler;
            // _call.InfoDataReceived += InfoDataReceivedHandler;
            _call.CallStateChanged += CallStateChangedHandler;
            _call.Accept();

            lock (_sync)
            {
                // Wait for the defined MAX_CALL_LENGTH
                Monitor.Wait(_sync, MAX_CALL_LENGTH * 1000);
                _log.Debug("Main thread woken up");
            }

            if (_call.CallState != CallState.Completed)
            {
                _log.Error("Call not completed. Incomplete results.");
                _call.HangUp();
            }
            
            if (_complete)
            {
                _log.Debug("Call is marked as complete");
                AudioProcessor audioProcessor = new AudioProcessor();
                try
                {
                    _noise = audioProcessor.Process(_wavFileName, _log);
                    _log.Info("Noise detected: " + _noise);
                    PostProcess();
                }
                catch (Exception ex)
                {
                    _log.Error("Exception encountered during audio process", ex);
                }
            }
            _log.Info("Done");
        }
    }
}
