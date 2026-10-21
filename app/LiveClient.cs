using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace StsCardEditor
{
    public class BridgeInfo
    {
        public int protocolVersion { get; set; }
        public string layerVersion { get; set; }
        public string gameVersion { get; set; }
        public string host { get; set; }
        public int port { get; set; }
        public string token { get; set; }
        public int pid { get; set; }
        public long startedAt { get; set; }

        public static BridgeInfo Read(string gameDir)
        {
            string path = Path.Combine(Path.Combine(gameDir, "sts-card-editor"), "bridge.json");
            if (!File.Exists(path)) return null;
            BridgeInfo b = JsonUtil.FromJson<BridgeInfo>(JsonUtil.ReadUtf8(path));
            if (b == null || string.IsNullOrEmpty(b.token) || b.port <= 0) return null;
            return b;
        }
    }

    public class LiveResponse
    {
        public bool Ok;
        public bool Pending;
        public string Error;
        public string RequestId;
        public string Op;
        public long Revision;
        public Dictionary<string, object> Data;
        public Dictionary<string, object> Raw;

        public static LiveResponse Parse(Dictionary<string, object> raw)
        {
            LiveResponse r = new LiveResponse();
            r.Raw = raw;
            if (raw == null)
            {
                r.Error = "empty response";
                return r;
            }
            r.Ok = raw.ContainsKey("ok") && Convert.ToBoolean(raw["ok"]);
            r.Pending = raw.ContainsKey("pending") && Convert.ToBoolean(raw["pending"]);
            r.Error = raw.ContainsKey("error") && raw["error"] != null ? Convert.ToString(raw["error"]) : null;
            r.RequestId = raw.ContainsKey("requestId") ? Convert.ToString(raw["requestId"]) : "";
            r.Op = raw.ContainsKey("op") ? Convert.ToString(raw["op"]) : "";
            r.Revision = raw.ContainsKey("revision") ? Convert.ToInt64(raw["revision"]) : 0L;
            r.Data = raw.ContainsKey("data") && raw["data"] is Dictionary<string, object>
                ? (Dictionary<string, object>)raw["data"] : new Dictionary<string, object>();
            return r;
        }
    }

    public class LiveClient : IDisposable
    {
        private TcpClient tcp;
        private StreamReader reader;
        private StreamWriter writer;
        private string token;
        public BridgeInfo Bridge { get; private set; }
        public bool Connected { get; private set; }
        public string LastError { get; private set; }

        public void Connect(string gameDir)
        {
            Disconnect();
            Bridge = BridgeInfo.Read(gameDir);
            if (Bridge == null)
            {
                throw new InvalidOperationException("未找到运行时 bridge.json。请确认游戏已安装适配层并已通过 Steam 正常启动；适配层未加载时不能实时修改。");
            }
            if (Bridge.protocolVersion != 1)
            {
                throw new InvalidOperationException("协议版本不匹配: " + Bridge.protocolVersion);
            }
            if (!string.IsNullOrEmpty(Bridge.host) && Bridge.host != "127.0.0.1")
            {
                throw new InvalidOperationException("bridge 主机不是 loopback，已拒绝连接。");
            }
            token = Bridge.token;
            TcpClient c = new TcpClient();
            IAsyncResult ar = c.BeginConnect("127.0.0.1", Bridge.port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(2500))
            {
                c.Close();
                throw new TimeoutException("连接游戏运行时超时。");
            }
            c.EndConnect(ar);
            c.ReceiveTimeout = 6000;
            c.SendTimeout = 6000;
            tcp = c;
            reader = new StreamReader(tcp.GetStream(), new UTF8Encoding(false));
            writer = new StreamWriter(tcp.GetStream(), new UTF8Encoding(false));
            writer.NewLine = "\n";
            writer.AutoFlush = true;
            Connected = true;
            LiveResponse status = Send("status", new Dictionary<string, object>(), 0L, Guid.NewGuid().ToString("N"));
            if (!status.Ok)
            {
                Disconnect();
                throw new InvalidOperationException("适配层握手失败: " + (status.Error == null ? "unknown" : status.Error));
            }
            LastError = null;
        }

        public void Disconnect()
        {
            Connected = false;
            try { if (reader != null) reader.Close(); } catch { }
            try { if (writer != null) writer.Close(); } catch { }
            try { if (tcp != null) tcp.Close(); } catch { }
            reader = null;
            writer = null;
            tcp = null;
        }

        public void Dispose()
        {
            Disconnect();
        }

        public LiveResponse Status()
        {
            return Send("status", new Dictionary<string, object>(), 0L, Guid.NewGuid().ToString("N"));
        }

        public LiveResponse Snapshot()
        {
            return Send("snapshot", new Dictionary<string, object>(), 0L, Guid.NewGuid().ToString("N"));
        }

        public LiveResponse Capabilities()
        {
            return Send("capabilities", new Dictionary<string, object>(), 0L, Guid.NewGuid().ToString("N"));
        }

        public LiveResponse CardCatalog()
        {
            return Send("cardCatalog", new Dictionary<string, object>(), 0L, Guid.NewGuid().ToString("N"));
        }

        public LiveResponse EditDeck(Dictionary<string, object> payload, long revision)
        {
            return WaitMutation("deckEdit", payload, revision);
        }

        public LiveResponse CardPool() { return Send("cardPool", new Dictionary<string,object>(), 0L, Guid.NewGuid().ToString("N")); }
        public LiveResponse SetCardPool(Dictionary<string,object> payload, long revision) { return WaitMutation("setCardPool", payload, revision); }

        private LiveResponse WaitMutation(string op, Dictionary<string,object> payload, long revision)
        {
            string requestId = Guid.NewGuid().ToString("N");
            LiveResponse r = Send(op, payload, revision, requestId);
            if (!r.Pending) return r;
            for (int i = 0; i < 60; i++)
            {
                Thread.Sleep(250);
                r = Send("result", new Dictionary<string, object>(), revision, requestId);
                if (!r.Pending) return r;
            }
            return new LiveResponse { Ok = false, Error = "未取得最终结果，请先刷新牌组确认，不要重复提交。请求 " + requestId, RequestId = requestId };
        }

        public LiveResponse AddCard(string cardId, string destination, bool upgraded, int amount, long revision)
        {
            Dictionary<string, object> p = new Dictionary<string, object>();
            p["cardId"] = cardId;
            p["destination"] = destination;
            p["upgraded"] = upgraded;
            p["amount"] = amount;
            string requestId = Guid.NewGuid().ToString("N");
            LiveResponse r = Send("addCard", p, revision, requestId);
            if (r.Pending)
            {
                for (int i = 0; i < 80; i++)
                {
                    Thread.Sleep(250);
                    LiveResponse result = Send("result", new Dictionary<string, object>(), revision, requestId);
                    if (!result.Pending) return result;
                }
                return new LiveResponse { Ok = false, Pending = false, Error = "等待游戏线程加入卡牌超时。", RequestId = requestId, Revision = r.Revision };
            }
            return r;
        }

        public LiveResponse Validate(string kind, string cardId, string instanceId, CustomCardModel spec,
            CardOverrideModel overrideModel, List<string> scopes, long revision)
        {
            Dictionary<string, object> payload = BuildPayload(kind, cardId, instanceId, spec, overrideModel, scopes);
            return Send("validate", payload, revision, Guid.NewGuid().ToString("N"));
        }

        public LiveResponse Apply(string kind, string cardId, string instanceId, CustomCardModel spec,
            CardOverrideModel overrideModel, List<string> scopes, long revision)
        {
            Dictionary<string, object> payload = BuildPayload(kind, cardId, instanceId, spec, overrideModel, scopes);
            string requestId = Guid.NewGuid().ToString("N");
            LiveResponse r = Send("apply", payload, revision, requestId);
            if (r.Pending)
            {
                for (int i = 0; i < 80; i++)
                {
                    Thread.Sleep(250);
                    LiveResponse result = Send("result", new Dictionary<string, object>(), revision, requestId);
                    if (!result.Pending)
                    {
                        return result;
                    }
                }
                return new LiveResponse
                {
                    Ok = false,
                    Pending = false,
                    Error = "等待游戏线程应用超时（20 秒）。游戏可能停在界面切换或动作结算中。",
                    RequestId = requestId,
                    Revision = r.Revision
                };
            }
            return r;
        }

        private static Dictionary<string, object> BuildPayload(string kind, string cardId, string instanceId,
            CustomCardModel spec, CardOverrideModel overrideModel, List<string> scopes)
        {
            Dictionary<string, object> p = new Dictionary<string, object>();
            p["kind"] = kind;
            p["cardId"] = cardId;
            p["instanceId"] = instanceId == null ? "" : instanceId;
            ArrayList s = new ArrayList();
            if (scopes != null) foreach (string x in scopes) s.Add(x);
            p["scopes"] = s;
            if (spec != null) p["spec"] = spec;
            if (overrideModel != null) p["override"] = overrideModel;
            return p;
        }

        private LiveResponse Send(string op, Dictionary<string, object> payload, long revision, string requestId)
        {
            if (!Connected || tcp == null || reader == null || writer == null)
            {
                throw new InvalidOperationException("尚未连接游戏运行时。");
            }
            Dictionary<string, object> req = new Dictionary<string, object>();
            req["protocolVersion"] = 1;
            req["token"] = token;
            req["requestId"] = requestId;
            req["revision"] = revision;
            req["op"] = op;
            req["payload"] = payload;
            string line = JsonUtil.ToJson(req);
            try
            {
                writer.WriteLine(line);
                writer.Flush();
                string responseLine = reader.ReadLine();
                if (responseLine == null) throw new IOException("连接已关闭");
                Dictionary<string, object> raw = JsonUtil.FromJson(responseLine) as Dictionary<string, object>;
                return LiveResponse.Parse(raw);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Disconnect();
                throw new InvalidOperationException("游戏运行时通信失败: " + e.Message, e);
            }
        }
    }
}
