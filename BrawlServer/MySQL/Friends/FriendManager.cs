using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySQL
{
    // 好友申请结果码
    public enum FriendResult
    {
        Success = 0,        // 申请已发出 / 互赞自动成好友
        Failed = 1,         // 失败（申请自己/查无此人）
        AlreadyFriend = 2,  // 已是好友
        AlreadyApplied = 3  // 已申请过
    }

    // FriendInfo.RelState：我与对方的关系状态
    public enum RelState
    {
        None = 0,           // 无关
        Friend = 1,         // 已是好友
        Applied = 2,        // 我申请过对方（等待对方处理）
        AppliedByOther = 3  // 对方申请我
    }

    public class FriendManager : Singleton<FriendManager>
    {
        // 好友列表：status=0，我出现在任意一边
        public List<Friend> GetFriends(string uid)
        {
            try
            {
                return MySqlManager.Instance.sqlSugarClientDB
                    .Queryable<Friend>()
                    .Where(f => (f.Uid == uid || f.FriendUid == uid) && f.Status == 0)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogError("查询好友列表异常！" + e);
                return new List<Friend>();
            }
        }

        // 别人申请我的：对方在左边（Uid），我在右边（FriendUid），申请中
        public List<Friend> GetRequests(string uid)
        {
            try
            {
                return MySqlManager.Instance.sqlSugarClientDB
                    .Queryable<Friend>()
                    .Where(f => f.FriendUid == uid && f.Status == 1)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogError("查询申请列表异常！" + e);
                return new List<Friend>();
            }
        }

        // 查 A、B 之间唯一一行（不存在返回 null）
        public Friend FindRelation(string a, string b)
        {
            try
            {
                return MySqlManager.Instance.sqlSugarClientDB
                    .Queryable<Friend>()
                    .Where(f => (f.Uid == a && f.FriendUid == b) || (f.Uid == b && f.FriendUid == a))
                    .First();
            }
            catch (Exception e)
            {
                Debug.LogError("查询关系异常！" + e);
                return null;
            }
        }

        // 发起申请，返回结果码
        public FriendResult Apply(string me, string other)
        {
            try
            {
                var db = MySqlManager.Instance.sqlSugarClientDB;
                Friend row = FindRelation(me, other);
                if (row == null)
                {
                    db.Insertable(new Friend
                    {
                        Uid = me,
                        FriendUid = other,
                        Status = 1,
                        Time = DateTime.Now
                    }).ExecuteCommand();
                    return FriendResult.Success;
                }
                if (row.Status == 0)
                    return FriendResult.AlreadyFriend;
                if (row.Uid == me)
                    return FriendResult.AlreadyApplied;
                // 对方先申请我 → 互赞自动成好友
                row.Status = 0;
                row.Time = DateTime.Now;
                db.Updateable(row).ExecuteCommand();
                return FriendResult.Success;
            }
            catch (Exception e)
            {
                Debug.LogError("申请好友异常！" + e);
                return FriendResult.Failed;
            }
        }

        // 处理别人的申请：同意置0，拒绝删行；找不到该申请返回 false
        public bool HandleApply(string me, string other, bool agree)
        {
            try
            {
                var db = MySqlManager.Instance.sqlSugarClientDB;
                Friend row = db.Queryable<Friend>()
                    .Where(f => f.Uid == other && f.FriendUid == me && f.Status == 1)
                    .First();
                if (row == null)
                    return false;
                if (agree)
                {
                    row.Status = 0;
                    row.Time = DateTime.Now;
                    db.Updateable(row).ExecuteCommand();
                }
                else
                {
                    db.Deleteable<Friend>()
                        .Where(f => f.Uid == other && f.FriendUid == me)
                        .ExecuteCommand();
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("处理申请异常！" + e);
                return false;
            }
        }

        // 删除 A、B 之间所有行（好友/申请一刀切）
        public bool DeleteRelation(string a, string b)
        {
            try
            {
                int n = MySqlManager.Instance.sqlSugarClientDB
                    .Deleteable<Friend>()
                    .Where(f => (f.Uid == a && f.FriendUid == b) || (f.Uid == b && f.FriendUid == a))
                    .ExecuteCommand();
                return n > 0;
            }
            catch (Exception e)
            {
                Debug.LogError("删除关系异常！" + e);
                return false;
            }
        }
    }
}
