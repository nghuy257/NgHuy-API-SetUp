using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace NgHuyAPI
{
    /// <summary>Live status of a key as reported by /api/public/check-key.</summary>
    internal sealed class KeyStatus
    {
        internal string Status { get; set; }
        internal bool Active { get; set; }
        internal string Product { get; set; }
        internal int UsedQuota { get; set; }
        internal int RemainQuota { get; set; }
        internal int TotalQuota { get; set; }
        internal bool UnlimitedQuota { get; set; }
        internal long ExpiredTime { get; set; }

        internal string ShortSummary
        {
            get
            {
                List<string> parts = new List<string>();
                parts.Add(PackageLabel(Product, TotalQuota, UnlimitedQuota));
                if (!UnlimitedQuota)
                {
                    parts.Add("left " + QuotaLabel(RemainQuota));
                }
                string expiry = ExpiryLabel(ExpiredTime);
                if (!string.IsNullOrEmpty(expiry))
                {
                    parts.Add("until " + expiry);
                }
                if (!Active)
                {
                    parts.Add(string.IsNullOrEmpty(Status) ? "inactive" : Status);
                }
                return string.Join(" | ", parts.ToArray());
            }
        }

        internal static string PackageLabel(string product, int quota, bool unlimited)
        {
            string name = string.IsNullOrWhiteSpace(product) ? "key" : product.ToUpperInvariant();
            return name + " " + (unlimited ? "UNLIMITED" : QuotaLabel(quota));
        }

        internal static string QuotaLabel(long quota)
        {
            if (quota <= 0)
            {
                return "0";
            }
            if (quota >= 1000000000L)
            {
                return Trim(quota / 1000000000d) + "B";
            }
            if (quota >= 1000000L)
            {
                return Trim(quota / 1000000d) + "M";
            }
            if (quota >= 1000L)
            {
                return Trim(quota / 1000d) + "K";
            }
            return quota.ToString(CultureInfo.InvariantCulture);
        }

        internal static string ExpiryLabel(long unixSeconds)
        {
            if (unixSeconds <= 0)
            {
                return string.Empty;
            }
            DateTime local = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixSeconds).ToLocalTime();
            return local.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }

        private static string Trim(double value)
        {
            string text = value.ToString("0.#", CultureInfo.InvariantCulture);
            return text;
        }
    }

    /// <summary>Talks to the public (unauthenticated) key endpoints of the gateway.</summary>
    internal static class KeyStatusClient
    {
        /// <summary>
        /// Extracts an sk- API key from a nghuyapi:// link
        /// (nghuyapi://sk-..., nghuyapi://open/sk-...).
        /// </summary>
        internal static string FromProtocolLink(string link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return null;
            }
            string value = link.Trim();
            if (!value.StartsWith("nghuyapi:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            value = value.Substring("nghuyapi:".Length).Trim('/', '\\', ' ');
            int query = value.IndexOf('?');
            if (query >= 0)
            {
                value = value.Substring(0, query);
            }
            value = Uri.UnescapeDataString(value);
            string[] parts = value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }
            string last = parts[parts.Length - 1].Trim();
            if (last.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
            {
                return last;
            }
            return null;
        }

        internal static async Task<KeyStatus> CheckAsync(string key)
        {
            using (HttpClient client = CreateClient())
            {
                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["key"] = (key ?? string.Empty).Trim();
                ApiEnvelope response = await PostAsync(client, AppPaths.ClaudeCodeBaseUrl + "/api/public/check-key", payload).ConfigureAwait(false);
                if (!response.Success)
                {
                    throw new InvalidOperationException(response.Message);
                }

                Dictionary<string, object> data = response.Data ?? new Dictionary<string, object>();
                return new KeyStatus
                {
                    Status = Str(data, "status"),
                    Active = Bool(data, "active"),
                    Product = Str(data, "product"),
                    UsedQuota = Int(data, "used_quota"),
                    RemainQuota = Int(data, "remain_quota"),
                    TotalQuota = Int(data, "total_quota"),
                    UnlimitedQuota = Bool(data, "unlimited_quota"),
                    ExpiredTime = Long(data, "expired_time")
                };
            }
        }

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "NgHuyAPI/1.2");
            return client;
        }

        private static async Task<ApiEnvelope> PostAsync(HttpClient client, string url, Dictionary<string, object> payload)
        {
            ApiEnvelope envelope = new ApiEnvelope();
            try
            {
                using (StringContent content = new StringContent(Json.Serialize(payload), Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = await client.PostAsync(url, content).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Dictionary<string, object> root;
                    try
                    {
                        root = Json.DeserializeDictionary(body);
                    }
                    catch
                    {
                        envelope.Message = "Gateway returned HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                        return envelope;
                    }

                    envelope.Success = Bool(root, "success");
                    envelope.Message = Str(root, "message");
                    envelope.Data = Json.AsDictionary(Get(root, "data"));
                    if (!envelope.Success && string.IsNullOrWhiteSpace(envelope.Message))
                    {
                        envelope.Message = "Gateway returned HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
                    }
                    return envelope;
                }
            }
            catch (Exception ex)
            {
                envelope.Success = false;
                envelope.Message = ex.Message;
                return envelope;
            }
        }

        private static object Get(Dictionary<string, object> values, string name)
        {
            object value;
            return values != null && values.TryGetValue(name, out value) ? value : null;
        }

        private static string Str(Dictionary<string, object> values, string name)
        {
            object value = Get(values, name);
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static bool Bool(Dictionary<string, object> values, string name)
        {
            object value = Get(values, name);
            if (value is bool)
            {
                return (bool)value;
            }
            bool parsed;
            return value != null && bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed;
        }

        private static int Int(Dictionary<string, object> values, string name)
        {
            long value = Long(values, name);
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }
            if (value < int.MinValue)
            {
                return int.MinValue;
            }
            return (int)value;
        }

        private static long Long(Dictionary<string, object> values, string name)
        {
            object value = Get(values, name);
            if (value == null)
            {
                return 0;
            }
            long parsed;
            if (long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            double asDouble;
            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out asDouble))
            {
                return (long)asDouble;
            }
            return 0;
        }

        private sealed class ApiEnvelope
        {
            internal bool Success { get; set; }
            internal string Message { get; set; }
            internal Dictionary<string, object> Data { get; set; }
        }
    }

    /// <summary>Registers the nghuyapi:// link handler for the current user (no admin rights needed).</summary>
    internal static class ProtocolRegistrar
    {
        internal const string Scheme = "nghuyapi";

        internal static void Register(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                return;
            }

            string command = "\"" + exePath + "\" \"%1\"";
            using (RegistryKey root = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + Scheme))
            {
                if (root == null)
                {
                    return;
                }
                root.SetValue(string.Empty, "URL:NgHuy API", RegistryValueKind.String);
                root.SetValue("URL Protocol", string.Empty, RegistryValueKind.String);
                using (RegistryKey icon = root.CreateSubKey("DefaultIcon"))
                {
                    if (icon != null)
                    {
                        icon.SetValue(string.Empty, "\"" + exePath + "\",0", RegistryValueKind.String);
                    }
                }
                using (RegistryKey open = root.CreateSubKey(@"shell\open\command"))
                {
                    if (open != null)
                    {
                        string existing = Convert.ToString(open.GetValue(string.Empty, string.Empty));
                        if (!string.Equals(existing, command, StringComparison.OrdinalIgnoreCase))
                        {
                            open.SetValue(string.Empty, command, RegistryValueKind.String);
                        }
                    }
                }
            }
        }

        internal static bool IsRegistered(string exePath)
        {
            try
            {
                using (RegistryKey open = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + Scheme + @"\shell\open\command"))
                {
                    if (open == null)
                    {
                        return false;
                    }
                    string existing = Convert.ToString(open.GetValue(string.Empty, string.Empty));
                    return existing.IndexOf(exePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
