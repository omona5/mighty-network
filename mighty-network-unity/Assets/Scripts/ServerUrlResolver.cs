using UnityEngine;

// ============================================================================
// 서버 WebSocket URL 결정 우선순위 (WebGL/에디터 공통)
//   1) URL 쿼리  ?ws=wss://host  또는  ?server=wss://host
//   2) PlayerPrefs 키 "mighty.serverUrl"
//   3) WebGL: 페이지와 같은 호스트 (https → wss, http → ws)
//   4) Inspector 기본값 (개발용 ws://localhost:3000)
// ============================================================================
public static class ServerUrlResolver
{
    public const string PrefsKey = "mighty.serverUrl";
    public const string DefaultUrl = "ws://localhost:3000";

    // Automatic connection uses deployment configuration, not the retired URL field.
    public static string ResolveDefault(string inspectorDefault = DefaultUrl)
    {
        string query = FromQueryParam();
        if (!string.IsNullOrEmpty(query)) return Normalize(query);
#if UNITY_WEBGL && !UNITY_EDITOR
        string page = FromPageHost();
        if (!string.IsNullOrEmpty(page)) return page;
#endif
        return Normalize(string.IsNullOrWhiteSpace(inspectorDefault) ? DefaultUrl : inspectorDefault);
    }

    public static string Resolve(string inspectorDefault)
    {
        string q = FromQueryParam();
        if (!string.IsNullOrEmpty(q)) return Normalize(q);

        string prefs = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(prefs)) return Normalize(prefs);

#if UNITY_WEBGL && !UNITY_EDITOR
        string page = FromPageHost();
        if (!string.IsNullOrEmpty(page)) return page;
#endif

        return Normalize(inspectorDefault);
    }

    public static void SavePrefs(string url)
    {
        string n = Normalize(url);
        PlayerPrefs.SetString(PrefsKey, n);
        PlayerPrefs.Save();
    }

    public static void ClearPrefs()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    public static string Normalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        string u = url.Trim();
        if (u.StartsWith("https://")) u = "wss://" + u.Substring("https://".Length);
        else if (u.StartsWith("http://")) u = "ws://" + u.Substring("http://".Length);
        // 끝 슬래시 제거
        while (u.EndsWith("/")) u = u.Substring(0, u.Length - 1);
        return u;
    }

    private static string FromQueryParam()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string abs = Application.absoluteURL;
#else
        // 에디터에서도 테스트용으로 환경변수처럼 쓰지 않음. absoluteURL은 에디터에선 비는 경우 많음.
        string abs = Application.absoluteURL;
#endif
        if (string.IsNullOrEmpty(abs)) return null;

        int q = abs.IndexOf('?');
        if (q < 0 || q >= abs.Length - 1) return null;
        string query = abs.Substring(q + 1);
        // fragment 제거
        int hash = query.IndexOf('#');
        if (hash >= 0) query = query.Substring(0, hash);

        foreach (string part in query.Split('&'))
        {
            if (string.IsNullOrEmpty(part)) continue;
            int eq = part.IndexOf('=');
            string key = eq >= 0 ? part.Substring(0, eq) : part;
            string val = eq >= 0 && eq < part.Length - 1 ? part.Substring(eq + 1) : "";
            key = UnityEngine.Networking.UnityWebRequest.UnEscapeURL(key);
            val = UnityEngine.Networking.UnityWebRequest.UnEscapeURL(val);
            if (key == "ws" || key == "server")
            {
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        return null;
    }

    private static string FromPageHost()
    {
        string abs = Application.absoluteURL;
        if (string.IsNullOrEmpty(abs)) return null;
        try
        {
            // 간단한 파싱: scheme://host[:port]/path]
            int schemeEnd = abs.IndexOf("://");
            if (schemeEnd < 0) return null;
            string scheme = abs.Substring(0, schemeEnd); // http or https
            int hostStart = schemeEnd + 3;
            int pathStart = abs.IndexOf('/', hostStart);
            int queryStart = abs.IndexOf('?', hostStart);
            int hostEnd = abs.Length;
            if (pathStart >= 0) hostEnd = Mathf.Min(hostEnd, pathStart);
            if (queryStart >= 0) hostEnd = Mathf.Min(hostEnd, queryStart);
            string hostPort = abs.Substring(hostStart, hostEnd - hostStart);
            if (string.IsNullOrEmpty(hostPort)) return null;

            if (scheme == "https") return "wss://" + hostPort;
            if (scheme == "http") return "ws://" + hostPort;
        }
        catch
        {
            return null;
        }
        return null;
    }
}
