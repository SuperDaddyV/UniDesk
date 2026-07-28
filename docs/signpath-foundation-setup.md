# UniDesk SignPath Foundation 配置

## 目标

公开发布的 UniDesk 自有 EXE、实际承载托管代码的 DLL 和最终安装包必须具有受 Windows 信任的 Authenticode 签名。签名私钥由 SignPath 的 HSM 管理，不进入本仓库、GitHub 日志或开发电脑。

SignPath Foundation 申请与条款：

- https://signpath.org/
- https://signpath.org/terms.html
- https://docs.signpath.io/trusted-build-systems/github

项目目前使用 MIT 许可证并已有公开 Release，但最终资格和项目审批仍由 SignPath Foundation 决定。

## 申请前公开材料

提交申请前，默认分支必须公开提供以下内容：

- 仓库首页包含 `## Code signing policy`、SignPath Foundation 资助声明，以及指向 `CODE_SIGNING_POLICY.md` 和 `PRIVACY.md` 的链接。
- `CODE_SIGNING_POLICY.md` 列明 Authors、Reviewers、Approvers、签名范围、人工批准流程和证书滥用报告入口。
- `PRIVACY.md` 如实披露本地存储、和风天气、Windows 定位和用户主动检查 GitHub 更新的数据流。
- 发布说明包含同样的代码签名政策和隐私政策入口。
- 主界面显示和风天气数据时，在展开态和收缩态均显示指向 `https://www.qweather.com` 的可见来源署名。

## 一次性人工配置

1. 申请 SignPath Foundation 开源项目签名，并将仓库设置为 `https://github.com/SuperDaddyV/UniDesk`。
2. 按 SignPath 指引安装其 GitHub App，只授权 UniDesk 仓库。
3. 在 SignPath 项目中创建发布签名策略以及下面两个 Artifact Configuration。
4. 创建仅有提交签名请求权限的 API Token。
5. 在 GitHub 仓库的 Actions Secret 中创建 `SIGNPATH_API_TOKEN`。
6. 在 GitHub 仓库的 Actions Variables 中创建：
   - `SIGNPATH_ORGANIZATION_ID`
   - `SIGNPATH_PROJECT_SLUG`
   - `SIGNPATH_SIGNING_POLICY_SLUG`
   - `SIGNPATH_PAYLOAD_ARTIFACT_CONFIGURATION_SLUG`
   - `SIGNPATH_INSTALLER_ARTIFACT_CONFIGURATION_SLUG`

不得把这些实际值写入仓库文件。配置完成后，从 GitHub Actions 手动运行 `Build and sign release candidate`；工作流仅允许从正式仓库的 `main` 分支执行。

## 应用载荷 Artifact Configuration

GitHub Actions 上传目录时会生成 ZIP，因此根元素必须是 `zip-file`。该配置签署 UniDesk 自有的应用宿主和托管代码 DLL，并保留 ZIP 中的其它运行时文件和 `release-source.json`。第三方运行时文件不以 UniDesk 身份重新签名。

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
  <zip-file>
    <pe-file-set>
      <include path="App/UniDesk.exe" />
      <include path="App/UniDesk.dll" />
      <include path="App/UniDesk.Hardware.Contracts.dll" />
      <include path="HardwareService/UniDesk.HardwareService.exe" />
      <include path="HardwareService/UniDesk.HardwareService.dll" />
      <include path="HardwareService/UniDesk.Hardware.Contracts.dll" />
      <include path="HardwareRepair/UniDesk.HardwareRepair.exe" />
      <include path="HardwareRepair/UniDesk.HardwareRepair.dll" />
      <for-each>
        <authenticode-sign />
      </for-each>
    </pe-file-set>
  </zip-file>
</artifact-configuration>
```

## 安装包 Artifact Configuration

```xml
<artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
  <zip-file>
    <pe-file path="UniDesk_Setup_*.exe">
      <authenticode-sign />
    </pe-file>
  </zip-file>
</artifact-configuration>
```

## 固定发布顺序

1. 普通 CI 全部通过。
2. 手动签名工作流从一个干净的 Git 提交生成应用载荷。
3. SignPath 签署全部 UniDesk 自有 EXE 和托管代码 DLL。
4. 工作流使用已签名载荷编译安装包。
5. SignPath 签署最终安装包。
6. `Test-ReleaseReadiness.ps1` 验证源码提交、版本、所有签名、PawnIO 固定哈希并生成 `release-manifest.json`。
7. 人工核对发布测试矩阵。
8. 获得用户最终确认后，才能创建 Git tag 和 GitHub Release。

工作流只上传经过验证的候选制品，不会自动创建 Release。
