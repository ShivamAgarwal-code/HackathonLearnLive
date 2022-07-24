﻿using DTOs.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DTOs.Group
{
    public class GroupDTO
    {
        public Guid Id { get; set; }
        public List<string> MembersId { get; set; }
        public int VideoChattingUsers { get; set; }
        public int OnlineUsers { get; set; }
        public string Name { get; set; }
        public string CreatorId { get; set; }
        public string Purpose { get; set; }
        public string PictureURI { get; set; }
        public List<UserDTO> MemberUsers { get; set; }
    }
}
