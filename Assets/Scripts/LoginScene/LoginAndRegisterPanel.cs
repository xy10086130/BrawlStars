using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class LoginAndRegisterPanel : BasePanel
{
    InputField nameIpt;
    InputField passwordIpt;
    Button loginBtn;
    Button registerBtn;

    protected override void Awake()
    {
        base.Awake();


        nameIpt = Bind<InputField>("NameIpt");
        passwordIpt = Bind<InputField>("PassWordIpt");
        loginBtn = Bind<Button>("LoginBtn");
        registerBtn = Bind<Button>("RegisterBtn");

        if (loginBtn != null) loginBtn.onClick.AddListener(LoginCheck);
        if (registerBtn != null) registerBtn.onClick.AddListener(RegisterCheck);
    }

    public override void OnOpen()
    {
        base.OnOpen();

        ClearIpt();
    }

    bool CheckInput()
    {
        if (nameIpt == null || passwordIpt == null) return false;

        if (string.IsNullOrWhiteSpace(nameIpt.text) ||
            string.IsNullOrWhiteSpace(passwordIpt.text))
        {
            FlyText.Show("昵称和密码不能为空");
            ClearIpt();
            return false;
        }
        return true;
    }

    void LoginCheck()
    {
        if (!CheckInput()) return;

        string name = nameIpt.text;
        string password = passwordIpt.text;

        ProtoManager.CSLogin(name, password, (result, uid) =>
        {
            if (result == LoginResult.Success)
            {
                FlyText.Show("登录成功");
                PlayerInfo.Instance.Init(uid, name);
                ClearIpt();
                StartCoroutine(TransitionPause());
            }
            else
            {
                FlyText.Show("登录失败：" + LoginResultText(result));
                ClearIpt();
            }
        });
    }

    public void RegisterCheck()
    {
        if (!CheckInput()) return;

        string name = nameIpt.text;
        string password = passwordIpt.text;

        ProtoManager.CSRegister(name, password, (result, userID) =>
        {
            if (result == RegisterResult.Success)
            {
                FlyText.Show("注册成功");
                PlayerInfo.Instance.Init(userID, name);
                StartCoroutine(TransitionPause());
            }
            else
            {
                FlyText.Show("注册失败：" + RegisterResultText(result));
                ClearIpt();
            }
        });
    }

    string LoginResultText(LoginResult result)
    {
        switch (result)
        {
            case LoginResult.WrongPwd: return "密码错误";
            case LoginResult.UserNotExist: return "用户不存在";
            case LoginResult.TimeoutToken: return "登录超时";
            default: return "网络异常";
        }
    }

    string RegisterResultText(RegisterResult result)
    {
        switch (result)
        {
            case RegisterResult.AlreadyExist: return "昵称已被注册";
            case RegisterResult.WrongCode: return "验证码错误";
            case RegisterResult.Forbidden: return "禁止注册";
            default: return "网络异常";
        }
    }

    void ClearIpt()
    {
        if (nameIpt != null) nameIpt.text = "";
        if (passwordIpt != null) passwordIpt.text = "";
    }

    IEnumerator TransitionPause()
    {
        yield return new WaitForSeconds(1);
        if (LoadingManager.Instance != null) LoadingManager.Instance.EnterTransition();
    }
}
