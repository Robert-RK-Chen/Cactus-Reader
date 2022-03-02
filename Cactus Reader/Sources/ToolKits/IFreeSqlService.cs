namespace Cactus_Reader.Sources.ToolKits
{
    public class IFreeSqlService
    {
        private static IFreeSql instance;
        static readonly string dbConnString = DatabaseGetter.GetDatabase();

        public static IFreeSql Instance
        {
            get { return instance ?? (instance = new FreeSql.FreeSqlBuilder().UseConnectionString(FreeSql.DataType.MySql, dbConnString).Build()); }
        }

        private IFreeSqlService() { }
    }
}
