// The add-in's own calls to the EasyPDM server, with the login the macros keep.
//
// The macros store the server address and their token with VBA's SaveSetting, i.e. under
// HKCU\Software\VB and VBA Program Settings\EasyPDM\Connection. The add-in reads the same
// values, so signing in through either the macros or the add-in's Log in command (which runs
// the macro) covers both; the add-in never writes them.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace EasyPDM.SolidWorksAddin
{
    internal static class Settings
    {
        private const string Key = @"Software\VB and VBA Program Settings\EasyPDM\Connection";
        private const string DefaultBaseUrl = "http://localhost:5000/api";

        private static string Get(string name, string fallback)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Key))
                    return key?.GetValue(name) as string ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// The API address, e.g. "http://server:5000/api".
        public static string BaseUrl => Get("ApiBaseUrl", DefaultBaseUrl).TrimEnd('/');

        /// The web app's address: the API address without its "/api".
        public static string ServerUrl
        {
            get
            {
                var url = BaseUrl;
                return url.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? url.Substring(0, url.Length - 4) : url;
            }
        }

        public static string Token => Get("SessionToken", "");
        public static string DisplayName => Get("DisplayName", "");
        public static bool SignedIn => Token.Length > 0;
    }

    internal sealed class ApiResult
    {
        public int Status;
        public object Body;
        public bool Ok => Status >= 200 && Status < 300;
    }

    internal static class Api
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        static Api()
        {
            // .NET Framework 4.8 still offers TLS 1.0 by default on some Windows setups.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public static ApiResult Call(string method, string path, object body = null)
        {
            var request = (HttpWebRequest)WebRequest.Create(Settings.BaseUrl + path);
            request.Method = method;
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            request.Accept = "application/json";
            // The macros learnt the hard way that Windows may answer a GET from its cache.
            request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            var token = Settings.Token;
            if (token.Length > 0)
                request.Headers[HttpRequestHeader.Cookie] = "pdm_session=" + token;
            if (body != null || method == "POST")
            {
                var bytes = Encoding.UTF8.GetBytes(body == null ? "{}" : Json.Serialize(body));
                request.ContentType = "application/json";
                request.ContentLength = bytes.Length;
                using (var stream = request.GetRequestStream())
                    stream.Write(bytes, 0, bytes.Length);
            }
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                    return Read(response);
            }
            catch (WebException e) when (e.Response is HttpWebResponse response)
            {
                using (response)
                    return Read(response);
            }
        }

        private static ApiResult Read(HttpWebResponse response)
        {
            string text;
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                text = reader.ReadToEnd();
            object body = null;
            if (text.Length > 0)
            {
                try { body = Json.DeserializeObject(text); }
                catch { body = text; }
            }
            return new ApiResult { Status = (int)response.StatusCode, Body = body };
        }

        /// The server's reason in words: refusals come as a JSON string, a ProblemDetails object
        /// or plain text, depending on the endpoint.
        public static string Error(ApiResult result)
        {
            if (result.Body is string text && text.Length > 0) return text;
            return Str(result.Body, "detail") ?? Str(result.Body, "title") ?? "HTTP " + result.Status;
        }

        public static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;

        public static Dictionary<string, object> Obj(object o, string key) =>
            Obj(o) != null && Obj(o).TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static string Str(object o, string key) =>
            Obj(o) != null && Obj(o).TryGetValue(key, out var v) && v != null
                ? Convert.ToString(v, CultureInfo.InvariantCulture)
                : null;

        public static bool Bool(object o, string key) =>
            Obj(o) != null && Obj(o).TryGetValue(key, out var v) && v is bool b && b;

        public static int? Int(object o, string key) =>
            Obj(o) != null && Obj(o).TryGetValue(key, out var v) && v != null && !(v is string) && !(v is bool)
                ? Convert.ToInt32(v, CultureInfo.InvariantCulture)
                : (int?)null;

        private static Tuple<string, string, string> _me;

        /// The signed-in user's id, asked once per server and login.
        public static string MyId()
        {
            var key = Settings.BaseUrl + "|" + Settings.Token;
            if (_me == null || _me.Item1 != key)
            {
                string id = null;
                try
                {
                    var me = Call("GET", "/auth/me");
                    if (me.Ok) id = Str(me.Body, "id");
                }
                catch { /* unknown: the lock then counts as someone else's */ }
                _me = Tuple.Create(key, id, "");
            }
            return _me.Item2;
        }

        public static string ItemLabel(object item)
        {
            var record = Str(item, "recordName");
            if (!string.IsNullOrEmpty(record)) return record;
            return Str(item, "itemNumber") + "(" + (Str(item, "name") ?? Str(item, "fileName")) + ")";
        }
    }
}
