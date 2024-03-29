﻿using FreeSql.DataAnnotations;
using System;
using System.Collections.Generic;

namespace Cactus_Reader.Entities
{
    public class User
    {
        public User()
        {
            Userkeys = new HashSet<Userkey>();
        }

        [Column(IsPrimary = true)]
        public string UID { set; get; }

        public string Email { set; get; }

        public string Name { set; get; }

        public string Mobile { set; get; }

        public string Password { set; get; }

        public DateTime RegistDate { set; get; }

        public virtual Privatekey Privatekey { get; set; }

        public virtual ICollection<Userkey> Userkeys { get; set; }
    }
}
