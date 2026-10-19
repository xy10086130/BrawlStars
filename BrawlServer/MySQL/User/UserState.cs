using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum RegisterResult
{
    Success,
    Failed,
    AlreadyExist,//账号存在
    WrongCode,//验证码错误
    Forbidden
}
public enum LoginResult
{
    Success,
    Failed,
    WrongPwd,
    UserNotExist,
    TimeoutToken
}