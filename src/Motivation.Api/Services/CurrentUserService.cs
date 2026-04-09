using System;
using Microsoft.AspNetCore.Http;
using Motivation.Application.Interfaces;

namespace Motivation.Api.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? GetUserId()
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("sub");
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                return null;
            return userId;
        }
    }
}
