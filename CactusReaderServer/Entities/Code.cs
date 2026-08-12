using FreeSql.DataAnnotations;
using System;

namespace CactusReaderService.Entities
{
    /// <summary>
    /// code 表实体：邮件验证码。
    /// 复合主键 (Email, CodeType)，三种业务（signin/reset/signup）互不覆盖；
    /// 校验通过即删除（防重放），限频（1 分钟）与有效期（5 分钟）由端点逻辑控制。
    /// </summary>
    public class Code
    {
        [Column(IsPrimary = true)]
        public string Email { get; set; }

        [Column(IsPrimary = true)]
        public string CodeType { get; set; }

        public string VerifyCode { get; set; }

        public DateTime CreateTime { get; set; }
    }
}
