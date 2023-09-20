using BasicRedisCacingExampleInDotNetCore.Models;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace BasicRedisCacingExampleInDotNetCore.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ReposController : ControllerBase
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly HttpClient _client;
        private readonly TelemetryClient _telemetry;
        private readonly IConfiguration _config;

        public ReposController(IConnectionMultiplexer redis, HttpClient client, TelemetryClient telemetry, IConfiguration configuration)
        {
            _redis = redis;
            _client = client;
            _telemetry = telemetry;
            _config = configuration;
        }

        /// <summary>
        /// Gets the number of repos for a user/organization
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        [HttpGet("{username}")]
        public async Task<IActionResult> GetRepoCount(string username)
        {

            bool cacheDisabled = _config.GetValue<bool>("DISABLE_CACHE");
            string returnJson = string.Empty;


            var funcTimer = Stopwatch.StartNew();
            GitResponseModel dataFromServiceCall = null;
            
            if(cacheDisabled){
                dataFromServiceCall = await _client.GetFromJsonAsync<GitResponseModel>($"users/{username}");
                var data = new ResponseModel { Repos = dataFromServiceCall.PublicRepos.ToString(), Username = username, Cached = false };
                returnJson = JsonSerializer.Serialize(data);
            }
            else
            {

                var db = _redis.GetDatabase();

                //check if we've already seen that username recently

                RedisValue cache;

                var success = false;
                var startTime = DateTime.UtcNow;
                var dependencyTimer = Stopwatch.StartNew();
                try
                {
                    cache = await db.StringGetAsync($"repos:{username}");
                    success = true;
                }
                catch (Exception ex)
                {
                    _telemetry.TrackException(ex);
                    throw new Exception("Redis Exception", ex);
                }
                finally
                {
                    dependencyTimer.Stop();
                    _telemetry.TrackDependency("RedisCache", "Example Redis Cache", "CacheLookup", startTime, dependencyTimer.Elapsed, success);
                }

                if (string.IsNullOrEmpty(cache))
                {
                    //Since we haven't seen this username recently, let's grab it from the github API
                    dataFromServiceCall = await _client.GetFromJsonAsync<GitResponseModel>($"users/{username}");
                    var data = new ResponseModel { Repos = dataFromServiceCall.PublicRepos.ToString(), Username = username, Cached = true };
                    await db.StringSetAsync($"repos:{username}", JsonSerializer.Serialize(data), expiry: TimeSpan.FromMinutes(5));
                    data.Cached = false;
                    returnJson = JsonSerializer.Serialize(data);
                }
                else
                {
                    //We've seen this username recently, so let's return the cached value
                    var data = JsonSerializer.Deserialize<ResponseModel>(cache);
                    data.Cached = true;
                    returnJson = JsonSerializer.Serialize(data);
                }
            }

            funcTimer.Stop();
            TimeSpan timeTaken = funcTimer.Elapsed;
            Response.Headers.Add("x-response-time", $"{timeTaken.Seconds}s {timeTaken.Milliseconds}ms");
            return Content(returnJson, "application/json");
        }
    }
}
