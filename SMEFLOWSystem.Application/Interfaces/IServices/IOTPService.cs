using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Interfaces.IServices
{
    public interface IOTPService
    {
        string GenerateOtp();
        Task StoreOtpAsync(string email, string otp);
        Task<bool> VerifyOtpAsync(string email, string otp);
    }
}
