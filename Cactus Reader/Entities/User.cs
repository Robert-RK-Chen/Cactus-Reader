using System;

namespace Cactus_Reader.Entities
{
    /// <summary>
    /// 用户实体（客户端纯 POCO）。
    /// 数据库操作已迁移至 CactusReaderServer，客户端不再依赖 FreeSql；
    /// Password 字段仅用于页面间传参，服务端 API 返回的 User 不含该字段。
    /// </summary>
    public class User
    {
        public string UID { set; get; }

        public string Email { set; get; }

        public string Name { set; get; }

        public string Mobile { set; get; }

        public string Password { set; get; }

        public DateTime RegistDate { set; get; }
    }
}
