using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    public class BaseController : ControllerBase
    {
        protected Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }
            return Guid.Parse(userIdStr);
        }

        protected async Task<User?> GetCurrentUserAsync(ApplicationDbContext db)
        {
            try
            {
                var userId = GetCurrentUserId();
                return await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            }
            catch
            {
                return null;
            }
        }

        protected async Task<Team?> GetCurrentTeamAsync(ApplicationDbContext db)
        {
            try
            {
                var userId = GetCurrentUserId();
                var membership = await db.TeamMemberships
                    .Include(tm => tm.Team)
                    .FirstOrDefaultAsync(tm => tm.UserId == userId);

                return membership?.Team;
            }
            catch
            {
                return null;
            }
        }
    }
}
