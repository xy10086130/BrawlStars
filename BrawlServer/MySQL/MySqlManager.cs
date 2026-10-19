using SqlSugar;
using System;


namespace MySQL
{
    public class MySqlManager : Singleton<MySqlManager>
    {
        public SqlSugarClient sqlSugarClientDB;
        public bool Init()
        {
            try
            {
                sqlSugarClientDB = new SqlSugarClient(
                    new ConnectionConfig
                    {
                        ConnectionString =
                            "server=localhost;uid=root;pwd=123456;database=brawlstars;SslMode=none;charset=utf8mb4",
                        DbType = DbType.MySql,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute
                    });

                sqlSugarClientDB.Ado.GetInt("SELECT 1");
                sqlSugarClientDB.CodeFirst.InitTables(typeof(User), typeof(Friend));


                Debug.LogInfo("启动数据库成功！");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("数据库启动失败！" + e);
                return false;
            }
        }
    }
}
