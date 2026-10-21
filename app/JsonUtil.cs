using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace StsCardEditor
{
    public static class JsonUtil
    {
        private static JavaScriptSerializer Create()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 200;
            return s;
        }

        public static T FromJson<T>(string text)
        {
            return Create().Deserialize<T>(text);
        }

        public static object FromJson(string text)
        {
            return Create().DeserializeObject(text);
        }

        public static string ToJson(object value)
        {
            return Create().Serialize(value);
        }

        public static string ReadUtf8(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public static void WriteUtf8(string path, string text)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
