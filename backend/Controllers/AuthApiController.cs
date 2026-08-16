using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : BaseController
    {
        private readonly ApplicationDbContext _db;
        private readonly ITokenService _tokenService;

        public AuthApiController(ApplicationDbContext db, ITokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        public class SignupRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string Team_Name { get; set; } = string.Empty; // matches team_name in Django signup request JSON
        }

        [HttpPost("signup")]
        [AllowAnonymous]
        public async Task<IActionResult> Signup([FromBody] SignupRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.Team_Name))
            {
                return BadRequest(new { detail = "Email, password, and team name are required." });
            }

            var emailNormalized = req.Email.Trim().ToLower();
            if (await _db.Users.AnyAsync(u => u.Email == emailNormalized))
            {
                return BadRequest(new { detail = "A user with this email already exists." });
            }

            // Create User
            var user = new User
            {
                Email = emailNormalized,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create Team
            var slug = Slugify(req.Team_Name);
            // Ensure unique slug
            var baseSlug = slug;
            int counter = 1;
            while (await _db.Teams.AnyAsync(t => t.Slug == slug))
            {
                slug = $"{baseSlug}-{counter++}";
            }

            var team = new Team
            {
                Name = req.Team_Name.Trim(),
                Slug = slug,
                Plan = "free",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            _db.Teams.Add(team);
            await _db.SaveChangesAsync();

            // Create TeamMembership
            var membership = new TeamMembership
            {
                UserId = user.Id,
                TeamId = team.Id,
                Role = "owner",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.TeamMemberships.Add(membership);
            await _db.SaveChangesAsync();

            var token = _tokenService.GenerateAccessToken(user);
            var refresh = _tokenService.GenerateRefreshToken();

            return StatusCode(201, new
            {
                token = token,
                refresh = refresh,
                user = new { id = user.Id, email = user.Email },
                team = new { id = team.Id, name = team.Name, slug = team.Slug, plan = team.Plan }
            });
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { detail = "Email and password are required." });
            }

            var emailNormalized = req.Email.Trim().ToLower();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailNormalized);

            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            {
                return Unauthorized(new { detail = "No active account found with the given credentials" });
            }

            var membership = await _db.TeamMemberships
                .Include(tm => tm.Team)
                .FirstOrDefaultAsync(tm => tm.UserId == user.Id);

            var token = _tokenService.GenerateAccessToken(user);
            var refresh = _tokenService.GenerateRefreshToken();

            return Ok(new
            {
                token = token,
                refresh = refresh,
                user = new { id = user.Id, email = user.Email },
                team = membership != null ? new { id = membership.Team!.Id, name = membership.Team.Name, slug = membership.Team.Slug, plan = membership.Team.Plan } : null
            });
        }

        public class RefreshRequest
        {
            public string Refresh { get; set; } = string.Empty;
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public IActionResult RefreshToken([FromBody] RefreshRequest req)
        {
            // Simple refresh token stub returning a mock token while client-side state is active
            // and we can simply issue a generic success.
            if (string.IsNullOrWhiteSpace(req.Refresh))
            {
                return BadRequest(new { detail = "Refresh token is required." });
            }

            return Ok(new
            {
                access = "new_access_token_here",
                token = "new_access_token_here"
            });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var user = await GetCurrentUserAsync(_db);
            if (user == null)
            {
                return Unauthorized(new { detail = "User not found." });
            }

            var team = await GetCurrentTeamAsync(_db);

            return Ok(new
            {
                user = new { id = user.Id, email = user.Email },
                team = team != null ? new { id = team.Id, name = team.Name, slug = team.Slug, plan = team.Plan } : null
            });
        }

        private string Slugify(string phrase)
        {
            string str = phrase.ToLower();
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            str = Regex.Replace(str, @"\s+", " ").Trim();
            str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim();
            str = Regex.Replace(str, @"\s", "-");
            return str;
        }
    }
}
