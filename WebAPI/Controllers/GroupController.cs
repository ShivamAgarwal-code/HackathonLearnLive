﻿using DTOs.Group;
using DTOs.User;
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

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GroupController : ControllerBase
    {
        private SignInManager<ApplicationUser> signInManager;
        private UserManager<ApplicationUser> userManager;
        private IHubContext<VideoChatHub> videoChatHub;
        private ApplicationDbContext applicationDbContext;
        public GroupController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, IHubContext<VideoChatHub> videoChatHub, ApplicationDbContext applicationDbContext)
        {
            this.signInManager = signInManager;
            this.userManager = userManager;
            this.videoChatHub = videoChatHub;
            this.applicationDbContext = applicationDbContext;
        }
        [Authorize]
        [HttpPost("CreateGroup")]
        public async Task<ActionResult> CreateGroup(GroupDTO groupDTO)
        {
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            applicationDbContext.Groups.Add(new Group
            {
                Name = groupDTO.Name,
                Purpose = groupDTO.Purpose,
                PictureURI = groupDTO.PictureURI,
                CreatorApplicationUserId = appUser.Id,
                ApplicationUsersInGroup = new List<ApplicationUserGroupMembership>
                {
                    new ApplicationUserGroupMembership
                    {
                        ApplicationUser = appUser
                    }
                }
            });
            await applicationDbContext.SaveChangesAsync();
            return Ok();
        }
        [HttpGet("GetAllGroups")]
        public async Task<List<GroupDTO>> GetAllGroups()
        {
            return applicationDbContext.Groups.Include(group => group.ApplicationUsersInGroup).ThenInclude(s => s.ApplicationUser).Select(group => new GroupDTO
            {
                Name = group.Name,
                Id = group.Id,
                OnlineUsers = group.ApplicationUsersInGroup.Where(user => user.ApplicationUser.IsOnline).Count(),
                Purpose = group.Purpose,
                PictureURI = group.PictureURI,
                MembersId = group.ApplicationUsersInGroup.Select(s => s.ApplicationUserId).ToList(),
                MemberUsers = group.ApplicationUsersInGroup.Select(s => new UserDTO { Id = s.ApplicationUserId }).ToList(),
                CreatorId = group.CreatorApplicationUserId
            }).ToList();
        }
        [HttpGet("GetGroup/{id}")]
        public async Task<GroupDTO> GetGroup(Guid id)
        {
            return applicationDbContext.Groups.Include(group => group.ApplicationUsersInGroup).ThenInclude(s => s.ApplicationUser).Where(s => s.Id == id).Select(group => new GroupDTO
            {
                Name = group.Name,
                Id = group.Id,
                OnlineUsers = group.ApplicationUsersInGroup.Where(user => user.ApplicationUser.IsOnline).Count(),
                Purpose = group.Purpose,
                PictureURI = group.PictureURI,
                MembersId = group.ApplicationUsersInGroup.Select(s => s.ApplicationUserId).ToList(),
                MemberUsers = group.ApplicationUsersInGroup.Select(s => new UserDTO { Id = s.ApplicationUserId }).ToList(),
                CreatorId = group.CreatorApplicationUserId
            }).First();
        }
        [HttpGet("JoinGroup/{groupId}")]
        public async Task<ActionResult> JoinGroup(Guid groupId)
        {
            Group group = applicationDbContext.Groups.Include(s => s.ApplicationUsersInGroup).Where(group => group.Id == groupId).First();
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (!group.ApplicationUsersInGroup.Select(s => s.ApplicationUserId).Contains(appUser.Id))
            {
                group.ApplicationUsersInGroup.Add(new ApplicationUserGroupMembership
                {
                    ApplicationUser = appUser
                });
                await applicationDbContext.SaveChangesAsync();
            }
            return Ok();
        }
        [HttpGet("LeaveGroup/{groupId}")]
        public async Task<ActionResult> LeaveGroup(Guid groupId)
        {
            Group group = applicationDbContext.Groups.Include(s => s.ApplicationUsersInGroup).Where(group => group.Id == groupId).First();
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (group.ApplicationUsersInGroup.Select(s => s.ApplicationUserId).Contains(appUser.Id))
            {
                ApplicationUserGroupMembership applicationUserGroupMembership = group.ApplicationUsersInGroup.Where(s => s.ApplicationUserId == appUser.Id).First();
                group.ApplicationUsersInGroup.Remove(applicationUserGroupMembership);
                await applicationDbContext.SaveChangesAsync();
            }
            return Ok();
        }
        [HttpGet("DeleteGroup/{groupId}")]
        public async Task<ActionResult> DeleteGroup(Guid groupId)
        {
            Group group = applicationDbContext.Groups.Include(s => s.ApplicationUsersInGroup).Where(group => group.Id == groupId).First();
            ApplicationUser appUser = await userManager.FindByIdAsync(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            if (group.CreatorApplicationUserId == null || group.CreatorApplicationUserId == appUser.Id)
            {
                applicationDbContext.Groups.Remove(group);
                await applicationDbContext.SaveChangesAsync();
            }
            return Ok();
        }
        [HttpGet("MessagesForGroup/{groupId}")]
        public async Task<List<MessagesDTO>> GetMessagesPerGroup(Guid groupId)
        {
            Group group = applicationDbContext.Groups.Include(s => s.Messages).ThenInclude(t => t.SenderApplicationUser).Where(group => group.Id == groupId).First();
            return group.Messages.ToList().Where(s => s.SenderApplicationUser != null).Select(m => new MessagesDTO
            {
                Id = m.Id,
                Content = m.TextMessage,
                GroupId = m.GroupId,
                SenderUserName = m.SenderApplicationUser.UserName
            }).ToList();
        }
        [HttpGet("LearningNotesForGroup/{groupId}")]
        public async Task<List<LearningNoteDTO>> LearningNotesPerGroup(Guid groupId)
        {
            Group group = applicationDbContext.Groups.Include(s => s.LearningNotes).ThenInclude(t => t.ApplicationUser).Where(group => group.Id == groupId).First();
            return group.LearningNotes.Select(m => new LearningNoteDTO
            {
                Id = m.Id,
                LearningMessage = m.LearningText
            }).ToList();
        }
    }
}
