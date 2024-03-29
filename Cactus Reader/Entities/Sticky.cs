﻿using System;

namespace Cactus_Reader.Entities
{
    public class Sticky
    {
        public bool IsLock { get; set; }

        public DateTime CreateTime { get; set; }

        public string StickyDocument { get; set; }

        public string StickyTheme { get; set; }

        public string StickySerial { get; set; }

        public string QuickViewText { get; set; }
    }
}
