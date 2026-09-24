using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LolSpeaker.Helper
{
    /// <summary>
    /// 清单资源工具类
    /// </summary>
    public static class ManifestHelper
    {
        public static Stream GetManifestStream(string jsonName)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream($"LolSpeaker.Assets.{jsonName}");
        }
    }
}
