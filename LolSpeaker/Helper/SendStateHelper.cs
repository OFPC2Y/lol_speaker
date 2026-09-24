using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace LolSpeaker.Helper
{
    /// <summary>
    /// 顺序发送的游标，按词库名持久化，重启程序后接着上次的位置继续
    /// </summary>
    public static class SendStateHelper
    {
        private static readonly string stateFileName = "sendState.json";

        private static Dictionary<string, int> _Cursors;

        private static string StateFilePath => LocalConfigHelper.GetPath(stateFileName);

        private static Dictionary<string, int> Cursors
        {
            get
            {
                if (_Cursors == null) _Cursors = Load();

                return _Cursors;
            }
        }

        /// <summary>
        /// 取该词库的下一条词条下标并推进游标，走到末尾后回到第一条
        /// </summary>
        public static int NextIndex(string categoryName, int count)
        {
            var cursors = Cursors;

            if (!cursors.TryGetValue(categoryName, out int cursor) || cursor < 0 || cursor >= count)
            {
                cursor = 0;
            }

            cursors[categoryName] = (cursor + 1) % count;

            Save();

            return cursor;
        }

        private static Dictionary<string, int> Load()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    var cursors = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(StateFilePath));

                    if (cursors != null) return cursors;
                }
            }
            catch
            {
                //游标文件损坏时按从头发送处理，不影响正常发送
            }

            return new Dictionary<string, int>();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(LocalConfigHelper.Dir);

                File.WriteAllText(StateFilePath, JsonConvert.SerializeObject(Cursors));
            }
            catch
            {
                //游标写不进去只影响下次启动的起始位置，不影响本次发送
            }
        }
    }
}
