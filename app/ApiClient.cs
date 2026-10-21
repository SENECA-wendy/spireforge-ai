using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace StsCardEditor
{
    public static class ApiClient
    {
        public static string GenerateCard(ApiSettings settings, string userText, string color="RED")
        {
            if(Array.IndexOf(Professions.CustomColors,color)<0)throw new InvalidDataException("无效的卡牌职业归属。");
            string system = "你是《杀戮尖塔 1》的卡牌规格生成器。只输出一个 JSON 对象，不要输出 Markdown 或解释。"
                + "JSON 字段：id（必须以 stseditor_ 开头）、name、description、color（"+color+"，职业为"+Professions.Name(color)+"）、type（ATTACK/SKILL/POWER）、"
                + "rarity（COMMON/UNCOMMON/RARE）、target（ENEMY/ALL_ENEMY/SELF/NONE）、cost、image（red/attack/bash、red/skill/shrug_it_off 或 red/power/metallicize）、"
                + "exhaust（可选）、effects（数组）。每个 effect 字段：type、amount、upgradeAmount（可选）、power（仅 APPLY_POWER）、target。"
                + "支持的 effect.type：DAMAGE、BLOCK、DRAW、GAIN_ENERGY、LOSE_HP、HEAL、DISCARD、APPLY_POWER、START_TURN_BLOCK、DRAW_ON_PLAY、ALL_CARDS_ZERO_COST。"
                + "所有牌0费使用 ALL_CARDS_ZERO_COST、target=SELF、amount=1，持续本场战斗，包括后来抽到/生成的固定费用牌；X费和不可打出的牌除外。"
                + "不能实现的需求必须返回 {\"supported\":false,\"reason\":\"具体未实现机制\"}；严禁用敏捷等占位、替代或漏掉效果。不要凭空生成未安装的 Java 扩展。"
                + "APPLY_POWER 的 power 只支持 Strength、Dexterity、Vulnerable、Weak、Frail、Poison、Thorns，amount 必须至少为 1。"
                + "如果描述是“每当你打出一张牌，抽 X 张牌”，使用 DRAW_ON_PLAY，amount=X，target=SELF；不要用 APPLY_POWER 0 占位。"
                + "upgradeAmount 是升级后的最终数值，不是增量；例如初始 9、升级后 12，就填 12。"
                + "字段名必须严格使用上述名称，不要使用 upgrade_amount、debuff 等别名。"
                + "description 使用 !D!、!B!、!M! 占位符，让游戏根据数值显示。不要生成 Java 代码、脚本或命令。";
            string payload = BuildPayload(settings, system, userText);
            string response = Post(settings, payload);
            string reason;
            string content = TryExtractContent(response, out reason);
            string firstResponse = response;
            if (content == null)
            {
                // Some providers occasionally return a 200 response with an empty choices array.
                // Retry once before giving up, but keep the raw response for diagnosis.
                response = Post(settings, payload);
                content = TryExtractContent(response, out reason);
            }
            if (content == null)
            {
                throw new InvalidDataException(
                    "模型没有返回可用内容。\n"
                    + (string.IsNullOrEmpty(reason) ? "" : "原因: " + reason + "\n")
                    + "\n模型服务响应:\n" + PreviewResponse(response)
                    + (firstResponse == response ? "" : "\n\n首次响应:\n" + PreviewResponse(firstResponse))
                    + "\n\n请检查模型名称是否为服务商文档中的 API ID，或稍后重试。");
            }
            string json = ExtractJson(content);
            Dictionary<string, object> specification = JsonUtil.FromJson(json) as Dictionary<string, object>;
            if (specification != null && specification.ContainsKey("supported") && !Convert.ToBoolean(specification["supported"]))
                throw new InvalidDataException("该效果尚未实现：" + (specification.ContainsKey("reason") ? Convert.ToString(specification["reason"]) : "模型声明不支持"));
            CustomCardModel card = JsonUtil.FromJson<CustomCardModel>(json);
            List<string> intentErrors = CardGenerationRules.ValidateIntent(userText, card);
            if (intentErrors.Count > 0) throw new InvalidDataException(string.Join("\n", intentErrors.ToArray()));
            card.color=color;return JsonUtil.ToJson(card);
        }

        public static string TestConnection(ApiSettings settings)
        {
            string payload = BuildPayload(settings, "你是一个连接测试助手。只回复 OK。", "回复 OK");
            string response = Post(settings, payload);
            return response.Length > 500 ? response.Substring(0, 500) : response;
        }

        private static string TryExtractContent(string response, out string reason)
        {
            reason = null;
            object parsed;
            try
            {
                parsed = JsonUtil.FromJson(response);
            }
            catch (Exception e)
            {
                reason = "响应不是有效 JSON: " + e.Message;
                return null;
            }

            Dictionary<string, object> root = parsed as Dictionary<string, object>;
            if (root == null)
            {
                reason = "响应不是 JSON 对象";
                return null;
            }
            if (!root.ContainsKey("choices"))
            {
                if (root.ContainsKey("error"))
                {
                    reason = "服务端返回错误: " + Convert.ToString(JsonUtil.ToJson(root["error"]));
                }
                else
                {
                    reason = "响应缺少 choices 字段，可能不是聊天补全接口";
                }
                return null;
            }

            // JavaScriptSerializer returns object[] for JSON arrays; some other
            // parsers return ArrayList. Both implement the non-generic IList.
            System.Collections.IList choices = root["choices"] as System.Collections.IList;
            if (choices == null)
            {
                reason = "choices 不是数组";
                return null;
            }
            if (choices.Count == 0)
            {
                reason = "choices 数组为空";
                return null;
            }

            Dictionary<string, object> choice = choices[0] as Dictionary<string, object>;
            if (choice == null)
            {
                reason = "choices[0] 格式无效";
                return null;
            }
            Dictionary<string, object> message = choice.ContainsKey("message")
                ? choice["message"] as Dictionary<string, object>
                : null;
            string content = message != null && message.ContainsKey("content")
                ? Convert.ToString(message["content"])
                : null;
            if (string.IsNullOrEmpty(content))
            {
                reason = "choices[0].message.content 为空";
                if (choice.ContainsKey("finish_reason") && choice["finish_reason"] != null)
                {
                    reason += "，finish_reason=" + Convert.ToString(choice["finish_reason"]);
                }
                if (message != null && message.ContainsKey("reasoning_content")
                    && !string.IsNullOrEmpty(Convert.ToString(message["reasoning_content"])))
                {
                    reason += "，只返回了 reasoning_content";
                }
                return null;
            }
            return content;
        }

        private static string PreviewResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return "(空响应)";
            string s = response.Trim();
            if (s.Length > 4000) s = s.Substring(0, 4000) + "\n...（已截断）";
            return s;
        }

        public static string GenerateRaw(ApiSettings settings, string system, string user, double creativity)
        {
            string response = Post(settings, BuildPayload(settings, system, user, creativity));
            string reason;
            string content = TryExtractContent(response, out reason);
            if (content == null) throw new InvalidDataException("模型没有返回设计结果：" + reason);
            return ExtractJson(content);
        }

        private static string BuildPayload(ApiSettings settings, string system, string user)
        {
            return BuildPayload(settings, system, user, 1.0);
        }

        private static string BuildPayload(ApiSettings settings, string system, string user, double creativity)
        {
            List<object> messages = new List<object>();
            Dictionary<string, object> m1 = new Dictionary<string, object>();
            m1["role"] = "system";
            m1["content"] = system;
            messages.Add(m1);
            Dictionary<string, object> m2 = new Dictionary<string, object>();
            m2["role"] = "user";
            m2["content"] = user;
            messages.Add(m2);
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["model"] = settings.model;
            root["messages"] = messages;
            root["temperature"] = Math.Max(0.0, Math.Min(1.0, creativity));
            root["stream"] = false;
            return JsonUtil.ToJson(root);
        }

        private static string Post(ApiSettings settings, string payload)
        {
            if (settings == null || string.IsNullOrEmpty(settings.baseUrl)) throw new InvalidOperationException("请先配置 Base URL。");
            if (string.IsNullOrEmpty(settings.model)) throw new InvalidOperationException("请先配置模型名称。");
            if (string.IsNullOrEmpty(settings.apiKey)) throw new InvalidOperationException("请先配置 API Key。");

            List<string> urls = BuildEndpointCandidates(settings.baseUrl);
            ApiHttpException last = null;
            for (int i = 0; i < urls.Count; i++)
            {
                try
                {
                    return PostOne(settings, urls[i], payload);
                }
                catch (ApiHttpException e)
                {
                    e.AttemptedUrls = urls;
                    last = e;
                    bool canRetry = i + 1 < urls.Count && (e.StatusCode == 404 || e.StatusCode == 405);
                    if (canRetry) continue;
                    throw new InvalidOperationException(e.ToUserMessage(), e);
                }
            }
            throw new InvalidOperationException(last != null ? last.ToUserMessage() : "模型请求失败：没有可用的接口地址。");
        }

        private static string PostOne(ApiSettings settings, string url, string payload)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "POST";
                req.ContentType = "application/json; charset=utf-8";
                req.Headers["Authorization"] = "Bearer " + settings.apiKey;
                req.UserAgent = "STSCardEditor/1.1.5";
                req.Timeout = Math.Max(5, settings.timeoutSeconds) * 1000;
                req.ReadWriteTimeout = Math.Max(5, settings.timeoutSeconds) * 1000;
                byte[] data = Encoding.UTF8.GetBytes(payload);
                req.ContentLength = data.Length;
                using (Stream s = req.GetRequestStream())
                {
                    s.Write(data, 0, data.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    string contentType = resp.ContentType == null ? "" : resp.ContentType;
                    string body;
                    using (StreamReader r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        body = r.ReadToEnd();
                    }
                    if (LooksLikeMarkup(contentType, body))
                    {
                        throw new ApiHttpException(url, (int)resp.StatusCode, body, contentType,
                            "服务器返回的是网页/XML，不是聊天补全 JSON。");
                    }
                    return body;
                }
            }
            catch (ApiHttpException)
            {
                throw;
            }
            catch (WebException e)
            {
                int status = 0;
                string contentType = "";
                string detail = "";
                HttpWebResponse response = e.Response as HttpWebResponse;
                if (response != null)
                {
                    try
                    {
                        status = (int)response.StatusCode;
                        contentType = response.ContentType == null ? "" : response.ContentType;
                        using (StreamReader r = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                        {
                            detail = r.ReadToEnd();
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        response.Close();
                    }
                }
                throw new ApiHttpException(url, status, detail, contentType, e.Message);
            }
            catch (Exception e)
            {
                throw new ApiHttpException(url, 0, "", "", e.Message);
            }
        }

        private static bool LooksLikeMarkup(string contentType, string body)
        {
            if (!string.IsNullOrEmpty(contentType)
                && (contentType.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0
                    || contentType.IndexOf("xml", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
            if (string.IsNullOrEmpty(body)) return false;
            string trimmed = body.TrimStart();
            return trimmed.StartsWith("<", StringComparison.Ordinal);
        }

        private static List<string> BuildEndpointCandidates(string raw)
        {
            if (string.IsNullOrEmpty(raw)) throw new InvalidOperationException("请先配置 Base URL。");
            string text = raw.Trim().Trim('"', '\'').Trim();
            Uri uri;
            if (!Uri.TryCreate(text, UriKind.Absolute, out uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("Base URL 必须是完整的 http 或 https 地址，例如 https://api.deepseek.com/v1。");
            }

            string host = uri.Host.ToLowerInvariant();
            string path = uri.AbsolutePath == null ? "" : uri.AbsolutePath.TrimEnd('/');
            if (host == "platform.deepseek.com" || host == "chat.deepseek.com" || host == "www.deepseek.com")
            {
                throw new InvalidOperationException(
                    "这里填的是 DeepSeek 网页控制台，不是 API 地址。\n"
                    + "DeepSeek 官方 API 的 Base URL 应填：https://api.deepseek.com/v1\n"
                    + "API Key 请到 platform.deepseek.com 的 API keys 页面创建，但不要把网页地址填到这里。");
            }
            if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Base URL 指向的是网页文件，不是 API 接口。请填写服务商 API 文档中的地址。");
            }
            if (!string.IsNullOrEmpty(uri.Query) && !path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Base URL 带查询参数时，请直接填写完整的 .../chat/completions 接口地址。");
            }

            string baseUrl = text.TrimEnd('/');
            List<string> urls = new List<string>();
            if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(baseUrl);
            }
            else if (path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(baseUrl + "/chat/completions");
            }
            else
            {
                // 大多数 OpenAI 兼容服务使用 /v1/chat/completions；
                // 也尝试 /chat/completions，兼容把版本前缀省略的服务。
                urls.Add(baseUrl + "/v1/chat/completions");
                urls.Add(baseUrl + "/chat/completions");
            }

            List<string> unique = new List<string>();
            foreach (string u in urls)
            {
                if (!unique.Contains(u)) unique.Add(u);
            }
            return unique;
        }

        private sealed class ApiHttpException : Exception
        {
            public string Url;
            public int StatusCode;
            public string ResponseBody;
            public string ContentType;
            public List<string> AttemptedUrls;

            public ApiHttpException(string url, int statusCode, string body, string contentType, string message)
                : base(message)
            {
                Url = url;
                StatusCode = statusCode;
                ResponseBody = body == null ? "" : body;
                ContentType = contentType == null ? "" : contentType;
            }

            public string ToUserMessage()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("模型请求失败");
                if (StatusCode > 0) sb.Append("（HTTP ").Append(StatusCode).Append("）");
                sb.AppendLine("。");
                sb.AppendLine("请求地址: " + Url);
                if (!string.IsNullOrEmpty(ContentType)) sb.AppendLine("响应类型: " + ContentType);
                if (!string.IsNullOrEmpty(ResponseBody))
                {
                    string body = ResponseBody;
                    if (body.Length > 1200) body = body.Substring(0, 1200) + "\n...（已截断）";
                    sb.AppendLine("服务端返回:");
                    sb.AppendLine(body.Trim());
                }
                else if (!string.IsNullOrEmpty(Message))
                {
                    sb.AppendLine("错误信息: " + Message);
                }
                sb.AppendLine();
                sb.AppendLine("请检查 Base URL：");
                sb.AppendLine("- 应填服务商 API 文档中的地址，例如 DeepSeek 官方为 https://api.deepseek.com/v1");
                sb.AppendLine("- 也可以直接填完整的 .../v1/chat/completions 地址");
                sb.AppendLine("- 不要填 platform.deepseek.com 等网页控制台、下载页、对象存储(OSS)地址");
                sb.AppendLine("- 模型名称填 API 文档里的 ID（例如 deepseek-chat），不是网页上显示的模型名称");
                if (StatusCode == 401 || StatusCode == 403)
                {
                    sb.AppendLine("- 401/403 表示 API Key 无效、过期或没有该模型权限；地址本身通常没有问题。");
                }
                else if (StatusCode == 404 || StatusCode == 405)
                {
                    sb.AppendLine("- 404/405 通常表示该地址没有聊天补全接口，或服务商要求不同的路径。");
                }
                else if (StatusCode == 429)
                {
                    sb.AppendLine("- 429 表示频率或额度受限；请稍后重试或检查账户余额。");
                }
                else if (StatusCode == 0)
                {
                    sb.AppendLine("- 没有拿到 HTTP 响应：请检查网络、代理、防火墙和地址是否可访问。");
                }
                return sb.ToString().TrimEnd();
            }
        }

        private static string ExtractJson(string content)
        {
            string s = content.Trim();
            if (s.StartsWith("```"))
            {
                int first = s.IndexOf('\n');
                if (first >= 0) s = s.Substring(first + 1);
                int end = s.LastIndexOf("```", StringComparison.Ordinal);
                if (end >= 0) s = s.Substring(0, end);
            }
            int a = s.IndexOf('{');
            int b = s.LastIndexOf('}');
            if (a < 0 || b <= a) throw new InvalidDataException("模型返回内容中没有 JSON 对象。");
            return s.Substring(a, b - a + 1);
        }
    }
}
