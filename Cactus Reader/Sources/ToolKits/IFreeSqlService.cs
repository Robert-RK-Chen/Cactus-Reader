namespace Cactus_Reader.Sources.ToolKits
{
    public class IFreeSqlService
    {
        private static IFreeSql instance;
        static readonly string myConnString = "Server=sh-cdb-0q4l9dac.sql.tencentcdb.com;port=59121;User ID=RobertChen;Password=#TSLover1213;Database=cactus_reader;Charset=GBK;SslMode=none;Max pool size=10";

        public static IFreeSql Instance
        {
            get { return instance ?? (instance = new FreeSql.FreeSqlBuilder().UseConnectionString(FreeSql.DataType.MySql, myConnString).Build()); }
        }

        private IFreeSqlService() { }
    }
}
