using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace Netopia.VOIPTester.Util
{
    public class Config
    {
        private Dictionary<string, string> _configData = new Dictionary<string, string>();

        public Config(string fileName)
        {
            ReadFile(fileName);
        }

        private void ReadFile(string fileName)
        {
            foreach (string line in File.ReadAllLines(fileName))
            {
                if ((!string.IsNullOrEmpty(line)) &&
                    (!line.StartsWith(";")) &&
                    (!line.StartsWith("#")) &&
                    (!line.StartsWith("'")) &&
                    (line.Contains('=')))
                {
                    int index = line.IndexOf('=');
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(index + 1).Trim();

                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    _configData.Add(key, value);
                }
            }
        }

        public string GetStringValue(string key)
        {
            if (_configData.ContainsKey(key))
                return _configData[key];
            else
                return null;
        }

        public int GetIntValue(string key)
        {
           return Int32.Parse(GetStringValue(key));
        }
    }
}
