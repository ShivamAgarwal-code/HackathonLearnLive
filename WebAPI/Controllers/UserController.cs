﻿using DTOs.Group;
using DTOs.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using WebAPI.Data;
using WebAPI.Data.Entities;
using WebAPI.Hubs;
using WebAPI.Twilioo;

namespace WebAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private SignInManager<ApplicationUser> signInManager;
        private UserManager<ApplicationUser> userManager;
        private ApplicationDbContext applicationDbContext;
        private IHubContext<VideoChatHub> videoChatHubContext;
        public UserController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext applicationDbContext, IHubContext<VideoChatHub> videoChatHubContext)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.applicationDbContext = applicationDbContext;
            this.videoChatHubContext = videoChatHubContext;
        }
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback([FromServices] TwilioWhatsAppService twilioService, string ReturnUrl = null)
        {
            var info = await signInManager.GetExternalLoginInfoAsync();
            if (info.Principal == null)
            {
                return Redirect("/User/Login");
            }
            var user = await userManager.FindByNameAsync(info.Principal.Identity.Name);
            if (info is not null && user is null)
            {
                ApplicationUser _user = new ApplicationUser
                {
                    UserName = info.Principal.Identity.Name,
                    IsOnline = false,
                    PictureURI = info.Principal.Claims.Where(claim => claim.Type == "picture").First().Value
                };

                var result = await userManager.CreateAsync(_user);
                
                if (result.Succeeded)
                {
                    result = await userManager.AddLoginAsync(_user, info);
                    await signInManager.SignInAsync(_user, isPersistent: false, info.LoginProvider);
                    return LocalRedirect("/");
                }
            }

            string pictureURI = info.Principal.Claims.Where(claim => claim.Type == "picture").First().Value;
            if (user.PictureURI != pictureURI)
            {
                user.PictureURI = pictureURI;
                await userManager.UpdateAsync(user);
            }

            var signInResult = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);
            return signInResult switch
            {
                Microsoft.AspNetCore.Identity.SignInResult { Succeeded: true } => LocalRedirect("/"),
                _ => Redirect("/Error")
            };
        }
        [Authorize]
        [HttpGet("Logout")]
        public async Task<ActionResult> LogoutCurrentUser()
        {
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            appUser.TabsOpen = 0;
            appUser.IsOnline = false;
            await userManager.UpdateAsync(appUser);
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await videoChatHubContext.Clients.All.SendAsync("UpdateOnlineUsers");
            return Redirect("/");
        }
        [HttpGet("BFFUser")]
        [AllowAnonymous]
        public ActionResult<BFFUserInfoDTO> GetCurrentUser([FromServices] TwilioWhatsAppService twilioService)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return BFFUserInfoDTO.Anonymous;
            }

            return new BFFUserInfoDTO()
            {
                Claims = User.Claims.Select(claim => new ClaimValueDTO { Type = claim.Type, Value = claim.Value }).ToList()
            };
        }
        [HttpGet("myPhone")]
        public async Task<SaveUserPhoneNumberDTO> GetMyPhoneNumber()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return new SaveUserPhoneNumberDTO() { MobileNumber = string.Empty };
            }
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return new SaveUserPhoneNumberDTO
            {
                MobileNumber = appUser.MobileNumber
            };
        }
        [HttpPost("SavePhoneNumber")]
        public async Task<ActionResult> SavePhoneNumber(SaveUserPhoneNumberDTO phoneNumber)
        {
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            appUser.MobileNumber = phoneNumber.MobileNumber;
            appUser.MobilePushNotifications = phoneNumber.ReceivePushNotifications;
            await userManager.UpdateAsync(appUser);
            return Ok();
        }
        [HttpGet("GetAllLearningNotes")]
        public async Task<List<LearningNoteDTO>> GetAllLearningNotes()
        {
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            return applicationDbContext.Users.Include(user => user.LearningNotes).First(s => s.Id == appUser.Id).LearningNotes.Select(s => new LearningNoteDTO
            {
                LearningMessage = s.LearningText,
                GroupName = s.Group.Name
            }).ToList();
        }
    }
}

