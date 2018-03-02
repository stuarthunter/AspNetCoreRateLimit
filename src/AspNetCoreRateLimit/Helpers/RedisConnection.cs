using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace AspNetCoreRateLimit.Helpers
{
    public class RedisConnection
    {
        private readonly Lazy<ConnectionMultiplexer> _redis;
        private readonly Dictionary<string, Lazy<LoadedLuaScript>> _scripts = new Dictionary<string, Lazy<LoadedLuaScript>>();

        // TODO: set connection string using IOptions

        public RedisConnection(string connectionString)
        {
            _redis = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(connectionString));
        }

        public IDatabase Database => _redis.Value.GetDatabase();

        public void RegisterScript(string key, string script)
        {
            _scripts[key] = new Lazy<LoadedLuaScript>(() => LoadScript(script));
        }

        public async Task<RedisResult> ExecuteScriptAsync(string key, object ps)
        {
            var loadedScript = _scripts[key];
            var retryOnNoScript = loadedScript.IsValueCreated; // prevent recursive loop

            try
            {
                return await loadedScript.Value.EvaluateAsync(Database, ps);
            }
            catch (RedisServerException ex)
            {
                // handle missing script after Redis restart
                if (ex.Message.StartsWith("NOSCRIPT") && retryOnNoScript)
                {
                    var script = loadedScript.Value.OriginalScript;
                    _scripts[key] = new Lazy<LoadedLuaScript>(() => LoadScript(script));
                    return await ExecuteScriptAsync(key, ps);
                }
                throw;
            }
        }

        private LoadedLuaScript LoadScript(string script)
        {
            var preparedScript = LuaScript.Prepare(script);
            LoadedLuaScript loadedScript = null;

            // load script on all servers
            // note: only need to store single instance of loaded script as it is a wrapper around the original prepared script
            foreach (var server in _redis.Value.GetEndPoints().Select(x => _redis.Value.GetServer(x)))
            {
                loadedScript = preparedScript.Load(server);
            }

            return loadedScript;
        }
    }
}
