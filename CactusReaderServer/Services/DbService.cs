using FreeSql;

namespace CactusReaderService.Services
{
    /// <summary>
    /// 数据库访问服务（单一职责：持有 FreeSql 实例）。
    /// 连接串来自 appsettings.json 的 ConnectionStrings:MySql，客户端不再接触数据库凭据。
    /// </summary>
    public class DbService
    {
        public IFreeSql FreeSql { get; }

        public DbService(string connectionString)
        {
            // FreeSqlBuilder.Build() 为惰性连接，构造时不会真正连库；
            // 首次查询失败时由端点统一捕获并返回错误。
            FreeSql = new FreeSqlBuilder()
                .UseConnectionString(DataType.MySql, connectionString)
                .UseAutoSyncStructure(false)
                .Build();
        }
    }
}
