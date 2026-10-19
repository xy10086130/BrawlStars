using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySQL
{
    public class UserManager : Singleton<UserManager>
    {
        // 注册
        public RegisterResult Register(string nickName, string password, out string userID)
        {
            userID = null;

            try
            {
                if (string.IsNullOrWhiteSpace(nickName) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    return RegisterResult.Failed;
                }

                var db = MySqlManager.Instance.sqlSugarClientDB;

                bool alreadyExist = db.Queryable<User>()
                    .Any(item => item.NickName == nickName);

                if (alreadyExist)
                {
                    return RegisterResult.AlreadyExist;
                }

                userID = GenerateUserID();

                User user = new User
                {
                    NickName = nickName,
                    Password = password,
                    Logindate = DateTime.Now,
                    UserID = userID
                };

                db.Insertable(user)
                    .ExecuteReturnIdentity();

                return RegisterResult.Success;
            }
            catch (Exception e)
            {
                Debug.LogError("注册异常！" + e);
                return RegisterResult.Failed;
            }
        }

        // 生成5位大写字母账号，随机到不重复为止
        private string GenerateUserID()
        {
            Random random = new Random();
            string userID;
            do
            {
                char[] chars = new char[5];
                for (int i = 0; i < 5; i++)
                    chars[i] = (char)('A' + random.Next(26));
                userID = new string(chars);
            }
            while (MySqlManager.Instance.sqlSugarClientDB
                .Queryable<User>()
                .Any(item => item.UserID == userID));
            return userID;
        }

        // 登录
        public LoginResult Login(string nickName, string password, out User user)
        {
            user = null;

            try
            {
                user = MySqlManager.Instance.sqlSugarClientDB
                    .Queryable<User>()
                    .Where(item => item.NickName == nickName)
                    .First();

                if (user == null)
                    return LoginResult.UserNotExist;

                if (user.Password != password)
                    return LoginResult.WrongPwd;

                user.Logindate = DateTime.Now;

                MySqlManager.Instance.sqlSugarClientDB
                    .Updateable(user)
                    .ExecuteCommand();

                return LoginResult.Success;
            }
            catch (System.Exception e)
            {
                Debug.LogError("登录异常！" + e);
                return LoginResult.Failed;
            }
        }


        public User GetUser(string userID)
        {
            try
            {
                return MySqlManager.Instance.sqlSugarClientDB
                    .Queryable<User>()
                    .First(u => u.UserID == userID);
            }
            catch (Exception e)
            {
                Debug.LogError("查询用户异常: " + e);
                return null;
            }
        }

        // 按 UID 精确查，最多 10 条，排除自己
        public List<User> SearchUsers(string keyword, string excludeUID)
        {
            try
            {
                return MySqlManager.Instance.sqlSugarClientDB
                    .Queryable<User>()
                    .Where(u => u.UserID == keyword && u.UserID != excludeUID)
                    .Take(10)
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogError("搜索用户异常！" + e);
                return new List<User>();
            }
        }
    }
}
