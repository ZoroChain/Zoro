using Microsoft.AspNetCore.Http;
using Zoro.IO.Json;

namespace Zoro.Plugins
{
    public interface IRpcPlugin
    {
        JObject OnProcess(HttpContext context, string method, JArray _params);
    }
}
