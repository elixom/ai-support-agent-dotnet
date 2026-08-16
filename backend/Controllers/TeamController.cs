using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TeamController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;

        public TeamController(ApplicationDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        // ---------------------------------------------------------------------------
        // Team details: GET & PUT
        // ---------------------------------------------------------------------------

        [HttpGet]
        [Route("")]
        public async Task<IActionResult> GetTeam()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "You are not a member of any team." });
            return Ok(new { id = team.Id, name = team.Name, slug = team.Slug, plan = team.Plan });
        }

        public class UpdateTeamRequest
        {
            public string? Name { get; set; }
        }

        [HttpPut]
        [Route("")]
        public async Task<IActionResult> UpdateTeam([FromBody] UpdateTeamRequest req)
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "You are not a member of any team." });

            if (!string.IsNullOrEmpty(req.Name))
            {
                team.Name = req.Name.Trim();
                team.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { id = team.Id, name = team.Name, slug = team.Slug, plan = team.Plan });
        }

        // ---------------------------------------------------------------------------
        // AI API keys: GET, POST, PUT
        // ---------------------------------------------------------------------------

        [HttpGet("ai")]
        public async Task<IActionResult> GetAIConfig()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            return Ok(new
            {
                anthropic_api_key = team.AnthropicApiKey,
                openai_api_key = team.OpenaiApiKey
            });
        }

        public class AIConfigRequest
        {
            public string? Anthropic_Api_Key { get; set; } // matches anthropic_api_key in JSON
            public string? Openai_Api_Key { get; set; } // matches openai_api_key in JSON
        }

        [HttpPost("ai")]
        [HttpPut("ai")]
        public async Task<IActionResult> UpdateAIConfig([FromBody] AIConfigRequest req)
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            if (req.Anthropic_Api_Key != null) team.AnthropicApiKey = req.Anthropic_Api_Key;
            if (req.Openai_Api_Key != null) team.OpenaiApiKey = req.Openai_Api_Key;

            team.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                anthropic_api_key = team.AnthropicApiKey,
                openai_api_key = team.OpenaiApiKey
            });
        }

        // ---------------------------------------------------------------------------
        // WhatsApp Business API config: GET, POST, PUT, TEST
        // ---------------------------------------------------------------------------

        [HttpGet("whatsapp")]
        public async Task<IActionResult> GetWhatsAppConfig()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamWhatsAppConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config == null) return NotFound(new { detail = "WhatsApp config not found." });

            return Ok(new
            {
                id = config.Id,
                phone_number_id = config.PhoneNumberId,
                access_token = config.AccessToken,
                verify_token = config.VerifyToken,
                is_active = config.IsActive
            });
        }

        public class WhatsAppConfigRequest
        {
            public string? Phone_Number_Id { get; set; }
            public string? Access_Token { get; set; }
            public string? Verify_Token { get; set; }
        }

        [HttpPost("whatsapp")]
        [HttpPut("whatsapp")]
        public async Task<IActionResult> UpdateWhatsAppConfig([FromBody] WhatsAppConfigRequest req)
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamWhatsAppConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            bool isNew = false;

            if (config == null)
            {
                config = new TeamWhatsAppConfig { TeamId = team.Id };
                isNew = true;
            }

            if (req.Phone_Number_Id != null) config.PhoneNumberId = req.Phone_Number_Id.Trim();
            if (req.Access_Token != null) config.AccessToken = req.Access_Token.Trim();
            if (req.Verify_Token != null) config.VerifyToken = req.Verify_Token.Trim();

            config.IsActive = true;
            config.UpdatedAt = DateTime.UtcNow;

            if (isNew) _db.TeamWhatsAppConfigs.Add(config);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id = config.Id,
                phone_number_id = config.PhoneNumberId,
                access_token = config.AccessToken,
                verify_token = config.VerifyToken,
                is_active = config.IsActive
            });
        }

        [HttpPost("whatsapp/test")]
        public async Task<IActionResult> TestWhatsApp()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamWhatsAppConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config == null) return NotFound(new { detail = "WhatsApp config not found. Save config first." });

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync($"https://graph.facebook.com/v22.0/{config.PhoneNumberId}");

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var el = doc.RootElement;
                    return Ok(new
                    {
                        status = "connected",
                        phone_number = el.TryGetProperty("display_phone_number", out var ph) ? ph.GetString() : "",
                        quality_rating = el.TryGetProperty("quality_rating", out var qr) ? qr.GetString() : "",
                        verified_name = el.TryGetProperty("verified_name", out var vn) ? vn.GetString() : ""
                    });
                }
                else
                {
                    var errorString = await response.Content.ReadAsStringAsync();
                    return BadRequest(new { status = "failed", detail = errorString });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = "failed", detail = ex.Message });
            }
        }

        // ---------------------------------------------------------------------------
        // Gmail API config: GET, POST, PUT, DELETE
        // ---------------------------------------------------------------------------

        [HttpGet("gmail")]
        public async Task<IActionResult> GetGmailConfig()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamGmailConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config == null) return NotFound(new { detail = "Gmail config not found." });

            return Ok(new
            {
                id = config.Id,
                google_client_id = config.GoogleClientId,
                google_client_secret = config.GoogleClientSecret,
                credentials_json = config.CredentialsJson,
                watch_email = config.WatchEmail,
                is_active = config.IsActive,
                last_poll_at = config.LastPollAt
            });
        }

        public class GmailConfigRequest
        {
            public string? Google_Client_Id { get; set; }
            public string? Google_Client_Secret { get; set; }
            public string? Credentials_Json { get; set; }
            public string? Watch_Email { get; set; }
        }

        [HttpPost("gmail")]
        [HttpPut("gmail")]
        public async Task<IActionResult> UpdateGmailConfig([FromBody] GmailConfigRequest req)
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamGmailConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            bool isNew = false;

            if (config == null)
            {
                config = new TeamGmailConfig { TeamId = team.Id };
                isNew = true;
            }

            if (req.Google_Client_Id != null) config.GoogleClientId = req.Google_Client_Id.Trim();
            if (req.Google_Client_Secret != null) config.GoogleClientSecret = req.Google_Client_Secret.Trim();
            if (req.Credentials_Json != null) config.CredentialsJson = req.Credentials_Json.Trim();
            if (req.Watch_Email != null) config.WatchEmail = req.Watch_Email.Trim();

            config.IsActive = !string.IsNullOrEmpty(config.CredentialsJson);
            config.UpdatedAt = DateTime.UtcNow;

            if (isNew) _db.TeamGmailConfigs.Add(config);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id = config.Id,
                google_client_id = config.GoogleClientId,
                google_client_secret = config.GoogleClientSecret,
                credentials_json = config.CredentialsJson,
                watch_email = config.WatchEmail,
                is_active = config.IsActive,
                last_poll_at = config.LastPollAt
            });
        }

        [HttpDelete("gmail")]
        public async Task<IActionResult> DisconnectGmail()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamGmailConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config == null) return NotFound(new { detail = "Gmail config not found." });

            config.CredentialsJson = string.Empty;
            config.IsActive = false;
            config.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { detail = "Gmail disconnected." });
        }

        // ---------------------------------------------------------------------------
        // Telegram Bot API config: GET, POST, PUT
        // ---------------------------------------------------------------------------

        [HttpGet("telegram")]
        public async Task<IActionResult> GetTelegramConfig()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamTelegramConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config == null) return NotFound(new { detail = "Telegram config not found." });

            return Ok(new
            {
                id = config.Id,
                bot_token = config.BotToken,
                bot_username = config.BotUsername,
                is_active = config.IsActive
            });
        }

        public class TelegramConfigRequest
        {
            public string? Bot_Token { get; set; }
            public string? Bot_Username { get; set; }
        }

        [HttpPost("telegram")]
        [HttpPut("telegram")]
        public async Task<IActionResult> UpdateTelegramConfig([FromBody] TelegramConfigRequest req)
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamTelegramConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            bool isNew = false;

            if (config == null)
            {
                config = new TeamTelegramConfig { TeamId = team.Id };
                isNew = true;
            }

            if (req.Bot_Token != null) config.BotToken = req.Bot_Token.Trim();
            if (req.Bot_Username != null) config.BotUsername = req.Bot_Username.Trim();

            config.IsActive = true;
            config.UpdatedAt = DateTime.UtcNow;

            if (isNew) _db.TeamTelegramConfigs.Add(config);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id = config.Id,
                bot_token = config.BotToken,
                bot_username = config.BotUsername,
                is_active = config.IsActive
            });
        }

        // ---------------------------------------------------------------------------
        // Facebook Messenger config: GET, POST, PUT
        // ---------------------------------------------------------------------------

        [HttpGet("messenger")]
        public async Task<IActionResult> GetMessengerConfig()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamMessengerConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config == null) return NotFound(new { detail = "Messenger config not found." });

            return Ok(new
            {
                id = config.Id,
                page_access_token = config.PageAccessToken,
                page_id = config.PageId,
                verify_token = config.VerifyToken,
                instagram_enabled = config.InstagramEnabled,
                is_active = config.IsActive
            });
        }

        public class MessengerConfigRequest
        {
            public string? Page_Access_Token { get; set; }
            public string? Page_Id { get; set; }
            public string? Verify_Token { get; set; }
            public bool? Instagram_Enabled { get; set; }
        }

        [HttpPost("messenger")]
        [HttpPut("messenger")]
        public async Task<IActionResult> UpdateMessengerConfig([FromBody] MessengerConfigRequest req)
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "No team." });

            var config = await _db.TeamMessengerConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            bool isNew = false;

            if (config == null)
            {
                config = new TeamMessengerConfig { TeamId = team.Id };
                isNew = true;
            }

            if (req.Page_Access_Token != null) config.PageAccessToken = req.Page_Access_Token.Trim();
            if (req.Page_Id != null) config.PageId = req.Page_Id.Trim();
            if (req.Verify_Token != null) config.VerifyToken = req.Verify_Token.Trim();
            if (req.Instagram_Enabled.HasValue) config.InstagramEnabled = req.Instagram_Enabled.Value;

            config.IsActive = true;
            config.UpdatedAt = DateTime.UtcNow;

            if (isNew) _db.TeamMessengerConfigs.Add(config);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id = config.Id,
                page_access_token = config.PageAccessToken,
                page_id = config.PageId,
                verify_token = config.VerifyToken,
                instagram_enabled = config.InstagramEnabled,
                is_active = config.IsActive
            });
        }
    }

    // ---------------------------------------------------------------------------
    // Gmail OAuth Controllers: GET (initialized under api/auth/gmail/init & callback)
    // ---------------------------------------------------------------------------

    [ApiController]
    [Route("api/auth/gmail")]
    public class GmailOAuthController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public GmailOAuthController(ApplicationDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _db = db;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private static readonly string[] GmailScopes = {
            "https://www.googleapis.com/auth/gmail.readonly",
            "https://www.googleapis.com/auth/gmail.send",
            "https://www.googleapis.com/auth/gmail.modify"
        };

        [HttpGet("init")]
        [Authorize]
        public async Task<IActionResult> InitOAuth()
        {
            var team = await GetCurrentTeamAsync(_db);
            if (team == null) return NotFound(new { detail = "You are not a member of any team." });

            var clientId = string.Empty;
            var config = await _db.TeamGmailConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config != null) clientId = config.GoogleClientId;

            if (string.IsNullOrEmpty(clientId)) clientId = _configuration["GOOGLE_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");

            if (string.IsNullOrEmpty(clientId))
            {
                return BadRequest(new { detail = "Google Client ID is not configured. Enter it in Settings > Gmail." });
            }

            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/gmail/callback";
            var state = team.Id.ToString();
            var scope = string.Join(" ", GmailScopes);

            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString(scope)}&access_type=offline&prompt=consent&state={state}";

            return Ok(new { auth_url = authUrl });
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
        {
            var dashboardUrl = $"{Request.Scheme}://{Request.Host}/Dashboard/Settings";

            if (!string.IsNullOrEmpty(error))
            {
                return Redirect($"{dashboardUrl}?error={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return Redirect($"{dashboardUrl}?error=missing_code_or_state");
            }

            if (!Guid.TryParse(state, out var teamId))
            {
                return Redirect($"{dashboardUrl}?error=invalid_team");
            }

            var team = await _db.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
            if (team == null)
            {
                return Redirect($"{dashboardUrl}?error=invalid_team");
            }

            var clientId = string.Empty;
            var clientSecret = string.Empty;

            var config = await _db.TeamGmailConfigs.FirstOrDefaultAsync(c => c.TeamId == team.Id);
            if (config != null)
            {
                clientId = config.GoogleClientId;
                clientSecret = config.GoogleClientSecret;
            }

            if (string.IsNullOrEmpty(clientId)) clientId = _configuration["GOOGLE_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
            if (string.IsNullOrEmpty(clientSecret)) clientSecret = _configuration["GOOGLE_CLIENT_SECRET"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";

            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/gmail/callback";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var tokenReqData = new Dictionary<string, string>
                {
                    { "code", code ?? "" },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "redirect_uri", redirectUri },
                    { "grant_type", "authorization_code" }
                };

                var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenReqData));
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    return Redirect($"{dashboardUrl}?error=token_exchange_failed");
                }

                var responseString = await tokenResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";

                // Fetch watch email
                string watchEmail = string.Empty;
                var userInfoReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                userInfoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var userInfoResp = await client.SendAsync(userInfoReq);

                if (userInfoResp.IsSuccessStatusCode)
                {
                    var userInfoStr = await userInfoResp.Content.ReadAsStringAsync();
                    using var userInfoDoc = JsonDocument.Parse(userInfoStr);
                    watchEmail = userInfoDoc.RootElement.TryGetProperty("email", out var emProp) ? emProp.GetString() ?? "" : "";
                }

                if (config == null)
                {
                    config = new TeamGmailConfig { TeamId = team.Id };
                    _db.TeamGmailConfigs.Add(config);
                }

                config.CredentialsJson = responseString;
                config.IsActive = true;
                if (!string.IsNullOrEmpty(watchEmail)) config.WatchEmail = watchEmail;
                config.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return Redirect($"{dashboardUrl}?connected=true");
            }
            catch (Exception)
            {
                return Redirect($"{dashboardUrl}?error=token_exchange_error");
            }
        }
    }
}
