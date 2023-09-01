using BasicRedisCacingExampleInDotNetCore.Models;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
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

        public ReposController(IConnectionMultiplexer redis, HttpClient client, TelemetryClient telemetry)
        {
            _redis = redis;
            _client = client;
            _telemetry=telemetry;
        }

        /// <summary>
        /// Gets the number of repos for a user/organization
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        [HttpGet("{username}")]
        public async Task<IActionResult> GetRepoCount(string username)
        {
            var funcTimer = Stopwatch.StartNew();
            var db = _redis.GetDatabase();

            //check if we've already seen that username recently

            RedisValue cache = null;


            // var success = false;
            // var startTime = DateTime.UtcNow;
            // var timer = System.Diagnostics.Stopwatch.StartNew();
            // try
            // {
            //     cache = await db.StringGetAsync($"repos:{username}");
            //     success = true;
            // }
            // catch (Exception ex)
            // {
            //     _telemetry.TrackException(ex);
            //     throw new Exception("Redis Exception", ex);
            // }
            // finally
            // {
            //     timer.Stop();
            //     _telemetry.TrackDependency("RedisCache", "Example Redis Cache", "CacheLookup", startTime, timer.Elapsed, success);
            // }

            if (string.IsNullOrEmpty(cache))
            {
                //Since we haven't seen this username recently, let's grab it from the github API
                var gitData = await _client.GetFromJsonAsync<GitResponseModel>($"users/{username}");
                var data = new ResponseModel { Repos = gitData.PublicRepos.ToString(), Username = username, Cached = true };
                await db.StringSetAsync($"repos:{username}", JsonSerializer.Serialize(data), expiry: TimeSpan.FromMinutes(5));
                data.Cached = false;
                cache = JsonSerializer.Serialize(data);
            }

            funcTimer.Stop();
            TimeSpan timeTaken = funcTimer.Elapsed;
            Response.Headers.Add("x-response-time", $"{timeTaken.Seconds}s {timeTaken.Milliseconds}ms");
            return Content(cache, "application/json");
        }
    }
}
