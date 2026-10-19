using SqlSugar;
using System;

namespace MySQL
{
    [SugarTable("user")]
    public class User
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int ID { get; set; }

        public string NickName { get; set; }

        public string Password { get; set; }

        public DateTime Logindate { get; set; }
        public string UserID { get; set; }   // 5位字母ID
    }
}
