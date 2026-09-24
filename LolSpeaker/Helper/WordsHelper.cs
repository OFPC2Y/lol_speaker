using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using LolSpeaker.ViewModels;

namespace LolSpeaker.Helper
{
    public class WordsHelper
    {

        /// <summary>
        /// 单条词条的长度上限
        /// </summary>
        public const int MaxContentLength = 200;

        public static string GetZAWord()
        {
            var client = new RestClient("https://api.shadiao.app/");
            var request = new RestRequest("nmsl", Method.Get);
            request.AddParameter("level", "min");
            var response = client.Execute(request);
            if (response.IsSuccessful)
            {
                var res = JObject.Parse(response.Content);

                return res["data"]["text"].ToString();
            }
            else
                return "连接服务器失败";
        }


        public static void EnsureValidContent(string value)
        {
            if( !IsVaileContent(value,out string msg))
            {
                throw new ArgumentOutOfRangeException(msg, innerException: null);
            }
        }

        private static bool IsVaileContent(string value,out string msg)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                msg = "词条内容不为空";
                return false;
            }
            if (value.Length > MaxContentLength)
            {
                msg = $"词条内容不能超过{MaxContentLength}";
                return false;
            }

            msg = "";
            return true;
        }

        public static bool IsVaileContent(string value)
        {
            return IsVaileContent(value, out _);
        }
    }
}
