using Microsoft.Extensions.Caching.Distributed;
using SMEFLOWSystem.Application.Interfaces.IServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class OTPService : IOTPService   
    {
        private readonly IDistributedCache _cache;

        public OTPService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public string GenerateOtp()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        public async Task StoreOtpAsync(string email, string otp)
        {
            var key = $"otp:{email.ToLower()}";
            await _cache.SetStringAsync(key, otp, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
        }

        public async Task<bool> VerifyOtpAsync(string email, string otp)
        {
            var key = $"otp:{email.ToLower()}";
            var storedOtp = await _cache.GetStringAsync(key);
            return storedOtp == otp;
        }
    }
}
