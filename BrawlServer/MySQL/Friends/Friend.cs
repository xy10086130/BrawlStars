using SqlSugar;
using System;

namespace MySQL
{
    [SugarTable("friend")]
    public class Friend
    {
        // 每对关系一行：Uid = 关系发起方
        [SugarColumn(IsPrimaryKey = true)]
        public string Uid { get; set; }

        [SugarColumn(IsPrimaryKey = true)]
        public string FriendUid { get; set; }

        public int Status { get; set; }   // 0=好友 1=申请中

        public DateTime Time { get; set; }
    }
}
